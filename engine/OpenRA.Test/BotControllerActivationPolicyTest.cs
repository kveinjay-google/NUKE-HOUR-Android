#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class BotControllerActivationPolicyTest
	{
		[TestCase(false, true, 7, 7, false, false)]
		[TestCase(true, true, 7, 7, false, true)]
		[TestCase(true, true, 7, 9, true, false)]
		[TestCase(true, false, 7, 9, true, true)]
		[TestCase(true, false, 7, 7, false, false)]
		public void OnlyTheDesignatedMultiplayerClientActivatesLobbyBots(
			bool isBot, bool hasLobbyClient, int localClientId, int controllerClientId,
			bool isHost, bool expected)
		{
			var policyType = typeof(Player).Assembly.GetType("OpenRA.BotControllerActivationPolicy");
			Assert.That(policyType, Is.Not.Null,
				"Bot activation must follow the controller identity that the server uses to validate bot orders.");
			if (policyType == null)
				return;

			var shouldActivate = policyType.GetMethod(
				"ShouldActivate", BindingFlags.Public | BindingFlags.Static);
			Assert.That(shouldActivate, Is.Not.Null);
			if (shouldActivate == null)
				return;

			Assert.That(shouldActivate.Invoke(null,
				new object[] { isBot, hasLobbyClient, localClientId, controllerClientId, isHost }),
				Is.EqualTo(expected));
		}

		[Test]
		public void PlayerConstructionUsesTheSharedBotControllerPolicy()
		{
			var source = System.IO.File.ReadAllText(System.IO.Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Player.cs"));

			StringAssert.Contains("BotControllerActivationPolicy.ShouldActivate", source);
			StringAssert.Contains("client?.BotControllerClientIndex", source);
		}

		[Test]
		public void SingleHumanLobbyRepairsEveryStaleBotControllerToTheAdmin()
		{
			var lobby = new Session();
			lobby.Clients.Add(new Session.Client
			{
				Index = 7,
				IsAdmin = true,
				State = Session.ClientState.Ready
			});

			for (var i = 0; i < 5; i++)
				lobby.Clients.Add(new Session.Client
				{
					Index = 20 + i,
					Bot = "test",
					BotControllerClientIndex = 0,
					State = Session.ClientState.NotReady
				});

			Assert.That(lobby.RepairBotControllerAssignments(), Is.EqualTo(5));
			Assert.That(lobby.Clients.Where(c => c.IsBot).Select(c => c.BotControllerClientIndex),
				Is.All.EqualTo(7));
		}

		[Test]
		public void ValidRemoteBotControllerIsPreserved()
		{
			var lobby = new Session();
			lobby.Clients.Add(new Session.Client
			{
				Index = 7,
				IsAdmin = true,
				State = Session.ClientState.Ready
			});
			lobby.Clients.Add(new Session.Client
			{
				Index = 9,
				State = Session.ClientState.Ready
			});
			lobby.Clients.Add(new Session.Client
			{
				Index = 20,
				Bot = "test",
				BotControllerClientIndex = 9,
				State = Session.ClientState.NotReady
			});

			Assert.That(lobby.RepairBotControllerAssignments(), Is.Zero);
			Assert.That(lobby.ClientWithIndex(20).BotControllerClientIndex, Is.EqualTo(9));
		}

		[Test]
		public void ServerRepairsControllerAssignmentsBeforeCreatingPlayers()
		{
			var source = System.IO.File.ReadAllText(System.IO.Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Server", "Server.cs"));
			var repair = source.IndexOf("RepairBotControllerAssignments()", System.StringComparison.Ordinal);
			var createPlayers = source.IndexOf("cmpi.CreateServerPlayers", System.StringComparison.Ordinal);

			Assert.That(repair, Is.GreaterThanOrEqualTo(0));
			Assert.That(repair, Is.LessThan(createPlayers),
				"The canonical controller must be synchronized before Player activates its bot module.");
		}

		[Test]
		public void SlotBotNormalizesTheRequestedControllerBeforeStoringIt()
		{
			var source = System.IO.File.ReadAllText(System.IO.Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "ServerTraits", "LobbyCommands.cs"));

			StringAssert.Contains("ResolveBotControllerClientIndex(controllerClientIndex)", source);
			StringAssert.Contains("bot.BotControllerClientIndex = controllerClientIndex;", source);
		}

		static string RepositoryRoot()
		{
			var directory = new System.IO.DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !System.IO.Directory.Exists(
				System.IO.Path.Combine(directory.FullName, "engine", "OpenRA.Game")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}
	}
}
