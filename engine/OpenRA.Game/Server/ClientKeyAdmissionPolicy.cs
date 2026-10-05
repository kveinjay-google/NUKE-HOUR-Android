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
using System.Linq;

namespace OpenRA.Server
{
	public readonly struct ClientKeyAdmissionDecision
	{
		public readonly bool Allowed;
		public readonly string Reason;

		public ClientKeyAdmissionDecision(bool allowed, string reason)
		{
			Allowed = allowed;
			Reason = reason;
		}
	}

	public sealed class ClientKeyAdmissionPolicy
	{
		const int MaxPublicKeyLength = 8192;
		const int MaxSignatureLength = 4096;
		readonly HashSet<string> allowed;
		readonly HashSet<string> denied;
		readonly bool requireVerified;

		public bool Enabled => requireVerified || allowed.Count > 0 || denied.Count > 0;

		public ClientKeyAdmissionPolicy(
			IEnumerable<string> allowedFingerprints,
			IEnumerable<string> deniedFingerprints,
			bool requireVerified)
		{
			allowed = Validate(allowedFingerprints, nameof(allowedFingerprints));
			denied = Validate(deniedFingerprints, nameof(deniedFingerprints));
			this.requireVerified = requireVerified;
		}

		static HashSet<string> Validate(IEnumerable<string> values, string name)
		{
			var result = new HashSet<string>(StringComparer.Ordinal);
			foreach (var value in values ?? Enumerable.Empty<string>())
			{
				if (!IsCanonicalFingerprint(value))
					throw new ArgumentException("Client key fingerprints must use canonical sha256 lowercase hex.", name);

				result.Add(value);
			}

			return result;
		}

		public static bool IsCanonicalFingerprint(string value)
		{
			if (value == null || value.Length != 71 || !value.StartsWith("sha256:", StringComparison.Ordinal))
				return false;

			for (var i = 7; i < value.Length; i++)
				if (!((value[i] >= '0' && value[i] <= '9') || (value[i] >= 'a' && value[i] <= 'f')))
					return false;

			return true;
		}

		public static bool VerifyProof(
			string publicKey,
			string fingerprint,
			string nonce,
			string signature,
			out string verifiedFingerprint, bool canonicalSpki = false)
		{
			verifiedFingerprint = null;
			if (string.IsNullOrEmpty(publicKey) || publicKey.Length > MaxPublicKeyLength ||
				string.IsNullOrEmpty(nonce) || string.IsNullOrEmpty(signature) || signature.Length > MaxSignatureLength ||
				!IsCanonicalFingerprint(fingerprint))
				return false;

			try
			{
				var parameters = CryptoUtil.DecodePEMPublicKey(publicKey);
				var calculated = canonicalSpki
					? OpenRA.Network.RankedInstallationIdentity.FromPublicKey(publicKey, "", _ => "").Fingerprint
					: CryptoUtil.PublicKeyFingerprintSha256(parameters);
				if (!string.Equals(calculated, fingerprint, StringComparison.Ordinal) ||
					!CryptoUtil.VerifySignature(parameters, nonce, signature))
					return false;

				verifiedFingerprint = calculated;
				return true;
			}
			catch
			{
				return false;
			}
		}

		public ClientKeyAdmissionDecision Evaluate(string fingerprint, bool verified)
		{
			if ((requireVerified || allowed.Count > 0) && (!verified || !IsCanonicalFingerprint(fingerprint)))
				return new ClientKeyAdmissionDecision(false, "proof_required");
			if (IsCanonicalFingerprint(fingerprint) && denied.Contains(fingerprint))
				return new ClientKeyAdmissionDecision(false, "denied");
			if (allowed.Count > 0 && !allowed.Contains(fingerprint))
				return new ClientKeyAdmissionDecision(false, "not_allowed");

			return new ClientKeyAdmissionDecision(true, "allowed");
		}
	}
}
