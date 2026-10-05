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
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NukeHourRankedLobbyClientTest
	{
		sealed class RecordingHandler : HttpMessageHandler
		{
			readonly Queue<HttpResponseMessage> responses;
			public readonly List<string> Paths = new();
			public readonly List<string> Authorizations = new();
			public readonly List<string> Bodies = new();

			public RecordingHandler(params HttpResponseMessage[] responses)
			{
				this.responses = new Queue<HttpResponseMessage>(responses);
			}

			protected override async Task<HttpResponseMessage> SendAsync(
				HttpRequestMessage request, CancellationToken cancellationToken)
			{
				Paths.Add(request.RequestUri.PathAndQuery);
				Authorizations.Add(request.Headers.Authorization?.Parameter ?? "");
				Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
				return responses.Dequeue();
			}
		}

		static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json"),
		};

		const string SessionJson = "{\"accountId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"," +
			"\"username\":\"Commander\",\"accessToken\":\"access-token-that-is-long-enough-1234\"," +
			"\"refreshToken\":\"refresh-token-that-is-long-enough-123\"," +
			"\"accessExpiresUtc\":\"2026-10-02T12:10:00Z\"," +
			"\"refreshExpiresUtc\":\"2026-11-01T12:00:00Z\"}";

		[Test]
		public void RankedLobbyUrlRequiresHttpsExceptForLoopbackDevelopment()
		{
			Assert.Multiple(() =>
			{
				Assert.That(RankedLobbyClient.ParseBaseUri("https://lobby.nukehour.com").AbsoluteUri,
					Is.EqualTo("https://lobby.nukehour.com/"));
				Assert.That(RankedLobbyClient.ParseBaseUri("http://127.0.0.1:8080").IsLoopback, Is.True);
				Assert.That(() => RankedLobbyClient.ParseBaseUri("http://203.0.113.7"), Throws.ArgumentException);
				Assert.That(() => RankedLobbyClient.ParseBaseUri("https://lobby.nukehour.com/?token=secret"), Throws.ArgumentException);
			});
		}

		[Test]
		public async Task LoginAndRefreshUseBoundedJsonWithoutLoggingSecrets()
		{
			var handler = new RecordingHandler(
				Json(HttpStatusCode.OK, SessionJson),
				Json(HttpStatusCode.OK, SessionJson.Replace("Commander", "COMMANDER")));
			using var client = new RankedLobbyClient(
				new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));

			var session = await client.LoginAsync("Commander", "correct horse battery staple", CancellationToken.None);
			var refreshed = await client.RefreshAsync(session.RefreshToken, CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(session.Username, Is.EqualTo("Commander"));
				Assert.That(refreshed.Username, Is.EqualTo("COMMANDER"));
				Assert.That(handler.Paths, Is.EqualTo(new[] { "/v1/accounts/login", "/v1/accounts/refresh" }));
				Assert.That(handler.Bodies[0], Does.Contain("\"username\":\"Commander\""));
				Assert.That(handler.Bodies[0], Does.Contain("correct horse battery staple"));
				Assert.That(handler.Bodies[1], Does.Contain("refresh-token-that-is-long-enough-123"));
				Assert.That(session.ToString(), Does.Not.Contain(session.AccessToken));
				Assert.That(session.ToString(), Does.Not.Contain(session.RefreshToken));
			});
		}

		[Test]
		public async Task DeviceChallengeAndBindingUseAccessBearerAndSha256Proof()
		{
			var handler = new RecordingHandler(
				Json(HttpStatusCode.Created,
					"{\"challengeId\":\"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\"," +
					"\"nonce\":\"nonce-value-that-is-long-enough-1234\"," +
					"\"message\":\"nukehour-ranked-device-v1\\naccount\\nnonce\"," +
					"\"expiresUtc\":\"2026-10-02T12:05:00Z\"}"),
				Json(HttpStatusCode.Created,
					"{\"id\":\"cccccccc-cccc-cccc-cccc-cccccccccccc\",\"name\":\"This Mac\"," +
					"\"fingerprint\":\"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"," +
					"\"status\":\"active\",\"createdUtc\":\"2026-10-02T12:00:00Z\"," +
					"\"lastSeenUtc\":\"2026-10-02T12:00:00Z\"}"));
			using var client = new RankedLobbyClient(
				new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));

			var challenge = await client.CreateDeviceChallengeAsync("access-token", CancellationToken.None);
			var device = await client.BindDeviceAsync(
				"access-token", challenge.Id, challenge.Nonce, "PUBLIC KEY", "signature", "This Mac", CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(device.Name, Is.EqualTo("This Mac"));
				Assert.That(handler.Authorizations, Is.EqualTo(new[] { "access-token", "access-token" }));
				Assert.That(handler.Bodies[1], Does.Contain("\"challengeId\":\"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\""));
				Assert.That(handler.Bodies[1], Does.Contain("\"publicKey\":\"PUBLIC KEY\""));
			});
		}

		[Test]
		public void QueueAndLeaderboardModelsRejectMalformedOrOversizedResponses()
		{
			var oversized = new string('x', RankedLobbyClient.MaxResponseBytes + 1);
			var handler = new RecordingHandler(
				Json(HttpStatusCode.OK, "{\"state\":\"assigned\",\"serverPort\":70000}"),
				Json(HttpStatusCode.OK, oversized));
			using var client = new RankedLobbyClient(
				new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));

			Assert.That(async () => await client.GetQueueStatusAsync("access-token", CancellationToken.None),
				Throws.TypeOf<RankedLobbyException>().With.Property("Error").EqualTo(RankedLobbyError.InvalidResponse));
			Assert.That(async () => await client.GetLeaderboardAsync(
				"11111111-1111-1111-1111-111111111111", 100, CancellationToken.None),
				Throws.TypeOf<RankedLobbyException>().With.Property("Error").EqualTo(RankedLobbyError.InvalidResponse));
		}

		[Test]
		public async Task LeaderboardUsesBackendSeasonQueryName()
		{
			const string seasonId = "11111111-1111-1111-1111-111111111111";
			var handler = new RecordingHandler(Json(HttpStatusCode.OK,
				"{\"season\":{\"id\":\"11111111-1111-1111-1111-111111111111\",\"name\":\"Season One\",\"status\":\"active\",\"startsUtc\":\"2026-10-01T00:00:00Z\",\"endsUtc\":\"2026-11-01T00:00:00Z\",\"rulesVersion\":1},\"players\":[]}"));
			using var client = new RankedLobbyClient(
				new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));

			await client.GetLeaderboardAsync(seasonId, 100, CancellationToken.None);

			Assert.That(handler.Paths[0], Is.EqualTo(
				"/v1/ranked/seasons/11111111-1111-1111-1111-111111111111/leaderboard?limit=100"));
		}

		[Test]
		public async Task PersonalResultComesFromAuthenticatedRatingEvents()
		{
			const string matchId = "22222222-2222-2222-2222-222222222222";
			var handler = new RecordingHandler(
				Json(HttpStatusCode.OK,
					$"{{\"events\":[{{\"seasonId\":\"11111111-1111-1111-1111-111111111111\",\"sequence\":7,\"eventType\":\"match\",\"matchId\":\"{matchId}\",\"algorithmVersion\":1,\"ratingBefore\":1500,\"ratingAfter\":1512,\"ratingDelta\":12,\"occurredUtc\":\"2026-10-02T12:12:00Z\",\"outcome\":\"win\"}}]}}"),
				Json(HttpStatusCode.OK,
					$"{{\"id\":\"{matchId}\",\"seasonId\":\"11111111-1111-1111-1111-111111111111\",\"state\":\"SETTLED\",\"map\":\"ranked-map-uid\",\"startedUtc\":\"2026-10-02T12:00:00Z\",\"endedUtc\":\"2026-10-02T12:12:00Z\",\"voidReason\":null,\"players\":[{{\"accountId\":\"33333333-3333-3333-3333-333333333333\",\"username\":\"Alpha\",\"outcome\":\"win\",\"ratingBefore\":1500,\"ratingAfter\":1512,\"ratingDelta\":12}},{{\"accountId\":\"44444444-4444-4444-4444-444444444444\",\"username\":\"Bravo\",\"outcome\":\"loss\",\"ratingBefore\":1500,\"ratingAfter\":1488,\"ratingDelta\":-12}}]}}"));
			using var client = new RankedLobbyClient(
				new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));

			var events = await client.GetRatingEventsAsync("access-token", 10, CancellationToken.None);
			var match = await client.GetPublicMatchAsync(matchId, CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(events.Events[0].RatingDelta, Is.EqualTo(12));
				Assert.That(events.Events[0].Outcome, Is.EqualTo("win"));
				Assert.That(match.State, Is.EqualTo("SETTLED"));
				Assert.That(handler.Paths, Is.EqualTo(new[]
				{
					"/v1/ranked/me/rating-events?limit=10",
					$"/v1/ranked/matches/{matchId}",
				}));
				Assert.That(handler.Authorizations, Is.EqualTo(new[] { "access-token", "" }));
			});
		}

		[Test]
		public void RsaSha256ProofDoesNotChangeLegacySha1Signature()
		{
			using var rsa = RSA.Create(2048);
			var parameters = rsa.ExportParameters(true);
			const string message = "nukehour-ranked-device-v1\naccount\nnonce";

			var sha256Signature = CryptoUtil.SignSha256(parameters, message);
			var legacySignature = CryptoUtil.Sign(parameters, message);

			Assert.Multiple(() =>
			{
				Assert.That(rsa.VerifyData(
					Encoding.UTF8.GetBytes(message), Convert.FromBase64String(sha256Signature),
					HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1), Is.True);
				Assert.That(sha256Signature, Is.Not.EqualTo(legacySignature));
				Assert.That(CryptoUtil.VerifySignature(parameters, message, legacySignature), Is.True);
			});
		}
	}
}
