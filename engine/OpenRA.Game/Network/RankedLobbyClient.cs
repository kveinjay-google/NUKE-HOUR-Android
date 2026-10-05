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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenRA.Network
{
	public enum RankedLobbyError
	{
		Unauthorized,
		Conflict,
		RateLimited,
		ServiceUnavailable,
		InvalidResponse,
	}

	public sealed class RankedLobbyException : Exception
	{
		public RankedLobbyError Error { get; }
		public HttpStatusCode? StatusCode { get; }

		public RankedLobbyException(RankedLobbyError error, string message,
			HttpStatusCode? statusCode = null, Exception inner = null)
			: base(message, inner)
		{
			Error = error;
			StatusCode = statusCode;
		}
	}

	public sealed class RankedSession
	{
		public string AccountId { get; init; }
		public string Username { get; init; }
		public string AccessToken { get; init; }
		public string RefreshToken { get; init; }
		public DateTime AccessExpiresUtc { get; init; }
		public DateTime RefreshExpiresUtc { get; init; }
		public string[] RecoveryCodes { get; init; } = Array.Empty<string>();

		public override string ToString() => $"RankedSession({Username}, expires {AccessExpiresUtc:O})";
	}

	public sealed class RankedDeviceChallenge
	{
		public string ChallengeId { get; init; }
		public string Id => ChallengeId;
		public string Nonce { get; init; }
		public string Message { get; init; }
		public DateTime ExpiresUtc { get; init; }
	}

	public sealed class RankedDevice
	{
		public string Id { get; init; }
		public string Name { get; init; }
		public string Fingerprint { get; init; }
		public string Status { get; init; }
		public DateTime CreatedUtc { get; init; }
		public DateTime LastSeenUtc { get; init; }
	}

	public sealed class RankedDeviceList
	{
		public RankedDevice[] Devices { get; init; } = Array.Empty<RankedDevice>();
	}

	public sealed class RankedQueueJoin
	{
		public string DeviceId { get; init; }
		public string Region { get; init; }
		public string RuntimeContract { get; init; }
		public string EngineCompatibility { get; init; }
		public int HandshakeSchemaVersion { get; init; }
		public int OrdersVersion { get; init; }
		public string ModVersion { get; init; }
	}

	public sealed class RankedQueueStatus
	{
		public string State { get; init; }
		public string TicketId { get; init; }
		public string ProposalId { get; init; }
		public string MatchId { get; init; }
		public string Opponent { get; init; }
		public DateTime? JoinedUtc { get; init; }
		public DateTime? ProposalExpiresUtc { get; init; }
		public bool? Accepted { get; init; }
		public bool? OpponentAccepted { get; init; }
		public DateTime? CooldownUntilUtc { get; init; }
		public string ServerEndpoint { get; init; }
		public int? ServerPort { get; init; }
		public string AdmissionToken { get; init; }
		public DateTime? AdmissionExpiresUtc { get; init; }

		public override string ToString() => $"RankedQueueStatus({State}, match {MatchId ?? "none"})";
	}

	public sealed class RankedSeason
	{
		public string Id { get; init; }
		public string Name { get; init; }
		public string Status { get; init; }
		public DateTime StartsUtc { get; init; }
		public DateTime EndsUtc { get; init; }
		public int RulesVersion { get; init; }
	}

	public sealed class RankedSeasonList
	{
		public RankedSeason[] Seasons { get; init; } = Array.Empty<RankedSeason>();
	}

	public sealed class RankedLeaderboardPlayer
	{
		public int Rank { get; init; }
		public string AccountId { get; init; }
		public string Username { get; init; }
		public int Rating { get; init; }
		public double Deviation { get; init; }
		public int GamesPlayed { get; init; }
		public int Wins { get; init; }
		public int Losses { get; init; }
		public int PeakRating { get; init; }
		public DateTime? LastCompletedUtc { get; init; }
	}

	public sealed class RankedLeaderboard
	{
		public RankedSeason Season { get; init; }
		public RankedLeaderboardPlayer[] Players { get; init; } = Array.Empty<RankedLeaderboardPlayer>();
	}

	public sealed class RankedRatingEvent
	{
		public string SeasonId { get; init; }
		public int Sequence { get; init; }
		public string EventType { get; init; }
		public string MatchId { get; init; }
		public int AlgorithmVersion { get; init; }
		public int RatingBefore { get; init; }
		public int RatingAfter { get; init; }
		public int RatingDelta { get; init; }
		public DateTime OccurredUtc { get; init; }
		public string Outcome { get; init; }
	}

	public sealed class RankedRatingEventList
	{
		public RankedRatingEvent[] Events { get; init; } = Array.Empty<RankedRatingEvent>();
	}

	public sealed class RankedPublicMatchPlayer
	{
		public string AccountId { get; init; }
		public string Username { get; init; }
		public string Outcome { get; init; }
		public int RatingBefore { get; init; }
		public int RatingAfter { get; init; }
		public int RatingDelta { get; init; }
	}

	public sealed class RankedPublicMatch
	{
		public string Id { get; init; }
		public string SeasonId { get; init; }
		public string State { get; init; }
		public string Map { get; init; }
		public DateTime? StartedUtc { get; init; }
		public DateTime? EndedUtc { get; init; }
		public string VoidReason { get; init; }
		public RankedPublicMatchPlayer[] Players { get; init; } = Array.Empty<RankedPublicMatchPlayer>();
	}

	public sealed class RankedServerRegistration
	{
		public string ServerId { get; init; }
		public string ServerEndpoint { get; init; }
		public int ServerPort { get; init; }
		public string Region { get; init; }
		public string RuntimeContract { get; init; }
		public string EngineCompatibility { get; init; }
		public int HandshakeSchemaVersion { get; init; }
		public int OrdersVersion { get; init; }
		public string ModVersion { get; init; }
	}

	public sealed class RankedServerSession
	{
		public string ServerId { get; init; }
		public string ServerToken { get; init; }
		public string Status { get; init; }

		public override string ToString() => $"RankedServerSession({ServerId}, {Status})";
	}

	public sealed class RankedAssignmentParticipant
	{
		public string AccountId { get; init; }
		public string Username { get; init; }
		public string DeviceFingerprint { get; init; }
		public int Slot { get; init; }
	}

	public sealed class RankedServerAssignment
	{
		public string MatchId { get; init; }
		public string SeasonId { get; init; }
		public string Map { get; init; }
		public string RuntimeContract { get; init; }
		public string EngineCompatibility { get; init; }
		public int HandshakeSchemaVersion { get; init; }
		public int OrdersVersion { get; init; }
		public string ModVersion { get; init; }
		public int RulesVersion { get; init; }
		public DateTime LeaseExpiresUtc { get; init; }
		public RankedAssignmentParticipant[] Participants { get; init; } = Array.Empty<RankedAssignmentParticipant>();
	}

	public sealed class RankedAssignmentLease
	{
		public string MatchId { get; init; }
		public DateTime LeaseExpiresUtc { get; init; }
	}

	public sealed class RankedAdmissionIdentity
	{
		public string AccountId { get; init; }
		public string Username { get; init; }
		public int Slot { get; init; }
	}

	public sealed class RankedMatchOutcome
	{
		public string AccountId { get; init; }
		public string Outcome { get; init; }
	}

	public sealed class RankedMatchDisconnect
	{
		public string AccountId { get; init; }
		public int Frame { get; init; }
		public string Reason { get; init; }
	}

	public sealed class RankedMatchResult
	{
		public int ResultRevision { get; init; }
		public RankedMatchOutcome[] Outcomes { get; init; } = Array.Empty<RankedMatchOutcome>();
		public string Map { get; init; }
		public string RuntimeContract { get; init; }
		public string EngineCompatibility { get; init; }
		public int HandshakeSchemaVersion { get; init; }
		public int OrdersVersion { get; init; }
		public string ModVersion { get; init; }
		public int RulesVersion { get; init; }
		public DateTime StartedUtc { get; init; }
		public DateTime EndedUtc { get; init; }
		public int FinalTick { get; init; }
		public RankedMatchDisconnect[] Disconnects { get; init; } = Array.Empty<RankedMatchDisconnect>();
	}

	public sealed class RankedSettlementPlayer
	{
		public string AccountId { get; init; }
		public string Username { get; init; }
		public string Outcome { get; init; }
		public int RatingBefore { get; init; }
		public int RatingAfter { get; init; }
		public int RatingDelta { get; init; }
	}

	public sealed class RankedSettlement
	{
		public string MatchId { get; init; }
		public string State { get; init; }
		public int? ResultRevision { get; init; }
		public string VoidReason { get; init; }
		public RankedSettlementPlayer[] Players { get; init; } = Array.Empty<RankedSettlementPlayer>();
	}

	public sealed class RankedLobbyClient : IDisposable
	{
		public const int MaxResponseBytes = 1024 * 1024;
		static readonly Regex FingerprintPattern = new("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant);
		static readonly HashSet<string> QueueStates = new(StringComparer.Ordinal)
		{
			"idle", "queued", "proposal", "accepted", "assigned", "cooldown", "active"
		};
		static readonly HashSet<string> PublicMatchStates = new(StringComparer.Ordinal) { "SETTLED", "VOID" };
		static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		};

		readonly HttpClient httpClient;
		readonly Uri baseUri;

		public RankedLobbyClient(HttpClient httpClient, Uri baseUri)
		{
			this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			this.baseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
			ParseBaseUri(baseUri.AbsoluteUri);
		}

		public static Uri ParseBaseUri(string value)
		{
			if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
				(uri.Scheme != Uri.UriSchemeHttps && (uri.Scheme != Uri.UriSchemeHttp || !uri.IsLoopback)) ||
				!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
				throw new ArgumentException("Ranked Lobby must use HTTPS, except loopback development URLs.", nameof(value));

			return uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(uri.AbsoluteUri + "/");
		}

		static RankedLobbyError MapStatus(HttpStatusCode status) => status switch
		{
			HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => RankedLobbyError.Unauthorized,
			HttpStatusCode.Conflict or HttpStatusCode.NotFound => RankedLobbyError.Conflict,
			(HttpStatusCode)429 => RankedLobbyError.RateLimited,
			_ => RankedLobbyError.ServiceUnavailable,
		};

		HttpRequestMessage Request(HttpMethod method, string path, object payload = null, string bearer = null)
		{
			var request = new HttpRequestMessage(method, new Uri(baseUri, path));
			if (!string.IsNullOrEmpty(bearer))
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
			if (payload != null)
				request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
			return request;
		}

		static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
		{
			if (content.Headers.ContentLength > MaxResponseBytes)
				throw new RankedLobbyException(RankedLobbyError.InvalidResponse, "Ranked Lobby response exceeded the size limit.");

			await using var input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
			using var output = new MemoryStream();
			var buffer = new byte[8192];
			while (true)
			{
				var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
				if (read == 0)
					break;
				if (output.Length + read > MaxResponseBytes)
					throw new RankedLobbyException(RankedLobbyError.InvalidResponse, "Ranked Lobby response exceeded the size limit.");
				output.Write(buffer, 0, read);
			}
			return output.ToArray();
		}

		async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			try
			{
				using (request)
				using (var response = await httpClient.SendAsync(
					request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
				{
					if (!response.IsSuccessStatusCode)
						throw new RankedLobbyException(MapStatus(response.StatusCode),
							$"Ranked Lobby returned HTTP {(int)response.StatusCode}.", response.StatusCode);
					var bytes = await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);
					try
					{
						var value = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
						if (value == null)
							throw new JsonException("Empty response.");
						Validate(value);
						return value;
					}
					catch (Exception ex) when (ex is JsonException or InvalidDataException)
					{
						throw new RankedLobbyException(RankedLobbyError.InvalidResponse,
							"Ranked Lobby returned an invalid response.", response.StatusCode, ex);
					}
				}
			}
			catch (RankedLobbyException)
			{
				throw;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
			{
				throw new RankedLobbyException(RankedLobbyError.ServiceUnavailable,
					"Ranked Lobby is unavailable.", null, ex);
			}
		}

		async Task<T> SendOptionalAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
			where T : class
		{
			try
			{
				using (request)
				using (var response = await httpClient.SendAsync(
					request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
				{
					if (response.StatusCode == HttpStatusCode.NoContent)
						return null;
					if (!response.IsSuccessStatusCode)
						throw new RankedLobbyException(MapStatus(response.StatusCode),
							$"Ranked Lobby returned HTTP {(int)response.StatusCode}.", response.StatusCode);
					var bytes = await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);
					try
					{
						var value = JsonSerializer.Deserialize<T>(bytes, JsonOptions);
						if (value == null)
							throw new JsonException("Empty response.");
						Validate(value);
						return value;
					}
					catch (Exception ex) when (ex is JsonException or InvalidDataException)
					{
						throw new RankedLobbyException(RankedLobbyError.InvalidResponse,
							"Ranked Lobby returned an invalid response.", response.StatusCode, ex);
					}
				}
			}
			catch (RankedLobbyException)
			{
				throw;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
			{
				throw new RankedLobbyException(RankedLobbyError.ServiceUnavailable,
					"Ranked Lobby is unavailable.", null, ex);
			}
		}

		async Task SendNoContentAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			try
			{
				using (request)
				using (var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false))
					if (!response.IsSuccessStatusCode)
						throw new RankedLobbyException(MapStatus(response.StatusCode),
							$"Ranked Lobby returned HTTP {(int)response.StatusCode}.", response.StatusCode);
			}
			catch (RankedLobbyException)
			{
				throw;
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
			{
				throw new RankedLobbyException(RankedLobbyError.ServiceUnavailable,
					"Ranked Lobby is unavailable.", null, ex);
			}
		}

		static void RequireGuid(string value, string name)
		{
			if (!Guid.TryParse(value, out _))
				throw new InvalidDataException($"Invalid {name}.");
		}

		static void RequireText(string value, string name, int maxLength)
		{
			if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength || value.Any(char.IsControl))
				throw new InvalidDataException($"Invalid {name}.");
		}

		static void Validate<T>(T value)
		{
			switch (value)
			{
				case RankedSession session:
					RequireGuid(session.AccountId, "account id");
					RequireText(session.Username, "username", 96);
					if (session.AccessToken?.Length < 32 || session.AccessToken.Length > 256 ||
						session.RefreshToken?.Length < 32 || session.RefreshToken.Length > 256 ||
						session.AccessExpiresUtc.Kind == DateTimeKind.Unspecified ||
						session.RefreshExpiresUtc.Kind == DateTimeKind.Unspecified)
						throw new InvalidDataException("Invalid ranked session.");
					break;
				case RankedDeviceChallenge challenge:
					RequireGuid(challenge.ChallengeId, "challenge id");
					RequireText(challenge.Nonce, "challenge nonce", 256);
					if (challenge.Nonce.Length < 32 || string.IsNullOrEmpty(challenge.Message) ||
						challenge.Message.Length > 1024 || challenge.ExpiresUtc.Kind == DateTimeKind.Unspecified)
						throw new InvalidDataException("Invalid device challenge.");
					break;
				case RankedDevice device:
					RequireGuid(device.Id, "device id");
					RequireText(device.Name, "device name", 48);
					if (!FingerprintPattern.IsMatch(device.Fingerprint ?? "") || device.Status is not ("active" or "revoked"))
						throw new InvalidDataException("Invalid ranked device.");
					break;
				case RankedDeviceList list:
					if (list.Devices == null || list.Devices.Length > 64)
						throw new InvalidDataException("Invalid device list.");
					foreach (var item in list.Devices)
						Validate(item);
					break;
				case RankedQueueStatus status:
					if (!QueueStates.Contains(status.State ?? "") || status.ServerPort is < 1 or > 65535 ||
						(status.AdmissionToken != null && (status.AdmissionToken.Length < 32 || status.AdmissionToken.Length > 256)))
						throw new InvalidDataException("Invalid ranked queue status.");
					break;
				case RankedSeasonList seasons:
					if (seasons.Seasons == null || seasons.Seasons.Length > 100)
						throw new InvalidDataException("Invalid season list.");
					foreach (var season in seasons.Seasons)
						Validate(season);
					break;
				case RankedSeason season:
					RequireGuid(season.Id, "season id");
					RequireText(season.Name, "season name", 96);
					if (season.RulesVersion < 1 || season.EndsUtc <= season.StartsUtc)
						throw new InvalidDataException("Invalid season.");
					break;
				case RankedLeaderboard leaderboard:
					if (leaderboard.Season == null || leaderboard.Players == null || leaderboard.Players.Length > 100)
						throw new InvalidDataException("Invalid leaderboard.");
					Validate(leaderboard.Season);
					foreach (var player in leaderboard.Players)
					{
						RequireGuid(player.AccountId, "leaderboard account id");
						RequireText(player.Username, "leaderboard username", 96);
						if (player.Rank < 1 || player.GamesPlayed < 0 || player.Wins < 0 || player.Losses < 0)
							throw new InvalidDataException("Invalid leaderboard player.");
					}
					break;
				case RankedRatingEventList events:
					if (events.Events == null || events.Events.Length > 100)
						throw new InvalidDataException("Invalid rating event list.");
					foreach (var item in events.Events)
					{
						RequireGuid(item.SeasonId, "rating event season id");
						if (item.MatchId != null)
							RequireGuid(item.MatchId, "rating event match id");
						RequireText(item.EventType, "rating event type", 24);
						if (item.Sequence < 1 || item.AlgorithmVersion < 1 ||
							item.OccurredUtc.Kind == DateTimeKind.Unspecified ||
							item.Outcome is not (null or "win" or "loss"))
							throw new InvalidDataException("Invalid rating event.");
					}
					break;
				case RankedPublicMatch match:
					RequireGuid(match.Id, "public match id");
					RequireGuid(match.SeasonId, "public match season id");
					RequireText(match.State, "public match state", 32);
					if (match.Map?.Length > 128 || match.StartedUtc?.Kind == DateTimeKind.Unspecified ||
						match.EndedUtc?.Kind == DateTimeKind.Unspecified || !PublicMatchStates.Contains(match.State) ||
						match.Players == null || match.Players.Length != 2)
						throw new InvalidDataException("Invalid public match.");
					foreach (var player in match.Players)
					{
						RequireGuid(player.AccountId, "public match player account id");
						RequireText(player.Username, "public match player username", 96);
						if (player.Outcome is not ("win" or "loss" or "void"))
							throw new InvalidDataException("Invalid public match player.");
					}
					break;
				case RankedServerSession server:
					RequireText(server.ServerId, "server id", 64);
					RequireText(server.Status, "server status", 32);
					if (server.ServerToken?.Length < 32 || server.ServerToken.Length > 256)
						throw new InvalidDataException("Invalid ranked server session.");
					break;
				case RankedServerAssignment assignment:
					RequireGuid(assignment.MatchId, "match id");
					RequireGuid(assignment.SeasonId, "season id");
					RequireText(assignment.Map, "assignment map", 128);
					RequireText(assignment.RuntimeContract, "runtime contract", 256);
					RequireText(assignment.EngineCompatibility, "engine compatibility", 128);
					RequireText(assignment.ModVersion, "mod version", 128);
					if (assignment.HandshakeSchemaVersion < 1 || assignment.OrdersVersion < 1 ||
						assignment.RulesVersion < 1 || assignment.LeaseExpiresUtc.Kind == DateTimeKind.Unspecified ||
						assignment.Participants == null || assignment.Participants.Length != 2 ||
						assignment.Participants.Select(participant => participant.Slot).Distinct().Count() != 2)
						throw new InvalidDataException("Invalid ranked assignment.");
					foreach (var participant in assignment.Participants)
					{
						RequireGuid(participant.AccountId, "participant account id");
						RequireText(participant.Username, "participant username", 96);
						if (!FingerprintPattern.IsMatch(participant.DeviceFingerprint ?? "") || participant.Slot is < 0 or > 1)
							throw new InvalidDataException("Invalid ranked assignment participant.");
					}
					break;
				case RankedAssignmentLease lease:
					RequireGuid(lease.MatchId, "match id");
					if (lease.LeaseExpiresUtc.Kind == DateTimeKind.Unspecified)
						throw new InvalidDataException("Invalid ranked assignment lease.");
					break;
				case RankedAdmissionIdentity identity:
					RequireGuid(identity.AccountId, "admitted account id");
					RequireText(identity.Username, "admitted username", 96);
					if (identity.Slot is < 0 or > 1)
						throw new InvalidDataException("Invalid ranked admission identity.");
					break;
				case RankedSettlement settlement:
					RequireGuid(settlement.MatchId, "settlement match id");
					if (settlement.State is not ("SETTLED" or "VOID") || settlement.Players == null ||
						settlement.Players.Length > 2 || (settlement.State == "SETTLED" && settlement.Players.Length != 2))
						throw new InvalidDataException("Invalid ranked settlement.");
					foreach (var player in settlement.Players)
					{
						RequireGuid(player.AccountId, "settlement account id");
						RequireText(player.Username, "settlement username", 96);
					}
					break;
			}
		}

		public Task<RankedSession> RegisterAsync(string username, string password, CancellationToken cancellationToken) =>
			SendAsync<RankedSession>(Request(HttpMethod.Post, "v1/accounts/register", new { username, password }), cancellationToken);

		public Task<RankedSession> LoginAsync(string username, string password, CancellationToken cancellationToken) =>
			SendAsync<RankedSession>(Request(HttpMethod.Post, "v1/accounts/login", new { username, password }), cancellationToken);

		public Task<RankedSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
			SendAsync<RankedSession>(Request(HttpMethod.Post, "v1/accounts/refresh", new { refreshToken }), cancellationToken);

		public Task LogoutAsync(string accessToken, CancellationToken cancellationToken) =>
			SendNoContentAsync(Request(HttpMethod.Post, "v1/accounts/logout", bearer: accessToken), cancellationToken);

		public Task<RankedDeviceChallenge> CreateDeviceChallengeAsync(
			string accessToken, CancellationToken cancellationToken) =>
			SendAsync<RankedDeviceChallenge>(Request(
				HttpMethod.Post, "v1/accounts/devices/challenge", bearer: accessToken), cancellationToken);

		public Task<RankedDevice> BindDeviceAsync(
			string accessToken, string challengeId, string nonce, string publicKey,
			string signature, string name, CancellationToken cancellationToken) =>
			SendAsync<RankedDevice>(Request(HttpMethod.Post, "v1/accounts/devices/bind",
				new { challengeId, nonce, publicKey, signature, name }, accessToken), cancellationToken);

		public Task<RankedDeviceList> ListDevicesAsync(string accessToken, CancellationToken cancellationToken) =>
			SendAsync<RankedDeviceList>(Request(HttpMethod.Get, "v1/accounts/devices", bearer: accessToken), cancellationToken);

		public Task<RankedQueueStatus> JoinQueueAsync(
			string accessToken, RankedQueueJoin join, CancellationToken cancellationToken) =>
			SendAsync<RankedQueueStatus>(Request(HttpMethod.Post, "v1/ranked/queue", join, accessToken), cancellationToken);

		public Task<RankedQueueStatus> GetQueueStatusAsync(string accessToken, CancellationToken cancellationToken) =>
			SendAsync<RankedQueueStatus>(Request(HttpMethod.Get, "v1/ranked/queue", bearer: accessToken), cancellationToken);

		public Task LeaveQueueAsync(string accessToken, CancellationToken cancellationToken) =>
			SendNoContentAsync(Request(HttpMethod.Delete, "v1/ranked/queue", bearer: accessToken), cancellationToken);

		public Task<RankedQueueStatus> AcceptProposalAsync(
			string accessToken, string proposalId, CancellationToken cancellationToken) =>
			SendAsync<RankedQueueStatus>(Request(HttpMethod.Post,
				$"v1/ranked/queue/proposals/{Uri.EscapeDataString(proposalId)}/accept", bearer: accessToken), cancellationToken);

		public Task DeclineProposalAsync(string accessToken, string proposalId, CancellationToken cancellationToken) =>
			SendNoContentAsync(Request(HttpMethod.Post,
				$"v1/ranked/queue/proposals/{Uri.EscapeDataString(proposalId)}/decline", bearer: accessToken), cancellationToken);

		public Task<RankedSeasonList> GetSeasonsAsync(CancellationToken cancellationToken) =>
			SendAsync<RankedSeasonList>(Request(HttpMethod.Get, "v1/ranked/seasons"), cancellationToken);

		public Task<RankedLeaderboard> GetLeaderboardAsync(
			string seasonId, int limit, CancellationToken cancellationToken)
		{
			if (limit < 1 || limit > 100)
				throw new ArgumentOutOfRangeException(nameof(limit));
			if (!Guid.TryParse(seasonId, out _))
				throw new ArgumentException("A valid ranked season id is required.", nameof(seasonId));
			return SendAsync<RankedLeaderboard>(Request(HttpMethod.Get,
				$"v1/ranked/seasons/{Uri.EscapeDataString(seasonId)}/leaderboard?limit={limit}"), cancellationToken);
		}

		public Task<RankedRatingEventList> GetRatingEventsAsync(
			string accessToken, int limit, CancellationToken cancellationToken)
		{
			if (limit < 1 || limit > 100)
				throw new ArgumentOutOfRangeException(nameof(limit));
			return SendAsync<RankedRatingEventList>(Request(HttpMethod.Get,
				$"v1/ranked/me/rating-events?limit={limit}", bearer: accessToken), cancellationToken);
		}

		public Task<RankedPublicMatch> GetPublicMatchAsync(
			string matchId, CancellationToken cancellationToken) =>
			SendAsync<RankedPublicMatch>(Request(HttpMethod.Get,
				$"v1/ranked/matches/{Uri.EscapeDataString(matchId)}"), cancellationToken);

		public Task<RankedServerSession> RegisterRankedServerAsync(
			string registrationCredential, RankedServerRegistration registration, CancellationToken cancellationToken) =>
			SendAsync<RankedServerSession>(Request(
				HttpMethod.Post, "v1/ranked/servers/register", registration, registrationCredential), cancellationToken);

		public Task<RankedServerAssignment> GetRankedServerAssignmentAsync(
			string serverId, string serverToken, CancellationToken cancellationToken) =>
			SendOptionalAsync<RankedServerAssignment>(Request(HttpMethod.Get,
				$"v1/ranked/servers/{Uri.EscapeDataString(serverId)}/assignment", bearer: serverToken), cancellationToken);

		public Task<RankedAssignmentLease> RenewRankedServerAssignmentAsync(
			string serverId, string serverToken, string matchId, CancellationToken cancellationToken) =>
			SendAsync<RankedAssignmentLease>(Request(HttpMethod.Post,
				$"v1/ranked/servers/{Uri.EscapeDataString(serverId)}/assignment/renew",
				new { matchId }, serverToken), cancellationToken);

		public Task<RankedAdmissionIdentity> ConsumeRankedAdmissionAsync(
			string serverId, string serverToken, string matchId, string admissionToken,
			string deviceFingerprint, CancellationToken cancellationToken) =>
			SendAsync<RankedAdmissionIdentity>(Request(HttpMethod.Post,
				$"v1/ranked/servers/{Uri.EscapeDataString(serverId)}/admissions/consume",
				new { matchId, admissionToken, deviceFingerprint }, serverToken), cancellationToken);

		public Task AcknowledgeRankedMatchStartAsync(
			string serverId, string serverToken, string matchId, DateTime startedUtc,
			CancellationToken cancellationToken) =>
			SendNoContentAsync(Request(HttpMethod.Post,
				$"v1/ranked/servers/{Uri.EscapeDataString(serverId)}/matches/{Uri.EscapeDataString(matchId)}/start",
				new { startedUtc }, serverToken), cancellationToken);

		public Task<RankedSettlement> SubmitRankedMatchResultAsync(
			string serverId, string serverToken, string matchId, RankedMatchResult result,
			CancellationToken cancellationToken) =>
			SendAsync<RankedSettlement>(Request(HttpMethod.Post,
				$"v1/ranked/servers/{Uri.EscapeDataString(serverId)}/matches/{Uri.EscapeDataString(matchId)}/result",
				result, serverToken), cancellationToken);

		public Task<RankedSettlement> VoidRankedMatchAsync(
			string serverId, string serverToken, string matchId, string reason, DateTime endedUtc,
			CancellationToken cancellationToken) =>
			SendAsync<RankedSettlement>(Request(HttpMethod.Post,
				$"v1/ranked/servers/{Uri.EscapeDataString(serverId)}/matches/{Uri.EscapeDataString(matchId)}/void",
				new { reason, endedUtc }, serverToken), cancellationToken);

		public void Dispose() => httpClient.Dispose();
	}
}
