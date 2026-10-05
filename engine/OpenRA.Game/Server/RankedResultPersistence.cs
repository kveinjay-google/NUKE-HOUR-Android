#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenRA.Network;

namespace OpenRA.Server
{
	public enum RankedSubmissionKind
	{
		Result,
		Void,
	}

	public sealed class RankedPendingSubmission
	{
		public RankedSubmissionKind Kind { get; init; }
		public string MatchId { get; init; }
		public RankedMatchResult Result { get; init; }
		public string VoidReason { get; init; }
		public DateTime EndedUtc { get; init; }
	}

	public static class RankedMatchResultFactory
	{
		static RankedPendingSubmission Void(RankedServerAssignment assignment, string reason, DateTime endedUtc) => new()
		{
			Kind = RankedSubmissionKind.Void,
			MatchId = assignment.MatchId,
			VoidReason = reason,
			EndedUtc = endedUtc.Kind == DateTimeKind.Utc ? endedUtc : endedUtc.ToUniversalTime(),
		};

		public static RankedPendingSubmission Create(
			RankedServerAssignment assignment, GameInformation game, bool desynced)
		{
			ArgumentNullException.ThrowIfNull(assignment);
			var endedUtc = game?.EndTimeUtc > DateTime.MinValue ? game.EndTimeUtc : DateTime.UtcNow;
			if (desynced)
				return Void(assignment, "desync", endedUtc);
			if (game == null || !StringComparer.Ordinal.Equals(game.MapUid, assignment.Map) ||
				game.StartTimeUtc.Kind == DateTimeKind.Unspecified || game.EndTimeUtc <= game.StartTimeUtc)
				return Void(assignment, "server_failure", endedUtc);

			var players = assignment.Participants.Select(participant => new
			{
				Participant = participant,
				Matches = game.Players.Where(candidate => candidate.IsHuman &&
					StringComparer.Ordinal.Equals(candidate.Fingerprint, participant.DeviceFingerprint)).ToArray(),
			}).ToArray();
			if (players.Any(item => item.Matches.Length != 1 || item.Matches[0].Outcome == WinState.Undefined) ||
				players.Count(item => item.Matches.Length == 1 && item.Matches[0].Outcome == WinState.Won) != 1 ||
				players.Count(item => item.Matches.Length == 1 && item.Matches[0].Outcome == WinState.Lost) != 1)
				return Void(assignment, "missing_outcome", endedUtc);

			return new RankedPendingSubmission
			{
				Kind = RankedSubmissionKind.Result,
				MatchId = assignment.MatchId,
				EndedUtc = game.EndTimeUtc,
				Result = new RankedMatchResult
				{
					ResultRevision = 1,
					Outcomes = players.Select(item => new RankedMatchOutcome
					{
						AccountId = item.Participant.AccountId,
						Outcome = item.Matches[0].Outcome == WinState.Won ? "win" : "loss",
					}).ToArray(),
					Map = assignment.Map,
					RuntimeContract = assignment.RuntimeContract,
					EngineCompatibility = assignment.EngineCompatibility,
					HandshakeSchemaVersion = assignment.HandshakeSchemaVersion,
					OrdersVersion = assignment.OrdersVersion,
					ModVersion = assignment.ModVersion,
					RulesVersion = assignment.RulesVersion,
					StartedUtc = game.StartTimeUtc,
					EndedUtc = game.EndTimeUtc,
					FinalTick = Math.Max(0, game.FinalGameTick),
					Disconnects = players.Where(item => item.Matches[0].DisconnectFrame > 0)
						.Select(item => new RankedMatchDisconnect
						{
							AccountId = item.Participant.AccountId,
							Frame = item.Matches[0].DisconnectFrame,
							Reason = "disconnect",
						}).ToArray(),
				},
			};
		}

		public static RankedPendingSubmission ServerFailure(RankedServerAssignment assignment, DateTime endedUtc) =>
			Void(assignment, "server_failure", endedUtc);
	}

	public sealed class RankedResultSpool
	{
		static readonly byte[] Magic = Encoding.ASCII.GetBytes("NHR1");
		static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			PropertyNameCaseInsensitive = true,
		};
		readonly string path;
		readonly byte[] key;

		byte[] DeriveKey(string purpose)
		{
			using var hmac = new HMACSHA256(key);
			return hmac.ComputeHash(Encoding.UTF8.GetBytes("nukehour-ranked-spool-v1:" + purpose));
		}

		byte[] Authenticate(byte[] iv, byte[] ciphertext)
		{
			using var hmac = new HMACSHA256(DeriveKey("authentication"));
			return hmac.ComputeHash(Magic.Concat(iv).Concat(ciphertext).ToArray());
		}

		public RankedResultSpool(string path, byte[] key)
		{
			this.path = !string.IsNullOrWhiteSpace(path) ? path : throw new ArgumentException("Spool path is required.");
			this.key = key?.Length == 32 ? key.ToArray() : throw new ArgumentException("Spool key must contain 32 bytes.");
		}

		public bool Exists => File.Exists(path);

		public async Task SaveAsync(RankedPendingSubmission submission, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(submission);
			var plaintext = JsonSerializer.SerializeToUtf8Bytes(submission, JsonOptions);
			byte[] iv;
			byte[] ciphertext;
			using (var aes = Aes.Create())
			{
				aes.Key = DeriveKey("encryption");
				aes.Mode = CipherMode.CBC;
				aes.Padding = PaddingMode.PKCS7;
				aes.GenerateIV();
				iv = aes.IV;
				using var encryptor = aes.CreateEncryptor();
				ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
			}
			var tag = Authenticate(iv, ciphertext);

			var directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);
			var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
			try
			{
				await using (var stream = new FileStream(
					temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
				{
					await stream.WriteAsync(Magic, cancellationToken).ConfigureAwait(false);
					await stream.WriteAsync(iv, cancellationToken).ConfigureAwait(false);
					await stream.WriteAsync(tag, cancellationToken).ConfigureAwait(false);
					await stream.WriteAsync(ciphertext, cancellationToken).ConfigureAwait(false);
					await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
				}
				File.Move(temporary, path, true);
			}
			finally
			{
				if (File.Exists(temporary))
					File.Delete(temporary);
				CryptographicOperations.ZeroMemory(plaintext);
			}
		}

		public async Task<RankedPendingSubmission> LoadAsync(CancellationToken cancellationToken)
		{
			if (!File.Exists(path))
				return null;
			var document = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
			if (document.Length < Magic.Length + 16 + 32 + 16 || !document.AsSpan(0, Magic.Length).SequenceEqual(Magic))
				throw new InvalidDataException("Invalid ranked result spool.");
			var iv = document.Skip(Magic.Length).Take(16).ToArray();
			var tag = document.Skip(Magic.Length + 16).Take(32).ToArray();
			var ciphertext = document.Skip(Magic.Length + 48).ToArray();
			if (!CryptographicOperations.FixedTimeEquals(tag, Authenticate(iv, ciphertext)))
				throw new InvalidDataException("Ranked result spool authentication failed.");
			byte[] plaintext = null;
			try
			{
				using (var aes = Aes.Create())
				{
					aes.Key = DeriveKey("encryption");
					aes.IV = iv;
					aes.Mode = CipherMode.CBC;
					aes.Padding = PaddingMode.PKCS7;
					using var decryptor = aes.CreateDecryptor();
					plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
				}
				return JsonSerializer.Deserialize<RankedPendingSubmission>(plaintext, JsonOptions)
					?? throw new InvalidDataException("Empty ranked result spool.");
			}
			catch (CryptographicException ex)
			{
				throw new InvalidDataException("Ranked result spool authentication failed.", ex);
			}
			finally
			{
				if (plaintext != null)
					CryptographicOperations.ZeroMemory(plaintext);
			}
		}

		public void Delete()
		{
			if (File.Exists(path))
				File.Delete(path);
		}
	}
}
