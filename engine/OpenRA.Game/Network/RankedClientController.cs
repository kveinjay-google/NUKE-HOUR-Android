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
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using OpenRA.Server;

namespace OpenRA.Network
{
	public enum RankedClientState
	{
		SignedOut,
		Restoring,
		BindingDevice,
		Ready,
		Queued,
		Proposal,
		Accepted,
		Assigned,
		Cooldown,
		Error,
	}

	public sealed class RankedCompatibility
	{
		public string Region { get; }
		public string RuntimeContract { get; }
		public string EngineCompatibility { get; }
		public int HandshakeSchemaVersion { get; }
		public int OrdersVersion { get; }
		public string ModVersion { get; }

		public RankedCompatibility(
			string region, string runtimeContract, string engineCompatibility,
			int handshakeSchemaVersion, int ordersVersion, string modVersion)
		{
			if (string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(runtimeContract) ||
				string.IsNullOrWhiteSpace(engineCompatibility) || string.IsNullOrWhiteSpace(modVersion) ||
				handshakeSchemaVersion < 1 || ordersVersion < 1)
				throw new ArgumentException("Ranked compatibility identity is incomplete.");
			Region = region;
			RuntimeContract = runtimeContract;
			EngineCompatibility = engineCompatibility;
			HandshakeSchemaVersion = handshakeSchemaVersion;
			OrdersVersion = ordersVersion;
			ModVersion = modVersion;
		}

		public static RankedCompatibility FromActiveMod(string region)
		{
			if (Game.ModData == null)
				throw new InvalidOperationException("The active mod is unavailable.");
			return new RankedCompatibility(
				region,
				Game.ModData.RuntimeContract.Serialize(),
				Game.EngineVersion,
				ProtocolVersion.HandshakeSchema,
				ProtocolVersion.Orders,
				Game.ModData.Manifest.Metadata.CompatibilityOrVersion);
		}
	}

	public sealed class RankedInstallationIdentity
	{
		readonly Func<string, string> sign;
		public string Fingerprint { get; }
		public string PublicKey { get; }
		public string Name { get; }

		public RankedInstallationIdentity(
			string fingerprint, string publicKey, string name, Func<string, string> sign)
		{
			Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
			PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
			Name = name ?? throw new ArgumentNullException(nameof(name));
			this.sign = sign ?? throw new ArgumentNullException(nameof(sign));
		}

		public string Sign(string message) => sign(message);

		public static RankedInstallationIdentity FromPublicKey(string legacyPublicKey, string name, Func<string, string> sign)
		{
			// Legacy handshake DER contains noncanonical integer padding. The
			// ranked service requires standard SPKI and fingerprints its DER bytes.
			using var rsa = RSA.Create();
			rsa.ImportParameters(CryptoUtil.DecodePEMPublicKey(legacyPublicKey));
			var encoded = rsa.ExportSubjectPublicKeyInfo();
			using var sha = SHA256.Create();
			var fingerprint = "sha256:" + Convert.ToHexString(sha.ComputeHash(encoded)).ToLowerInvariant();
			return new RankedInstallationIdentity(fingerprint,
				new string(PemEncoding.Write("PUBLIC KEY", encoded)), name, sign);
		}

		public static RankedInstallationIdentity FromLocalProfile()
		{
			var profile = Game.LocalPlayerProfile ?? throw new InvalidOperationException("Installation key is unavailable.");
			if (string.IsNullOrEmpty(profile.ClientKeyFingerprint) || string.IsNullOrEmpty(profile.PublicKey))
				throw new InvalidOperationException("Installation key has not been generated.");
			return FromPublicKey(
				profile.PublicKey,
				Platform.IsIOS ? "This iPhone or iPad" : Platform.IsAndroid ? "This Android device" : "This Mac",
				profile.SignRankedDeviceProof);
		}
	}

	public sealed class RankedMatchAssignment
	{
		public string MatchId { get; }
		public string Opponent { get; }
		public ConnectionTarget Endpoint { get; }
		public string AdmissionToken { get; }
		public string DeviceFingerprint { get; }
		public DateTime ExpiresUtc { get; }

		public RankedMatchAssignment(
			string matchId, string opponent, ConnectionTarget endpoint, string admissionToken,
			string deviceFingerprint, DateTime expiresUtc)
		{
			MatchId = matchId;
			Opponent = opponent;
			Endpoint = endpoint;
			AdmissionToken = admissionToken;
			DeviceFingerprint = deviceFingerprint;
			ExpiresUtc = expiresUtc;
		}
	}

	public sealed class RankedClientController
	{
		readonly RankedLobbyClient client;
		readonly RankedSessionManager sessions;
		readonly Func<RankedInstallationIdentity> installationFactory;
		readonly Func<RankedCompatibility> compatibilityFactory;
		readonly SemaphoreSlim mutex = new(1, 1);
		RankedInstallationIdentity installation;
		RankedCompatibility compatibility;
		RankedDevice device;
		RankedQueueStatus status;
		bool assignmentClaimed;

		public RankedClientState State { get; private set; } = RankedClientState.SignedOut;
		public string Username => sessions.Username;
		public RankedQueueStatus QueueStatus => status;

		public RankedClientController(
			RankedLobbyClient client,
			RankedSessionManager sessions,
			Func<RankedInstallationIdentity> installationFactory,
			Func<RankedCompatibility> compatibilityFactory)
		{
			this.client = client ?? throw new ArgumentNullException(nameof(client));
			this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
			this.installationFactory = installationFactory ?? throw new ArgumentNullException(nameof(installationFactory));
			this.compatibilityFactory = compatibilityFactory ?? throw new ArgumentNullException(nameof(compatibilityFactory));
		}

		async Task EnsureDeviceAsync(CancellationToken cancellationToken)
		{
			State = RankedClientState.BindingDevice;
			installation = installationFactory();
			compatibility = compatibilityFactory();
			var access = await sessions.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
			var devices = await client.ListDevicesAsync(access, cancellationToken).ConfigureAwait(false);
			device = devices.Devices.FirstOrDefault(candidate =>
				candidate.Status == "active" && StringComparer.Ordinal.Equals(candidate.Fingerprint, installation.Fingerprint));
			if (device == null)
			{
				var challenge = await client.CreateDeviceChallengeAsync(access, cancellationToken).ConfigureAwait(false);
				var signature = installation.Sign(challenge.Message);
				if (string.IsNullOrEmpty(signature))
					throw new InvalidOperationException("Installation key could not sign the ranked device challenge.");
				device = await client.BindDeviceAsync(
					access, challenge.Id, challenge.Nonce, installation.PublicKey,
					signature, installation.Name, cancellationToken).ConfigureAwait(false);
			}

			status = null;
			assignmentClaimed = false;
			State = RankedClientState.Ready;
		}

		async Task LockedAsync(Func<Task> operation, RankedClientState failureState = RankedClientState.Error)
		{
			await mutex.WaitAsync().ConfigureAwait(false);
			var previousState = State;
			try
			{
				await operation().ConfigureAwait(false);
			}
			catch
			{
				State = previousState is RankedClientState.Accepted or RankedClientState.Assigned
					? previousState : failureState;
				throw;
			}
			finally
			{
				mutex.Release();
			}
		}

		public Task LoginAsync(string username, string password, CancellationToken cancellationToken) =>
			LockedAsync(async () =>
			{
				await sessions.LoginAsync(username, password, cancellationToken).ConfigureAwait(false);
				await EnsureDeviceAsync(cancellationToken).ConfigureAwait(false);
			});

		public async Task<RankedSession> RegisterAsync(
			string username, string password, CancellationToken cancellationToken)
		{
			RankedSession result = null;
			await LockedAsync(async () =>
			{
				result = await sessions.RegisterAsync(username, password, cancellationToken).ConfigureAwait(false);
				await EnsureDeviceAsync(cancellationToken).ConfigureAwait(false);
			}).ConfigureAwait(false);
			return result;
		}

		public Task<bool> RestoreAsync(CancellationToken cancellationToken)
		{
			var restored = false;
			return RestoreInnerAsync();

			async Task<bool> RestoreInnerAsync()
			{
				await LockedAsync(async () =>
				{
					State = RankedClientState.Restoring;
					restored = await sessions.RestoreAsync(cancellationToken).ConfigureAwait(false);
					if (restored)
						await EnsureDeviceAsync(cancellationToken).ConfigureAwait(false);
					else
						State = RankedClientState.SignedOut;
				}).ConfigureAwait(false);
				return restored;
			}
		}

		void ApplyStatus(RankedQueueStatus value)
		{
			status = value ?? throw new InvalidOperationException("Ranked queue returned no status.");
			State = value.State switch
			{
				"idle" => RankedClientState.Ready,
				"queued" => RankedClientState.Queued,
				"proposal" => RankedClientState.Proposal,
				"accepted" => RankedClientState.Accepted,
				"assigned" or "active" => RankedClientState.Assigned,
				"cooldown" => RankedClientState.Cooldown,
				_ => throw new InvalidOperationException("Unknown ranked queue state."),
			};
		}

		public Task JoinQueueAsync(CancellationToken cancellationToken) => LockedAsync(async () =>
		{
			if (State != RankedClientState.Ready || device == null || compatibility == null)
				throw new InvalidOperationException("Ranked client is not ready to queue.");
			var access = await sessions.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
			ApplyStatus(await client.JoinQueueAsync(access, new RankedQueueJoin
			{
				DeviceId = device.Id,
				Region = compatibility.Region,
				RuntimeContract = compatibility.RuntimeContract,
				EngineCompatibility = compatibility.EngineCompatibility,
				HandshakeSchemaVersion = compatibility.HandshakeSchemaVersion,
				OrdersVersion = compatibility.OrdersVersion,
				ModVersion = compatibility.ModVersion,
			}, cancellationToken).ConfigureAwait(false));
		});

		public Task PollAsync(CancellationToken cancellationToken) => LockedAsync(async () =>
		{
			if (!sessions.IsAuthenticated)
				throw new InvalidOperationException("Ranked account is signed out.");
			var access = await sessions.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
			ApplyStatus(await client.GetQueueStatusAsync(access, cancellationToken).ConfigureAwait(false));
		});

		public Task AcceptAsync(CancellationToken cancellationToken) => LockedAsync(async () =>
		{
			if (State != RankedClientState.Proposal || string.IsNullOrEmpty(status?.ProposalId))
				throw new InvalidOperationException("No ranked proposal is available.");
			var access = await sessions.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
			ApplyStatus(await client.AcceptProposalAsync(
				access, status.ProposalId, cancellationToken).ConfigureAwait(false));
		});

		public Task DeclineAsync(CancellationToken cancellationToken) => LockedAsync(async () =>
		{
			if (State != RankedClientState.Proposal || string.IsNullOrEmpty(status?.ProposalId))
				throw new InvalidOperationException("No ranked proposal is available.");
			var access = await sessions.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
			await client.DeclineProposalAsync(access, status.ProposalId, cancellationToken).ConfigureAwait(false);
			State = RankedClientState.Cooldown;
		});

		public Task CancelAsync(CancellationToken cancellationToken) => LockedAsync(async () =>
		{
			if (State is RankedClientState.Assigned or RankedClientState.Accepted)
				throw new InvalidOperationException("A confirmed ranked match cannot be cancelled from the queue.");
			var access = await sessions.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
			await client.LeaveQueueAsync(access, cancellationToken).ConfigureAwait(false);
			status = null;
			State = RankedClientState.Ready;
		});

		public RankedMatchAssignment TakeAssignment()
		{
			if (State != RankedClientState.Assigned || assignmentClaimed || status == null ||
				!Guid.TryParse(status.MatchId, out _) || string.IsNullOrWhiteSpace(status.ServerEndpoint) ||
				status.ServerPort is < 1 or > 65535 || status.AdmissionToken?.Length < 32 ||
				status.AdmissionExpiresUtc == null)
				throw new InvalidOperationException("No valid ranked assignment is available.");

			assignmentClaimed = true;
			var assignment = new RankedMatchAssignment(
				status.MatchId, status.Opponent, new ConnectionTarget(status.ServerEndpoint, status.ServerPort.Value),
				status.AdmissionToken, installation.Fingerprint, status.AdmissionExpiresUtc.Value);
			status = null;
			State = RankedClientState.Ready;
			return assignment;
		}

		public Task LogoutAsync(CancellationToken cancellationToken) => LockedAsync(async () =>
		{
			await sessions.LogoutAsync(cancellationToken).ConfigureAwait(false);
			device = null;
			status = null;
			State = RankedClientState.SignedOut;
		}, RankedClientState.SignedOut);
	}
}
