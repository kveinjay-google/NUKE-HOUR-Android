#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class FogOverlayPreferenceTest
	{
		static string EngineRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "OpenRA.Game")))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the engine root.");
		}

		[Test]
		public void ExploredFogOverlayIsAnImmediateCosmeticPreference()
		{
			var field = typeof(GraphicSettings).GetField("ShowFogOverlay");
			Assert.That(field, Is.Not.Null, "Graphics settings must persist the visual fog preference.");
			Assert.That(field?.GetValue(new GraphicSettings()), Is.EqualTo(true),
				"Classic explored-area fog remains enabled by default.");

			var root = EngineRoot();
			var renderer = File.ReadAllText(Path.Combine(
				root, "OpenRA.Mods.Common", "Traits", "World", "ShroudRenderer.cs"));
			var logic = File.ReadAllText(Path.Combine(
				root, "OpenRA.Mods.Common", "Widgets", "Logic", "Settings", "DisplaySettingsLogic.cs"));
			var layout = File.ReadAllText(Path.Combine(root, "mods", "common", "chrome", "settings-display.yaml"));
			var english = File.ReadAllText(Path.Combine(root, "mods", "common", "fluent", "chrome.ftl"));
			var chinese = File.ReadAllText(Path.Combine(root, "mods", "common", "fluent", "zh-CN", "chrome.ftl"));

			Assert.Multiple(() =>
			{
				StringAssert.Contains("if (Game.Settings.Graphics.ShowFogOverlay)", renderer);
				StringAssert.Contains("fogLayer.Draw(wr.Viewport);", renderer);
				StringAssert.Contains("shroudLayer.Draw(wr.Viewport);", renderer,
					"Unexplored shroud must remain rendered when the cosmetic fog layer is disabled.");
				StringAssert.Contains("SettingsUtils.BindCheckboxPref(panel, \"FOG_OVERLAY_CHECKBOX\", ds, \"ShowFogOverlay\");", logic);
				StringAssert.Contains("ds.ShowFogOverlay = dds.ShowFogOverlay;", logic);
				StringAssert.Contains("Checkbox@FOG_OVERLAY_CHECKBOX:", layout);
				StringAssert.Contains("checkbox-fog-overlay-container", english);
				StringAssert.Contains("checkbox-fog-overlay-container", chinese);
			});
		}
	}
}
