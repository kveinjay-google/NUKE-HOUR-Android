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

using System.IO;
using System.Text.Json;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class HdProductionCameoCoverageTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[Test]
		public void SyntheticSequencesCoverTheCompleteReviewedCatalog()
		{
			var root = RepositoryRoot();
			var catalogPath = Path.Combine(root, "artsrc", "production-cameos-hd", "catalog.json");
			var sequencePath = Path.Combine(root, "mods", "ra2", "sequences", "production-cameos-hd.yaml");
			using var catalog = JsonDocument.Parse(File.ReadAllText(catalogPath));
			var sequences = File.ReadAllText(sequencePath);
			var actors = catalog.RootElement.GetProperty("actors");
			var powers = catalog.RootElement.GetProperty("support_powers");
			Assert.That(actors.GetArrayLength(), Is.EqualTo(118));
			Assert.That(powers.GetArrayLength(), Is.EqualTo(9));

			foreach (var actor in actors.EnumerateArray())
			{
				var key = actor.GetProperty("key").GetString();
				var runtime = actor.GetProperty("runtime_path").GetString()!["mods/ra2/".Length..];
				StringAssert.Contains($"hd-production-{key}:\n", sequences);
				StringAssert.Contains($"Filename: {runtime}", sequences);
			}

			foreach (var power in powers.EnumerateArray())
			{
				var key = ProductionIconPresentation.SupportImage(
					power.GetProperty("owner_actor").GetString()!,
					power.GetProperty("order_name").GetString()!);
				var runtime = power.GetProperty("runtime_path").GetString()!["mods/ra2/".Length..];
				StringAssert.Contains($"{key}:\n", sequences);
				StringAssert.Contains($"Filename: {runtime}", sequences);
			}

			var mod = File.ReadAllText(Path.Combine(root, "mods", "ra2", "mod.yaml"));
			StringAssert.Contains("ra2|sequences/production-cameos-hd.yaml", mod);
		}

		[Test]
		public void CategoryAndScrollControlsKeepOriginalFactionChrome()
		{
			var root = RepositoryRoot();
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));
			var layout = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ingame-player.yaml"));
			foreach (var key in new[] { "building", "support", "infantry", "vehicle", "aircraft", "ship" })
			{
				StringAssert.DoesNotContain($"production-icon-hd-{key}:\n", chrome);
				StringAssert.DoesNotContain($"ImageCollection: production-icon-hd-{key}", layout);
			}

			foreach (var key in new[] { "scroll-up", "scroll-down" })
			{
				StringAssert.Contains($"production-icon-hd-{key}:\n", chrome);
				StringAssert.Contains($"Image: uibits/production-cameos-hd/ui/{key}.png", chrome);
				StringAssert.DoesNotContain($"ImageCollection: production-icon-hd-{key}", layout,
					"HD cameos must not restore the rejected gold sidebar scroll buttons.");
			}

			StringAssert.Contains("Background: scrollup-buttons", layout);
			StringAssert.Contains("Background: scrolldown-buttons", layout);
		}
	}
}
