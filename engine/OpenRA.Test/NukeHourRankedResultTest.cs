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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.Network;
using OpenRA.Server;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NukeHourRankedResultTest
	{
		static RankedServerAssignment Assignment() => new()
		{
			MatchId = "11111111-1111-1111-1111-111111111111",
			SeasonId = "22222222-2222-2222-2222-222222222222",
			Map = "ranked-map-uid",
			RuntimeContract = "1|ra2-required-v1|" + new string('a', 64),
			EngineCompatibility = "release-20250330",
			HandshakeSchemaVersion = 1,
			OrdersVersion = 23,
			ModVersion = "1.0",
			RulesVersion = 1,
			LeaseExpiresUtc = new DateTime(2026, 10, 2, 12, 1, 0, DateTimeKind.Utc),
			Participants = new[]
			{
				new RankedAssignmentParticipant
				{
					AccountId = "33333333-3333-3333-3333-333333333333",
					Username = "Alpha",
					DeviceFingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
					Slot = 0,
				},
				new RankedAssignmentParticipant
				{
					AccountId = "44444444-4444-4444-4444-444444444444",
					Username = "Bravo",
					DeviceFingerprint = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
					Slot = 1,
				},
			},
		};

		static GameInformation CompletedGame()
		{
			var game = new GameInformation
			{
				MapUid = "ranked-map-uid",
				StartTimeUtc = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc),
				EndTimeUtc = new DateTime(2026, 10, 2, 12, 12, 0, DateTimeKind.Utc),
				FinalGameTick = 18000,
			};
			game.Players.Add(new GameInformation.Player
			{
				Name = "Untrusted client name",
				Fingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
				IsHuman = true,
				Outcome = WinState.Won,
			});
			game.Players.Add(new GameInformation.Player
			{
				Name = "Another client name",
				Fingerprint = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
				IsHuman = true,
				Outcome = WinState.Lost,
				DisconnectFrame = 17500,
			});
			return game;
		}

		[Test]
		public void ResultFactoryUsesOnlyAssignmentIdentityAndServerGameInformation()
		{
			var pending = RankedMatchResultFactory.Create(Assignment(), CompletedGame(), false);

			Assert.Multiple(() =>
			{
				Assert.That(pending.Kind, Is.EqualTo(RankedSubmissionKind.Result));
				Assert.That(pending.Result.ResultRevision, Is.EqualTo(1));
				Assert.That(pending.Result.Outcomes.Select(outcome => outcome.AccountId),
					Is.EquivalentTo(new[]
					{
						"33333333-3333-3333-3333-333333333333",
						"44444444-4444-4444-4444-444444444444",
					}));
				Assert.That(pending.Result.Outcomes.Single(outcome => outcome.Outcome == "win").AccountId,
					Is.EqualTo("33333333-3333-3333-3333-333333333333"));
				Assert.That(pending.Result.Disconnects.Single().AccountId,
					Is.EqualTo("44444444-4444-4444-4444-444444444444"));
				Assert.That(pending.Result.Map, Is.EqualTo("ranked-map-uid"));
			});
		}

		[Test]
		public void DesyncOrMissingOutcomeProducesVoidInsteadOfRatingResult()
		{
			var desync = RankedMatchResultFactory.Create(Assignment(), CompletedGame(), true);
			var incompleteGame = CompletedGame();
			incompleteGame.Players[0].Outcome = WinState.Undefined;
			var incomplete = RankedMatchResultFactory.Create(Assignment(), incompleteGame, false);

			Assert.Multiple(() =>
			{
				Assert.That(desync.Kind, Is.EqualTo(RankedSubmissionKind.Void));
				Assert.That(desync.VoidReason, Is.EqualTo("desync"));
				Assert.That(incomplete.Kind, Is.EqualTo(RankedSubmissionKind.Void));
				Assert.That(incomplete.VoidReason, Is.EqualTo("missing_outcome"));
			});
		}

		[Test]
		public void AmbiguousAuthoritativeIdentityProducesVoidInsteadOfCrashing()
		{
			var game = CompletedGame();
			game.Players.Add(new GameInformation.Player
			{
				Name = "Duplicate fingerprint",
				Fingerprint = game.Players[0].Fingerprint,
				IsHuman = true,
				Outcome = WinState.Won,
			});

			var pending = RankedMatchResultFactory.Create(Assignment(), game, false);

			Assert.Multiple(() =>
			{
				Assert.That(pending.Kind, Is.EqualTo(RankedSubmissionKind.Void));
				Assert.That(pending.VoidReason, Is.EqualTo("missing_outcome"));
			});
		}

		[Test]
		public async Task PendingSubmissionIsEncryptedAndRoundTripsWithoutPayloadChanges()
		{
			var directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(directory);
			try
			{
				var path = Path.Combine(directory, "pending.bin");
				var key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
				var spool = new RankedResultSpool(path, key);
				var pending = RankedMatchResultFactory.Create(Assignment(), CompletedGame(), false);

				await spool.SaveAsync(pending, CancellationToken.None);
				var bytes = await File.ReadAllBytesAsync(path);
				var restored = await spool.LoadAsync(CancellationToken.None);

				Assert.Multiple(() =>
				{
					Assert.That(Encoding.UTF8.GetString(bytes), Does.Not.Contain(pending.MatchId));
					Assert.That(Encoding.UTF8.GetString(bytes), Does.Not.Contain("accountId"));
					Assert.That(restored.MatchId, Is.EqualTo(pending.MatchId));
					Assert.That(restored.Result.ResultRevision, Is.EqualTo(1));
					Assert.That(restored.Result.Outcomes[0].AccountId,
						Is.EqualTo(pending.Result.Outcomes[0].AccountId));
				});
			}
			finally
			{
				Directory.Delete(directory, true);
			}
		}
	}
}
