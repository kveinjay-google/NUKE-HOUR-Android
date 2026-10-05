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
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class PreReleaseWarningPromptTest
	{
		[Test]
		public void DeveloperPreviewDialogAndContinueButtonAreFingerSizedOnPhones()
		{
			var root = RepositoryRoot();
			var paths = new[]
			{
				Path.Combine(root, "mods", "ra2", "chrome", "mainmenu-prerelease-notification.yaml"),
				Path.Combine(root, "engine", "mods", "ra2", "chrome", "mainmenu-prerelease-notification.yaml")
			};

			foreach (var path in paths)
			{
				var dialog = MiniYaml.FromFile(path).Single(node =>
					node.Key == "Background@MAINMENU_PRERELEASE_NOTIFICATION");
				var children = dialog.Value.NodeWithKey("Children").Value.Nodes;
				var button = children.Single(node => node.Key == "Button@CONTINUE_BUTTON");
				var phoneLogicalPerPoint = 1558d / 844d;

				Assert.Multiple(() =>
				{
					Assert.That(Integer(dialog, "Width"), Is.GreaterThanOrEqualTo(760));
					Assert.That(Integer(dialog, "Height"), Is.GreaterThanOrEqualTo(320));
					Assert.That(Integer(button, "Width"), Is.GreaterThanOrEqualTo(280));
					Assert.That(Integer(button, "Height") / phoneLogicalPerPoint,
						Is.GreaterThanOrEqualTo(48),
						"The continue button must remain at least 48 physical points tall on phones.");
					Assert.That(button.Value.NodeWithKey("Font").Value.Value,
						Is.EqualTo("BigBold"),
						"The prompt must use a font declared by both release and source manifests.");
				});
			}
		}

		[Test]
		public void StableRuntimeStartsMainMenuDirectlyWithoutReentrantWidgetReset()
		{
			var root = RepositoryRoot();
			var releaseRules = File.ReadAllText(Path.Combine(root, "mods", "ra2", "rules", "world.yaml"));
			var sourceRules = File.ReadAllText(Path.Combine(root, "engine", "mods", "ra2", "rules", "world.yaml"));
			var promptSource = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Cnc", "Widgets",
				"Logic", "PreReleaseWarningPrompt.cs"));

			Assert.Multiple(() =>
			{
				StringAssert.Contains("ShellmapRoot: MAINMENU", releaseRules);
				StringAssert.DoesNotContain("ShellmapRoot: MAINMENU_PRERELEASE_NOTIFICATION", releaseRules,
					"Release builds must not construct a warning widget and replace the UI tree from its constructor.");
				Assert.That(sourceRules, Is.EqualTo(releaseRules),
					"The Android engine mod path resolves the same synchronized runtime rules.");
				StringAssert.DoesNotContain("else\n\t\t\t\tShowMainMenu(world);", promptSource,
					"Prompt construction must never synchronously reset the complete UI tree.");
			});
		}

		static int Integer(MiniYamlNode node, string key)
		{
			return int.Parse(node.Value.NodeWithKey(key).Value.Value, CultureInfo.InvariantCulture);
		}

		static string RepositoryRoot()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (root != null && !Directory.Exists(Path.Combine(root, "engine", "OpenRA.Mods.Cnc")))
				root = Directory.GetParent(root)?.FullName;

			Assert.That(root, Is.Not.Null, "Could not locate repository root.");
			return root!;
		}
	}
}
