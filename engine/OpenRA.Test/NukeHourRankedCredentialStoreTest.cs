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
	public sealed class NukeHourRankedCredentialStoreTest
	{
		sealed class MemoryStore : IRankedCredentialStore
		{
			public bool IsAvailable => true;
			public RankedStoredCredential Credential;
			public int Saves;
			public int Deletes;
			public RankedStoredCredential Load() => Credential;
			public void Save(RankedStoredCredential credential)
			{
				Credential = credential;
				Saves++;
			}
			public void Delete()
			{
				Credential = null;
				Deletes++;
			}
		}

		sealed class ResponseHandler : HttpMessageHandler
		{
			readonly Queue<HttpResponseMessage> responses;
			public readonly List<string> Bodies = new();

			public ResponseHandler(params string[] json)
			{
				responses = new Queue<HttpResponseMessage>();
				foreach (var item in json)
					responses.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
					{
						Content = new StringContent(item, Encoding.UTF8, "application/json")
					});
			}

			protected override async Task<HttpResponseMessage> SendAsync(
				HttpRequestMessage request, CancellationToken cancellationToken)
			{
				Bodies.Add(request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
				return responses.Dequeue();
			}
		}

		static string Session(string access, string refresh, string accessExpiry) =>
			$"{{\"accountId\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\",\"username\":\"Commander\"," +
			$"\"accessToken\":\"{access}\",\"refreshToken\":\"{refresh}\"," +
			$"\"accessExpiresUtc\":\"{accessExpiry}\",\"refreshExpiresUtc\":\"2026-11-01T12:00:00Z\"}}";
		[Test]
		public void UnsupportedCredentialStoreFailsClosed()
		{
			var store = new UnavailableRankedCredentialStore();
			Assert.Multiple(() =>
			{
				Assert.That(store.IsAvailable, Is.False);
				Assert.That(store.Load(), Is.Null);
				Assert.That(() => store.Save(new RankedStoredCredential()), Throws.TypeOf<PlatformNotSupportedException>());
				Assert.That(() => store.Delete(), Throws.Nothing);
			});
		}

		[Test]
		public void StoredCredentialTextNeverContainsRefreshToken()
		{
			var credential = new RankedStoredCredential
			{
				AccountId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
				Username = "Commander",
				RefreshToken = "refresh-token-that-is-long-enough-123",
				RefreshExpiresUtc = DateTime.UtcNow.AddDays(30),
			};

			Assert.That(credential.ToString(), Does.Not.Contain(credential.RefreshToken));
		}

		[Test]
		public async Task SessionManagerPersistsOnlyRotatingRefreshMaterial()
		{
			var handler = new ResponseHandler(
				Session("first-access-token-that-is-long-12345", "first-refresh-token-that-is-long-1234", "2026-10-02T12:00:30Z"),
				Session("second-access-token-that-is-long-1234", "second-refresh-token-that-is-long-123", "2026-10-02T12:10:00Z"));
			var store = new MemoryStore();
			using var client = new RankedLobbyClient(new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));
			var manager = new RankedSessionManager(client, store,
				() => new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc));

			await manager.LoginAsync("Commander", "correct horse battery staple", CancellationToken.None);
			var access = await manager.GetAccessTokenAsync(CancellationToken.None);

			Assert.Multiple(() =>
			{
				Assert.That(access, Is.EqualTo("second-access-token-that-is-long-1234"));
				Assert.That(store.Saves, Is.EqualTo(2));
				Assert.That(store.Credential.RefreshToken, Is.EqualTo("second-refresh-token-that-is-long-123"));
				Assert.That(typeof(RankedStoredCredential).GetProperty("AccessToken"), Is.Null);
				Assert.That(handler.Bodies[1], Does.Contain("first-refresh-token-that-is-long-1234"));
			});
		}

		[Test]
		public async Task ExpiredStoredRefreshCredentialIsDeletedWithoutNetworkUse()
		{
			var handler = new ResponseHandler();
			var store = new MemoryStore
			{
				Credential = new RankedStoredCredential
				{
					AccountId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
					Username = "Commander",
					RefreshToken = "expired-refresh-token-that-is-long-123",
					RefreshExpiresUtc = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
				}
			};
			using var client = new RankedLobbyClient(new HttpClient(handler), new Uri("https://lobby.nukehour.com/"));
			var manager = new RankedSessionManager(client, store,
				() => new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc));

			Assert.That(await manager.RestoreAsync(CancellationToken.None), Is.False);
			Assert.Multiple(() =>
			{
				Assert.That(store.Deletes, Is.EqualTo(1));
				Assert.That(handler.Bodies, Is.Empty);
				Assert.That(manager.IsAuthenticated, Is.False);
			});
		}
	}
}
