#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License,
 * version 3 or any later version. For more information, see COPYING.
 */
#endregion

using System.IO;
using NUnit.Framework;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class LobbyOptionCompatibilityTest
	{
		[Test]
		public void MissingRemoteOptionUsesALockedLocalDefault()
		{
			var global = new Session.Global();
			var state = global.OptionStateOrDefault("population-player", "0");

			Assert.Multiple(() =>
			{
				Assert.That(state.Value, Is.EqualTo("0"));
				Assert.That(state.PreferredValue, Is.EqualTo("0"));
				Assert.That(state.IsLocked, Is.True);
				Assert.That(global.LobbyOptions, Does.Not.ContainKey("population-player"));
			});
		}

		[Test]
		public void ExistingRemoteOptionKeepsTheServerState()
		{
			var global = new Session.Global();
			var expected = new Session.LobbyOptionState
			{
				Value = "300",
				PreferredValue = "500",
				IsLocked = false
			};
			global.LobbyOptions.Add("population-player", expected);

			Assert.That(global.OptionStateOrDefault("population-player", "0"), Is.SameAs(expected));
		}

		[Test]
		public void LobbyConsumersDoNotDirectlyIndexPotentiallyMissingOptionIds()
		{
			var root = RepositoryRoot();
			foreach (var relativePath in new[]
			{
				"engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyOptionsLogic.cs",
				"engine/OpenRA.Mods.Common/Widgets/Logic/Lobby/LobbyLogic.cs",
				"engine/OpenRA.Mods.Common/ServerTraits/LobbyCommands.cs",
				"engine/OpenRA.Mods.Common/ServerTraits/SkirmishLogic.cs"
			})
			{
				var source = File.ReadAllText(Path.Combine(root, relativePath));
				StringAssert.DoesNotContain("LobbyOptions[option.Id]", source, relativePath);
				StringAssert.DoesNotContain("LobbyOptions[o.Id].Value", source, relativePath);
			}
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "engine")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null);
			return directory!.FullName;
		}
	}
}
