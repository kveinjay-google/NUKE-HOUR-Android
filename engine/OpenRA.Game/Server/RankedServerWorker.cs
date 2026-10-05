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
using System.Threading;
using System.Threading.Tasks;
using OpenRA.Network;

namespace OpenRA.Server
{
	public sealed class RankedServerWorker : IDisposable
	{
		readonly RankedLobbyClient client;
		readonly string registrationCredential;
		readonly RankedServerRegistration registration;
		readonly HashSet<string> consumedFingerprints = new(StringComparer.Ordinal);
		RankedServerSession session;
		bool startAcknowledged;

		public RankedServerAssignment Assignment { get; private set; }

		public RankedServerWorker(
			RankedLobbyClient client, string registrationCredential, RankedServerRegistration registration)
		{
			this.client = client ?? throw new ArgumentNullException(nameof(client));
			this.registrationCredential = !string.IsNullOrEmpty(registrationCredential)
				? registrationCredential : throw new ArgumentException("Ranked registration credential is required.");
			this.registration = registration ?? throw new ArgumentNullException(nameof(registration));
		}

		public async Task RegisterAsync(CancellationToken cancellationToken)
		{
			if (session != null)
				return;
			session = await client.RegisterRankedServerAsync(
				registrationCredential, registration, cancellationToken).ConfigureAwait(false);
			if (!StringComparer.Ordinal.Equals(session.ServerId, registration.ServerId))
				throw new InvalidDataException("Ranked coordinator returned a different server id.");
		}

		void ValidateCompatibility(RankedServerAssignment assignment)
		{
			if (!StringComparer.Ordinal.Equals(assignment.RuntimeContract, registration.RuntimeContract) ||
				!StringComparer.Ordinal.Equals(assignment.EngineCompatibility, registration.EngineCompatibility) ||
				assignment.HandshakeSchemaVersion != registration.HandshakeSchemaVersion ||
				assignment.OrdersVersion != registration.OrdersVersion ||
				!StringComparer.Ordinal.Equals(assignment.ModVersion, registration.ModVersion))
				throw new InvalidDataException("Ranked assignment is incompatible with this server runtime.");
		}

		public async Task<RankedServerAssignment> PollAssignmentAsync(CancellationToken cancellationToken)
		{
			if (session == null)
				throw new InvalidOperationException("Ranked server has not registered.");
			if (Assignment != null)
				return Assignment;
			var assignment = await client.GetRankedServerAssignmentAsync(
				session.ServerId, session.ServerToken, cancellationToken).ConfigureAwait(false);
			if (assignment == null)
				return null;
			ValidateCompatibility(assignment);
			Assignment = assignment;
			return assignment;
		}

		public async Task RenewAssignmentAsync(CancellationToken cancellationToken)
		{
			if (Assignment == null || session == null)
				throw new InvalidOperationException("No ranked assignment is active.");
			var lease = await client.RenewRankedServerAssignmentAsync(
				session.ServerId, session.ServerToken, Assignment.MatchId, cancellationToken).ConfigureAwait(false);
			if (!StringComparer.Ordinal.Equals(lease.MatchId, Assignment.MatchId))
				throw new InvalidDataException("Ranked coordinator renewed a different match.");
		}

		public bool ValidateClientProof(HandshakeResponse handshake, string nonce, out string fingerprint)
		{
			fingerprint = null;
			if (Assignment == null || handshake == null ||
				!StringComparer.Ordinal.Equals(handshake.RankedMatchId, Assignment.MatchId) ||
				string.IsNullOrEmpty(handshake.RankedAdmissionToken) ||
				!StringComparer.Ordinal.Equals(handshake.RankedDeviceFingerprint, handshake.ClientKeyFingerprint) ||
				!Assignment.Participants.Any(participant =>
					StringComparer.Ordinal.Equals(participant.DeviceFingerprint, handshake.RankedDeviceFingerprint)))
				return false;

			return ClientKeyAdmissionPolicy.VerifyProof(
				handshake.ClientKeyPublicKey, handshake.ClientKeyFingerprint, nonce,
				handshake.ClientKeySignature, out fingerprint, canonicalSpki: true) &&
				StringComparer.Ordinal.Equals(fingerprint, handshake.RankedDeviceFingerprint);
		}

		public async Task<RankedAdmissionIdentity> ConsumeAdmissionAsync(
			HandshakeResponse handshake, string nonce, CancellationToken cancellationToken)
		{
			if (!ValidateClientProof(handshake, nonce, out var fingerprint) || session == null)
				throw new InvalidDataException("Ranked client proof is invalid.");
			lock (consumedFingerprints)
				if (consumedFingerprints.Contains(fingerprint))
					throw new InvalidDataException("Ranked admission was already consumed.");

			var identity = await client.ConsumeRankedAdmissionAsync(
				session.ServerId, session.ServerToken, Assignment.MatchId,
				handshake.RankedAdmissionToken, fingerprint, cancellationToken).ConfigureAwait(false);
			var participant = Assignment.Participants.SingleOrDefault(candidate =>
				StringComparer.Ordinal.Equals(candidate.DeviceFingerprint, fingerprint));
			if (participant == null ||
				!StringComparer.Ordinal.Equals(identity.AccountId, participant.AccountId) ||
				!StringComparer.Ordinal.Equals(identity.Username, participant.Username) || identity.Slot != participant.Slot)
				throw new InvalidDataException("Ranked admission identity did not match the assignment.");

			lock (consumedFingerprints)
				if (!consumedFingerprints.Add(fingerprint))
					throw new InvalidDataException("Ranked admission was already consumed.");
			return identity;
		}

		public async Task AcknowledgeStartAsync(DateTime startedUtc, CancellationToken cancellationToken)
		{
			if (startAcknowledged)
				return;
			if (Assignment == null || session == null)
				throw new InvalidOperationException("No ranked assignment is active.");
			await client.AcknowledgeRankedMatchStartAsync(
				session.ServerId, session.ServerToken, Assignment.MatchId,
				startedUtc, cancellationToken).ConfigureAwait(false);
			startAcknowledged = true;
		}

		async Task<RankedSettlement> SendPendingAsync(
			RankedPendingSubmission pending, CancellationToken cancellationToken)
		{
			if (session == null)
				throw new InvalidOperationException("Ranked server has not registered.");
			return pending.Kind == RankedSubmissionKind.Result
				? await client.SubmitRankedMatchResultAsync(
					session.ServerId, session.ServerToken, pending.MatchId,
					pending.Result, cancellationToken).ConfigureAwait(false)
				: await client.VoidRankedMatchAsync(
					session.ServerId, session.ServerToken, pending.MatchId,
					pending.VoidReason, pending.EndedUtc, cancellationToken).ConfigureAwait(false);
		}

		public async Task<RankedSettlement> SubmitAsync(
			RankedResultSpool spool, RankedPendingSubmission pending, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(spool);
			ArgumentNullException.ThrowIfNull(pending);
			await spool.SaveAsync(pending, cancellationToken).ConfigureAwait(false);
			var settlement = await SendPendingAsync(pending, cancellationToken).ConfigureAwait(false);
			if (!StringComparer.Ordinal.Equals(settlement.MatchId, pending.MatchId))
				throw new InvalidDataException("Ranked settlement acknowledged a different match.");
			spool.Delete();
			return settlement;
		}

		public async Task<RankedSettlement> RetryPendingAsync(
			RankedResultSpool spool, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(spool);
			var pending = await spool.LoadAsync(cancellationToken).ConfigureAwait(false);
			if (pending == null)
				return null;
			var settlement = await SendPendingAsync(pending, cancellationToken).ConfigureAwait(false);
			if (!StringComparer.Ordinal.Equals(settlement.MatchId, pending.MatchId))
				throw new InvalidDataException("Ranked settlement acknowledged a different match.");
			spool.Delete();
			return settlement;
		}

		public void Dispose() => client.Dispose();
	}
}
