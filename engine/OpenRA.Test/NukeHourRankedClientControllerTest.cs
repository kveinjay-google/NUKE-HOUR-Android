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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NukeHourRankedClientControllerTest
	{
		[Test]
		public void RankedIdentityCanonicalizesLegacyPublicKeyAndMatchesServerFingerprint()
		{
			using var rsa = System.Security.Cryptography.RSA.Create(2048);
			var key = rsa.ExportParameters(false);
			var legacy = CryptoUtil.EncodePEMPublicKey(key);
			var factory = typeof(RankedInstallationIdentity).GetMethod("FromPublicKey");
			Assert.That(factory, Is.Not.Null, "Ranked public keys need canonical SPKI encoding");
			var identity = (RankedInstallationIdentity)factory.Invoke(null, new object[] { legacy, "Test", new Func<string, string>(x => x) });
			Assert.That(identity.PublicKey, Is.EqualTo(new string(System.Security.Cryptography.PemEncoding.Write("PUBLIC KEY", rsa.ExportSubjectPublicKeyInfo()))));
			using var sha = System.Security.Cryptography.SHA256.Create();
			Assert.That(identity.Fingerprint, Is.EqualTo("sha256:" + Convert.ToHexString(sha.ComputeHash(rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant()));
			Assert.That(identity.Sign("challenge"), Is.EqualTo("challenge"));
		}

		sealed class MemoryStore : IRankedCredentialStore
		{
			public bool IsAvailable => true;
			public RankedStoredCredential Credential;
			public RankedStoredCredential Load() => Credential;
			public void Save(RankedStoredCredential value) => Credential = value;
			public void Delete() => Credential = null;
		}

		sealed class ScriptedHandler : HttpMessageHandler
		{
			readonly Queue<HttpResponseMessage> responses = new();
			public readonly List<string> Paths = new();
			public readonly List<string> Bodies = new();

			public void Enqueue(HttpStatusCode status, string json = "") => responses.Enqueue(new HttpResponseMessage(status)
			{
				Content = new StringContent(json, Encoding.UTF8, "application/json")
			});

			protected override async Task<HttpResponseMessage> SendAsync(
				HttpRequestMessage request, CancellationToken cancellationToken)
			{
				Paths.Add(request.RequestUri.AbsolutePath);
				Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
				return responses.Dequeue();
			}
		}

		const string SessionJson = "{\"accountId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"," +
			"\"username\":\"Commander\",\"accessToken\":\"access-token-that-is-long-enough-1234\"," +
			"\"refreshToken\":\"refresh-token-that-is-long-enough-123\"," +
			"\"accessExpiresUtc\":\"2026-10-02T12:10:00Z\"," +
			"\"refreshExpiresUtc\":\"2026-11-01T12:00:00Z\"}";

		const string DeviceJson = "{\"id\":\"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\",\"name\":\"This Device\"," +
			"\"fingerprint\":\"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"," +
			"\"status\":\"active\",\"createdUtc\":\"2026-10-02T12:00:00Z\"," +
			"\"lastSeenUtc\":\"2026-10-02T12:00:00Z\"}";

		static RankedCompatibility Compatibility() => new(
			"us-west", "contract-v1:capability:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
			"release-20261002", 1, 23, "nukehour-core-sha256-example");

		static RankedInstallationIdentity Identity() => new(
			"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
			"-----BEGIN PUBLIC KEY-----\nkey\n-----END PUBLIC KEY-----",
			"This Device", message => "signature-for-" + message);

		[Test]
		public void RankedIsExplicitlyDisabledAndUnconfiguredByDefault()
		{
			var settings = new GameSettings();
			Assert.Multiple(() =>
			{
				Assert.That(settings.EnableRanked, Is.False);
				Assert.That(settings.RankedLobbyUrl, Is.Empty);
				Assert.That(settings.RankedRegion, Is.EqualTo("us-west"));
				Assert.That(typeof(GameSettings).GetField("RankedRefreshToken"), Is.Null);
			});
		}

		[Test]
		public async Task LoginBindsInstallationThenRunsProposalToAssignmentFlow()
		{
			var handler = new ScriptedHandler();
			handler.Enqueue(HttpStatusCode.OK, SessionJson);
			handler.Enqueue(HttpStatusCode.OK, "{\"devices\":[]}");
			handler.Enqueue(HttpStatusCode.Created,
				"{\"challengeId\":\"cccccccc-cccc-cccc-cccc-cccccccccccc\"," +
				"\"nonce\":\"nonce-value-that-is-long-enough-1234\",\"message\":\"proof-message\"," +
				"\"expiresUtc\":\"2026-10-02T12:05:00Z\"}");
			handler.Enqueue(HttpStatusCode.Created, DeviceJson);
			handler.Enqueue(HttpStatusCode.Created,
				"{\"state\":\"queued\",\"ticketId\":\"dddddddd-dddd-dddd-dddd-dddddddddddd\"," +
				"\"joinedUtc\":\"2026-10-02T12:00:00Z\"}");
			handler.Enqueue(HttpStatusCode.OK,
				"{\"state\":\"proposal\",\"proposalId\":\"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee\"," +
				"\"opponent\":\"Rival\",\"proposalExpiresUtc\":\"2026-10-02T12:00:20Z\"," +
				"\"accepted\":false,\"opponentAccepted\":false}");
			handler.Enqueue(HttpStatusCode.OK,
				"{\"state\":\"assigned\",\"proposalId\":\"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee\"," +
				"\"matchId\":\"ffffffff-ffff-ffff-ffff-ffffffffffff\",\"opponent\":\"Rival\"," +
				"\"serverEndpoint\":\"203.0.113.10\",\"serverPort\":4567," +
				"\"admissionToken\":\"admission-token-that-is-long-enough-123\"," +
				"\"admissionExpiresUtc\":\"2026-10-02T12:01:00Z\"}");

			using var client = new RankedLobbyClient(new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));
			var sessions = new RankedSessionManager(client, new MemoryStore(),
				() => new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc));
			var controller = new RankedClientController(client, sessions, Identity, Compatibility);

			await controller.LoginAsync("Commander", "correct horse battery staple", CancellationToken.None);
			await controller.JoinQueueAsync(CancellationToken.None);
			await controller.PollAsync(CancellationToken.None);
			await controller.AcceptAsync(CancellationToken.None);
			var assignment = controller.TakeAssignment();

			Assert.Multiple(() =>
			{
				Assert.That(assignment.MatchId, Is.EqualTo("ffffffff-ffff-ffff-ffff-ffffffffffff"));
				Assert.That(assignment.Endpoint.ToString(), Is.EqualTo("203.0.113.10:4567"));
				Assert.That(assignment.AdmissionToken, Is.EqualTo("admission-token-that-is-long-enough-123"));
				Assert.That(controller.State, Is.EqualTo(RankedClientState.Ready));
				Assert.That(() => controller.TakeAssignment(), Throws.InvalidOperationException);
				Assert.That(handler.Bodies[3], Does.Contain("signature-for-proof-message"));
				Assert.That(handler.Bodies[4], Does.Contain("\"deviceId\":\"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\""));
				Assert.That(handler.Bodies[4], Does.Contain("\"region\":\"us-west\""));
				Assert.That(handler.Paths[^1], Is.EqualTo("/v1/ranked/queue/proposals/eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee/accept"));
			});
		}

		[TestCase(RankedClientState.Accepted)]
		[TestCase(RankedClientState.Assigned)]
		public void ConfirmedMatchSurvivesFailedPoll(RankedClientState state)
		{
			using var client = new RankedLobbyClient(new HttpClient(new ScriptedHandler()), new Uri("https://lobby.nukehour.com/"));
			var controller = new RankedClientController(client, new RankedSessionManager(client, new MemoryStore()), Identity, Compatibility);
			typeof(RankedClientController).GetProperty("State").SetValue(controller, state);
			Assert.ThrowsAsync<InvalidOperationException>(async () => await controller.PollAsync(CancellationToken.None));
			Assert.That(controller.State, Is.EqualTo(state));
		}

		[Test]
		public async Task ExistingCaseInsensitiveAccountDisplayAndBoundDeviceAreReused()
		{
			var handler = new ScriptedHandler();
			handler.Enqueue(HttpStatusCode.OK, SessionJson.Replace("Commander", "COMMANDER"));
			handler.Enqueue(HttpStatusCode.OK, "{\"devices\":[" + DeviceJson + "]}");

			using var client = new RankedLobbyClient(new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));
			var sessions = new RankedSessionManager(client, new MemoryStore(),
				() => new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc));
			var controller = new RankedClientController(client, sessions, Identity, Compatibility);
			await controller.LoginAsync("commander", "correct horse battery staple", CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(controller.Username, Is.EqualTo("COMMANDER"));
				Assert.That(controller.State, Is.EqualTo(RankedClientState.Ready));
				Assert.That(handler.Paths, Is.EqualTo(new[] { "/v1/accounts/login", "/v1/accounts/devices" }));
			});
		}

		[Test]
		public async Task QueueCancellationAndLogoutClearLocalState()
		{
			var handler = new ScriptedHandler();
			handler.Enqueue(HttpStatusCode.OK, SessionJson);
			handler.Enqueue(HttpStatusCode.OK, "{\"devices\":[" + DeviceJson + "]}");
			handler.Enqueue(HttpStatusCode.Created,
				"{\"state\":\"queued\",\"ticketId\":\"dddddddd-dddd-dddd-dddd-dddddddddddd\"," +
				"\"joinedUtc\":\"2026-10-02T12:00:00Z\"}");
			handler.Enqueue(HttpStatusCode.NoContent);
			handler.Enqueue(HttpStatusCode.NoContent);

			var store = new MemoryStore();
			using var client = new RankedLobbyClient(new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));
			var sessions = new RankedSessionManager(client, store,
				() => new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc));
			var controller = new RankedClientController(client, sessions, Identity, Compatibility);
			await controller.LoginAsync("Commander", "correct horse battery staple", CancellationToken.None);
			await controller.JoinQueueAsync(CancellationToken.None);
			await controller.CancelAsync(CancellationToken.None);
			await controller.LogoutAsync(CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(controller.State, Is.EqualTo(RankedClientState.SignedOut));
				Assert.That(store.Credential, Is.Null);
				Assert.That(handler.Paths.GetRange(handler.Paths.Count - 2, 2),
					Is.EqualTo(new[] { "/v1/ranked/queue", "/v1/accounts/logout" }));
			});
		}
	}
}
