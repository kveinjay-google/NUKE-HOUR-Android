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
using System.Threading;
using System.Threading.Tasks;

namespace OpenRA.Network
{
	public sealed class RankedSessionManager
	{
		static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);
		readonly RankedLobbyClient client;
		readonly IRankedCredentialStore credentialStore;
		readonly Func<DateTime> utcNow;
		readonly SemaphoreSlim mutex = new(1, 1);
		RankedSession session;

		public bool IsAuthenticated => session != null;
		public string AccountId => session?.AccountId;
		public string Username => session?.Username;

		public RankedSessionManager(
			RankedLobbyClient client, IRankedCredentialStore credentialStore, Func<DateTime> utcNow = null)
		{
			this.client = client ?? throw new ArgumentNullException(nameof(client));
			this.credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
			this.utcNow = utcNow ?? (() => DateTime.UtcNow);
		}

		void Persist(RankedSession value)
		{
			credentialStore.Save(new RankedStoredCredential
			{
				AccountId = value.AccountId,
				Username = value.Username,
				RefreshToken = value.RefreshToken,
				RefreshExpiresUtc = value.RefreshExpiresUtc,
			});
		}

		async Task SetSessionAsync(Task<RankedSession> operation)
		{
			var value = await operation.ConfigureAwait(false);
			Persist(value);
			session = value;
		}

		public async Task LoginAsync(string username, string password, CancellationToken cancellationToken)
		{
			await mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				await SetSessionAsync(client.LoginAsync(username, password, cancellationToken)).ConfigureAwait(false);
			}
			finally
			{
				mutex.Release();
			}
		}

		public async Task<RankedSession> RegisterAsync(
			string username, string password, CancellationToken cancellationToken)
		{
			await mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				var value = await client.RegisterAsync(username, password, cancellationToken).ConfigureAwait(false);
				Persist(value);
				session = value;
				return value;
			}
			finally
			{
				mutex.Release();
			}
		}

		public async Task<bool> RestoreAsync(CancellationToken cancellationToken)
		{
			await mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				var stored = credentialStore.Load();
				if (stored == null)
					return false;
				if (stored.RefreshExpiresUtc <= utcNow())
				{
					credentialStore.Delete();
					return false;
				}

				try
				{
					await SetSessionAsync(client.RefreshAsync(stored.RefreshToken, cancellationToken)).ConfigureAwait(false);
					return true;
				}
				catch (RankedLobbyException ex) when (ex.Error == RankedLobbyError.Unauthorized)
				{
					credentialStore.Delete();
					session = null;
					return false;
				}
			}
			finally
			{
				mutex.Release();
			}
		}

		public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
		{
			await mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				if (session == null)
					throw new InvalidOperationException("The ranked account is not signed in.");
				if (session.AccessExpiresUtc <= utcNow() + RefreshMargin)
					await SetSessionAsync(client.RefreshAsync(session.RefreshToken, cancellationToken)).ConfigureAwait(false);
				return session.AccessToken;
			}
			finally
			{
				mutex.Release();
			}
		}

		public async Task LogoutAsync(CancellationToken cancellationToken)
		{
			await mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				if (session != null)
					try
					{
						await client.LogoutAsync(session.AccessToken, cancellationToken).ConfigureAwait(false);
					}
					catch (RankedLobbyException)
					{
						// Local logout is authoritative for this installation. The server token expires naturally.
					}
				credentialStore.Delete();
				session = null;
			}
			finally
			{
				mutex.Release();
			}
		}
	}
}
