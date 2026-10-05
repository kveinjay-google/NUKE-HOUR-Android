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
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IntroductionPromptLayoutTest
	{
		[Test]
		public void NukeHourDesktopPromptUsesRestrainedResponsiveChromeAndMobilePromptUsesTouchLayout()
		{
			var root = RepositoryRoot();
			var logic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "IntroductionPromptLogic.cs"));
			var promptChrome = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "chrome", "mainmenu-prompts.yaml"));
			var chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));

			StringAssert.Contains("var nukeHourDesktop = ApplyNukeHourDesktopPresentation(widget, modData);", logic);
			StringAssert.Contains("if (Platform.UsesMobileLayout || modData.Manifest.Id != \"ra2\")", logic);
			StringAssert.Contains("prompt.Background = \"cc-introduction-clean-background\";", logic);
			StringAssert.Contains("var panel = widget.Get<BackgroundWidget>(\"NUKE_HOUR_DESKTOP_PANEL\");", logic);
			StringAssert.Contains("panel.Background = \"settings-v2-panel\";", logic);
			StringAssert.Contains("Background@NUKE_HOUR_DESKTOP_PANEL:", promptChrome);
			StringAssert.Contains("Visible: false", promptChrome);
			StringAssert.Contains("const int MaximumWidth = 860;", logic);
			StringAssert.Contains("const int MaximumHeight = 520;", logic);
			StringAssert.Contains("title.Align = TextAlign.Left;", logic);
			StringAssert.Contains("descriptionB.IsVisible = () => false;", logic);
			StringAssert.Contains("mouseControlDescClassic.IsVisible = () => !nukeHourDesktop", logic);
			StringAssert.Contains("mouseControlDescModern.IsVisible = () => !nukeHourDesktop", logic);
			StringAssert.Contains("header.Background = \"cc-mp-surface\";", logic);
			StringAssert.Contains("nameTextfield.Background = \"cc-mp-field\";", logic);
			StringAssert.Contains("dropDown.Background = \"cc-mp-field\";", logic);
			StringAssert.Contains("dropDown.ShowSeparator = false;", logic);
			StringAssert.Contains("checkbox.Background = \"cc-mp-field\";", logic);
			StringAssert.Contains("continueButton.Background = \"cc-mp-control-pressed\";", logic);
			StringAssert.Contains("continueButton.Bounds = new WidgetBounds(panelX + Inset, panelY + promptHeight - 64, promptWidth - 2 * Inset, 42);", logic);
			StringAssert.Contains("if (!nukeHourDesktop && !nukeHourMobile)", logic);
			StringAssert.Contains("SettingsUtils.AdjustSettingsScrollPanelLayout", logic);
			StringAssert.Contains("cc-mp-field:", chrome);
			StringAssert.Contains("cc-mp-field-focused:", chrome);
			StringAssert.Contains("cc-mp-surface:", chrome);
			StringAssert.Contains("cc-introduction-clean-background:", chrome);
		}

		static string RepositoryRoot()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (root != null &&
				!Directory.Exists(Path.Combine(root, ".git")) &&
				!File.Exists(Path.Combine(root, ".git")))
				root = Directory.GetParent(root)?.FullName;

			Assert.That(root, Is.Not.Null, "Could not locate repository root.");
			return root!;
		}
	}
}
