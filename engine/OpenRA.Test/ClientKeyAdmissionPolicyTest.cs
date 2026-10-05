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
using System.Security.Cryptography;
using NUnit.Framework;
using OpenRA.Server;

namespace OpenRA.Test
{
    [TestFixture]
    public sealed class ClientKeyAdmissionPolicyTest
    {
        const string Fingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [TestCase(Fingerprint, true)]
        [TestCase("SHA256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
        [TestCase("sha256:xyz", false)]
        [TestCase("", false)]
        public void FingerprintsAreCanonical(string value, bool expected)
        {
            Assert.That(ClientKeyAdmissionPolicy.IsCanonicalFingerprint(value), Is.EqualTo(expected));
        }

        [Test]
        public void DenyListAlwaysWins()
        {
            var policy = new ClientKeyAdmissionPolicy(
                new[] { Fingerprint }, new[] { Fingerprint }, true);
            Assert.That(policy.Evaluate(Fingerprint, true).Allowed, Is.False);
            Assert.That(policy.Evaluate(Fingerprint, true).Reason, Is.EqualTo("denied"));
        }

        [Test]
        public void NonEmptyAllowListRejectsUnknownKey()
        {
            var policy = new ClientKeyAdmissionPolicy(
                new[] { Fingerprint }, Array.Empty<string>(), true);
            var unknown = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            Assert.That(policy.Evaluate(unknown, true).Reason, Is.EqualTo("not_allowed"));
        }

        [Test]
        public void ProofVerificationBindsFingerprintAndNonce()
        {
            using var rsa = RSA.Create(2048);
            var privateParameters = rsa.ExportParameters(true);
            var publicParameters = rsa.ExportParameters(false);
            var publicKey = CryptoUtil.EncodePEMPublicKey(publicParameters);
            var fingerprint = CryptoUtil.PublicKeyFingerprintSha256(publicParameters);
            var nonce = "server-nonce";
            var signature = CryptoUtil.Sign(privateParameters, nonce);

            Assert.That(ClientKeyAdmissionPolicy.VerifyProof(
                publicKey, fingerprint, nonce, signature, out var verified), Is.True);
            Assert.That(verified, Is.EqualTo(fingerprint));
            Assert.That(ClientKeyAdmissionPolicy.VerifyProof(
                publicKey, fingerprint, nonce + "changed", signature, out _), Is.False);
        }

        [Test]
        public void InvalidPemFailsClosed()
        {
            Assert.That(ClientKeyAdmissionPolicy.VerifyProof(
                "not a key", Fingerprint, "nonce", "signature", out _), Is.False);
        }
    }
}
