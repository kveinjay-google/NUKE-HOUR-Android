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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.Network;
using OpenRA.Server;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NukeHourRankedServerClientTest
	{
		sealed class RecordingHandler : HttpMessageHandler
		{
			readonly Queue<HttpResponseMessage> responses;
			public readonly List<string> MethodsAndPaths = new();
			public readonly List<string> Authorizations = new();
			public readonly List<string> Bodies = new();

			public RecordingHandler(params HttpResponseMessage[] responses)
			{
				this.responses = new Queue<HttpResponseMessage>(responses);
			}

			protected override async Task<HttpResponseMessage> SendAsync(
				HttpRequestMessage request, CancellationToken cancellationToken)
			{
				MethodsAndPaths.Add($"{request.Method} {request.RequestUri.PathAndQuery}");
				Authorizations.Add(request.Headers.Authorization?.Parameter ?? "");
				Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
				return responses.Dequeue();
			}
		}

		static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json"),
		};

		static RankedServerRegistration Registration() => new()
		{
			ServerId = "ranked-west-1",
			ServerEndpoint = "203.0.113.20",
			ServerPort = 1234,
			Region = "us-west",
			RuntimeContract = "1|ra2-required-v1|" + new string('a', 64),
			EngineCompatibility = "release-20250330",
			HandshakeSchemaVersion = 1,
			OrdersVersion = 23,
			ModVersion = "1.0",
		};

		[Test]
		public async Task RegistrationAssignmentAdmissionAndStartUseServerBearer()
		{
			const string matchId = "11111111-1111-1111-1111-111111111111";
			const string seasonId = "22222222-2222-2222-2222-222222222222";
			const string accountId = "33333333-3333-3333-3333-333333333333";
			const string serverToken = "server-token-that-is-long-enough-123456789";
			const string fingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
			var handler = new RecordingHandler(
				Json(HttpStatusCode.Created,
					$"{{\"serverId\":\"ranked-west-1\",\"serverToken\":\"{serverToken}\",\"status\":\"idle\"}}"),
				Json(HttpStatusCode.OK,
					$"{{\"matchId\":\"{matchId}\",\"seasonId\":\"{seasonId}\",\"map\":\"maps/ranked/arena-a\",\"runtimeContract\":\"1|ra2-required-v1|{new string('a', 64)}\",\"engineCompatibility\":\"release-20250330\",\"handshakeSchemaVersion\":1,\"ordersVersion\":23,\"modVersion\":\"1.0\",\"rulesVersion\":1,\"leaseExpiresUtc\":\"2026-10-02T12:01:00Z\",\"participants\":[{{\"accountId\":\"{accountId}\",\"username\":\"Alpha\",\"deviceFingerprint\":\"{fingerprint}\",\"slot\":0}},{{\"accountId\":\"44444444-4444-4444-4444-444444444444\",\"username\":\"Bravo\",\"deviceFingerprint\":\"sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\",\"slot\":1}}]}}"),
				Json(HttpStatusCode.OK,
					$"{{\"accountId\":\"{accountId}\",\"username\":\"Alpha\",\"slot\":0}}"),
				new HttpResponseMessage(HttpStatusCode.NoContent));
			using var client = new RankedLobbyClient(
				new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));

			var session = await client.RegisterRankedServerAsync(
				"registration-credential-that-is-long-enough", Registration(), CancellationToken.None);
			var assignment = await client.GetRankedServerAssignmentAsync(
				session.ServerId, session.ServerToken, CancellationToken.None);
			var identity = await client.ConsumeRankedAdmissionAsync(
				session.ServerId, session.ServerToken, matchId,
				"admission-token-that-is-long-enough-1234", fingerprint, CancellationToken.None);
			await client.AcknowledgeRankedMatchStartAsync(
				session.ServerId, session.ServerToken, matchId,
				new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc), CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(assignment.Map, Is.EqualTo("maps/ranked/arena-a"));
				Assert.That(assignment.Participants, Has.Length.EqualTo(2));
				Assert.That(identity.AccountId, Is.EqualTo(accountId));
				Assert.That(handler.MethodsAndPaths, Is.EqualTo(new[]
				{
					"POST /v1/ranked/servers/register",
					"GET /v1/ranked/servers/ranked-west-1/assignment",
					"POST /v1/ranked/servers/ranked-west-1/admissions/consume",
					$"POST /v1/ranked/servers/ranked-west-1/matches/{matchId}/start",
				}));
				Assert.That(handler.Authorizations, Is.EqualTo(new[]
				{
					"registration-credential-that-is-long-enough", serverToken, serverToken, serverToken,
				}));
				Assert.That(handler.Bodies[2], Does.Contain("admission-token-that-is-long-enough-1234"));
			});
		}

		[Test]
		public async Task NoAssignmentReturnsNullAndRenewalUsesStableMatchId()
		{
			const string matchId = "11111111-1111-1111-1111-111111111111";
			var handler = new RecordingHandler(
				new HttpResponseMessage(HttpStatusCode.NoContent),
				Json(HttpStatusCode.OK,
					$"{{\"matchId\":\"{matchId}\",\"leaseExpiresUtc\":\"2026-10-02T12:02:00Z\"}}"));
			using var client = new RankedLobbyClient(
				new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));

			var assignment = await client.GetRankedServerAssignmentAsync(
				"ranked-west-1", "server-token-that-is-long-enough-123456789", CancellationToken.None);
			var lease = await client.RenewRankedServerAssignmentAsync(
				"ranked-west-1", "server-token-that-is-long-enough-123456789", matchId, CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(assignment, Is.Null);
				Assert.That(lease.MatchId, Is.EqualTo(matchId));
				Assert.That(handler.Bodies[1], Does.Contain(matchId));
			});
		}

		[Test]
		public void RankedServerPolicyRejectsPartialUnsafeOrOrdinaryPublishedConfiguration()
		{
			var spoolKey = Convert.ToBase64String(new byte[32]);
			var disabled = new ServerSettings();
			Assert.DoesNotThrow(() => RankedServerPolicy.Validate(disabled, null));

			var partial = new ServerSettings { RankedServerEnabled = true };
			Assert.That(() => RankedServerPolicy.Validate(partial, null), Throws.ArgumentException);

			var valid = new ServerSettings
			{
				RankedServerEnabled = true,
				RankedLobbyUrl = "https://lobby.nukehour.com",
				RankedServerId = "ranked-west-1",
				RankedPublicEndpoint = "203.0.113.20",
				RankedPublicPort = 1234,
				RankedRegion = "us-west",
				MaxPlayers = 2,
				AdvertiseOnline = false,
				EnableSingleplayer = false,
			};
			Assert.DoesNotThrow(() => RankedServerPolicy.Validate(valid, new string('s', 32), spoolKey));

			valid.AdvertiseOnline = true;
			Assert.That(() => RankedServerPolicy.Validate(valid, new string('s', 32), spoolKey), Throws.ArgumentException);
			Assert.Multiple(() =>
			{
				Assert.That(RankedServerPolicy.IsAllowedLobbyCommand("state"), Is.True);
				Assert.That(RankedServerPolicy.IsAllowedLobbyCommand("color"), Is.True);
				Assert.That(RankedServerPolicy.IsAllowedLobbyCommand("map"), Is.False);
				Assert.That(RankedServerPolicy.IsAllowedLobbyCommand("slot_bot"), Is.False);
				Assert.That(RankedServerPolicy.IsAllowedLobbyCommand("option"), Is.False);
			});
		}

		[Test]
		public async Task WorkerBindsCryptographicProofToAssignedDeviceAndConsumesOnlyOnce()
		{
			const string matchId = "11111111-1111-1111-1111-111111111111";
			const string seasonId = "22222222-2222-2222-2222-222222222222";
			const string accountId = "33333333-3333-3333-3333-333333333333";
			const string token = "server-token-that-is-long-enough-123456789";
			const string nonce = "server-auth-nonce";
			using var rsa = RSA.Create(2048);
			var privateParameters = rsa.ExportParameters(true);
			var publicParameters = rsa.ExportParameters(false);
			var publicKey = CryptoUtil.EncodePEMPublicKey(publicParameters);
			var fingerprint = "sha256:" + Convert.ToHexString(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
			var handler = new RecordingHandler(
				Json(HttpStatusCode.Created,
					$"{{\"serverId\":\"ranked-west-1\",\"serverToken\":\"{token}\",\"status\":\"idle\"}}"),
				Json(HttpStatusCode.OK,
					$"{{\"matchId\":\"{matchId}\",\"seasonId\":\"{seasonId}\",\"map\":\"maps/ranked/arena-a\",\"runtimeContract\":\"1|ra2-required-v1|{new string('a', 64)}\",\"engineCompatibility\":\"release-20250330\",\"handshakeSchemaVersion\":1,\"ordersVersion\":23,\"modVersion\":\"1.0\",\"rulesVersion\":1,\"leaseExpiresUtc\":\"2026-10-02T12:01:00Z\",\"participants\":[{{\"accountId\":\"{accountId}\",\"username\":\"Alpha\",\"deviceFingerprint\":\"{fingerprint}\",\"slot\":0}},{{\"accountId\":\"44444444-4444-4444-4444-444444444444\",\"username\":\"Bravo\",\"deviceFingerprint\":\"sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\",\"slot\":1}}]}}"),
				Json(HttpStatusCode.OK,
					$"{{\"accountId\":\"{accountId}\",\"username\":\"Alpha\",\"slot\":0}}"));
			var client = new RankedLobbyClient(new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));
			using var worker = new RankedServerWorker(
				client, "registration-credential-that-is-long-enough", Registration());
			await worker.RegisterAsync(CancellationToken.None);
			await worker.PollAssignmentAsync(CancellationToken.None);
			var handshake = new HandshakeResponse
			{
				RankedMatchId = matchId,
				RankedAdmissionToken = "admission-token-that-is-long-enough-1234",
				RankedDeviceFingerprint = fingerprint,
				ClientKeyFingerprint = fingerprint,
				ClientKeyPublicKey = publicKey,
				ClientKeySignature = CryptoUtil.Sign(privateParameters, nonce),
			};

			var identity = await worker.ConsumeAdmissionAsync(handshake, nonce, CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(identity.Username, Is.EqualTo("Alpha"));
				Assert.That(worker.ValidateClientProof(handshake, nonce + "changed", out _), Is.False);
				Assert.That(async () => await worker.ConsumeAdmissionAsync(
					handshake, nonce, CancellationToken.None), Throws.TypeOf<InvalidDataException>());
			});
		}

		[Test]
		public async Task FailedResultSubmissionKeepsExactEncryptedPayloadForRetry()
		{
			const string matchId = "11111111-1111-1111-1111-111111111111";
			const string token = "server-token-that-is-long-enough-123456789";
			var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(directory);
			try
			{
				var spool = new RankedResultSpool(
					Path.Combine(directory, "pending.bin"), Enumerable.Range(0, 32).Select(value => (byte)value).ToArray());
				var pending = new RankedPendingSubmission
				{
					Kind = RankedSubmissionKind.Result,
					MatchId = matchId,
					EndedUtc = new DateTime(2026, 10, 2, 12, 12, 0, DateTimeKind.Utc),
					Result = new RankedMatchResult
					{
						ResultRevision = 1,
						Outcomes = new[]
						{
							new RankedMatchOutcome
							{
								AccountId = "33333333-3333-3333-3333-333333333333",
								Outcome = "win",
							},
							new RankedMatchOutcome
							{
								AccountId = "44444444-4444-4444-4444-444444444444",
								Outcome = "loss",
							},
						},
						Map = "ranked-map-uid",
						RuntimeContract = Registration().RuntimeContract,
						EngineCompatibility = Registration().EngineCompatibility,
						HandshakeSchemaVersion = 1,
						OrdersVersion = 23,
						ModVersion = "1.0",
						RulesVersion = 1,
						StartedUtc = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc),
						EndedUtc = new DateTime(2026, 10, 2, 12, 12, 0, DateTimeKind.Utc),
						FinalTick = 18000,
					},
				};
				var unavailable = new RecordingHandler(
					Json(HttpStatusCode.Created,
						$"{{\"serverId\":\"ranked-west-1\",\"serverToken\":\"{token}\",\"status\":\"idle\"}}"),
					new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
				using (var worker = new RankedServerWorker(
					new RankedLobbyClient(new HttpClient(unavailable), new Uri("https://lobby.nukehour.com/")),
					"registration-credential-that-is-long-enough", Registration()))
				{
					await worker.RegisterAsync(CancellationToken.None);
					Assert.That(async () => await worker.SubmitAsync(spool, pending, CancellationToken.None),
						Throws.TypeOf<RankedLobbyException>().With.Property("Error")
							.EqualTo(RankedLobbyError.ServiceUnavailable));
				}
				Assert.That(spool.Exists, Is.True);

				var success = new RecordingHandler(
					Json(HttpStatusCode.Created,
						$"{{\"serverId\":\"ranked-west-1\",\"serverToken\":\"{token}\",\"status\":\"idle\"}}"),
					Json(HttpStatusCode.OK,
						$"{{\"matchId\":\"{matchId}\",\"state\":\"SETTLED\",\"resultRevision\":1,\"voidReason\":null,\"players\":[{{\"accountId\":\"33333333-3333-3333-3333-333333333333\",\"username\":\"Alpha\",\"outcome\":\"win\",\"ratingBefore\":1500,\"ratingAfter\":1512,\"ratingDelta\":12}},{{\"accountId\":\"44444444-4444-4444-4444-444444444444\",\"username\":\"Bravo\",\"outcome\":\"loss\",\"ratingBefore\":1500,\"ratingAfter\":1488,\"ratingDelta\":-12}}]}}"));
				using (var worker = new RankedServerWorker(
					new RankedLobbyClient(new HttpClient(success), new Uri("https://lobby.nukehour.com/")),
					"registration-credential-that-is-long-enough", Registration()))
				{
					await worker.RegisterAsync(CancellationToken.None);
					var settlement = await worker.RetryPendingAsync(spool, CancellationToken.None);
					Assert.That(settlement.ResultRevision, Is.EqualTo(1));
				}

				Assert.Multiple(() =>
				{
					Assert.That(spool.Exists, Is.False);
					Assert.That(unavailable.MethodsAndPaths[1], Is.EqualTo(
						$"POST /v1/ranked/servers/ranked-west-1/matches/{matchId}/result"));
					Assert.That(success.MethodsAndPaths[1], Is.EqualTo(unavailable.MethodsAndPaths[1]));
					Assert.That(success.Bodies[1], Is.EqualTo(unavailable.Bodies[1]));
					Assert.That(success.Bodies[1], Does.Contain("\"resultRevision\":1"));
				});
			}
			finally
			{
				Directory.Delete(directory, true);
			}
		}
	}
}
