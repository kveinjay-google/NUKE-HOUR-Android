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
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NukeHourBrandingTest
	{
		static Settings LoadSettings(string storedName, params string[] arguments)
		{
			var settingsPath = Path.GetTempFileName();
			try
			{
				File.WriteAllText(settingsPath, $"Player:\n\tName: {storedName}\n");
				return new Settings(settingsPath, new Arguments(arguments));
			}
			finally
			{
				File.Delete(settingsPath);
			}
		}

		[Test]
		public void NewPlayersUseTheNukeHourName()
		{
			Assert.That(new PlayerSettings().Name, Is.EqualTo("NUKE HOUR"));
		}

		[Test]
		public void DefaultNameIsNotExposedToCommandLineFieldDiscovery()
		{
			Assert.That(typeof(PlayerSettings).GetField("DefaultName"), Is.Null);
		}

		[TestCase("Commander")]
		[TestCase("指挥官")]
		public void GeneratedLegacyNamesMigrateToNukeHour(string legacyName)
		{
			Assert.That(Settings.MigrateLegacyPlayerName(legacyName), Is.EqualTo("NUKE HOUR"));
		}

		[Test]
		public void CustomPlayerNamesArePreserved()
		{
			Assert.That(Settings.MigrateLegacyPlayerName("Field Marshal"), Is.EqualTo("Field Marshal"));
		}

		[TestCase("Commander", "NUKE HOUR")]
		[TestCase("指挥官", "NUKE HOUR")]
		[TestCase("Field Marshal", "Field Marshal")]
		public void StoredPlayerNamesMigrateAfterSettingsLoad(string storedName, string expectedName)
		{
			var settings = LoadSettings(storedName);

			Assert.That(settings.Player.Name, Is.EqualTo(expectedName));
		}

		[TestCase("Commander", "NUKE HOUR")]
		[TestCase("指挥官", "NUKE HOUR")]
		[TestCase("Field Marshal", "Field Marshal")]
		public void CommandLinePlayerNamesMigrateAfterOverrides(string commandLineName, string expectedName)
		{
			var settings = LoadSettings("Saved Player", $"Player.Name={commandLineName}");

			Assert.That(settings.Player.Name, Is.EqualTo(expectedName));
		}

		[Test]
		public void LocalSkirmishServerUsesTheTranslatedModTitle()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Game.cs"));

			Assert.That(source, Does.Contain("Name = ModData.Manifest.Metadata.TitleTranslated"));
			Assert.That(source, Does.Not.Contain("Name = \"Skirmish Game\""));
		}

		[Test]
		public void Ra2MainMenuDefinesTheAboutEntryAndPanel()
		{
			var yaml = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "mods", "ra2", "chrome", "mainmenu.yaml"));
			var modYaml = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "mods", "ra2", "mod.yaml"));
			var aboutButton = yaml.IndexOf("Button@ABOUT_BUTTON:", StringComparison.Ordinal);
			var quitButton = yaml.IndexOf("Button@QUIT_BUTTON:", StringComparison.Ordinal);
			var aboutPanel = yaml.IndexOf("StretchBackground@ABOUT_PANEL:", StringComparison.Ordinal);

			Assert.That(aboutButton, Is.GreaterThanOrEqualTo(0));
			Assert.That(quitButton, Is.GreaterThan(aboutButton));
			Assert.That(yaml, Does.Contain("Text: button-main-menu-about"));
			Assert.That(aboutPanel, Is.GreaterThanOrEqualTo(0));

			var panel = yaml.Substring(aboutPanel);
			Assert.That(panel, Does.Contain("Width: WINDOW_WIDTH"));
			Assert.That(panel, Does.Contain("Height: WINDOW_HEIGHT"));
			Assert.That(panel, Does.Contain("Background: cc-about-panel"));
			Assert.That(panel, Does.Contain("PreserveAspectRatio: false"));
			Assert.That(panel, Does.Contain("Label@ABOUT_TITLE:"));
			Assert.That(panel, Does.Contain("Font: SettingsTitle"));
			Assert.That(panel, Does.Contain("Text: label-nuke-hour-about-title"));
			Assert.That(panel, Does.Contain("Label@ABOUT_DESCRIPTION:"));
			Assert.That(panel, Does.Contain("Font: SettingsRegular"));
			Assert.That(panel, Does.Not.Contain("Font: Medium\n"));
			Assert.That(panel, Does.Contain("WordWrap: true"));
			Assert.That(panel, Does.Contain("Text: label-nuke-hour-about-description"));
			Assert.That(panel, Does.Contain("Label@VERSION_CAPTION:"));
			Assert.That(panel, Does.Contain("Label@VERSION_VALUE:"));
			Assert.That(panel, Does.Contain("Logic: VersionLabelLogic"));
			Assert.That(panel, Does.Contain("Text: label-nuke-hour-about-version"));
			Assert.That(panel, Does.Contain("Button@WEBSITE_BUTTON:"));
			Assert.That(panel, Does.Contain("Text: button-nuke-hour-about-website"));
			Assert.That(panel, Does.Contain("Button@BACK_BUTTON:"));
			Assert.That(panel, Does.Contain("Width: (PARENT_WIDTH - 116) / 2"));
			foreach (Match expression in Regex.Matches(panel, @"(?m)^\s*(?:X|Y|Width|Height): ([^\r\n]+)"))
				Assert.DoesNotThrow(() => new OpenRA.Support.IntegerExpression(expression.Groups[1].Value),
					"About layout must use the actual engine expression grammar.");
			Assert.That(panel, Does.Contain("Height: 56"));
			Assert.That(panel, Does.Contain("Text: button-back"));
			Assert.That(panel, Does.Contain("Key: escape"));
			Assert.That(panel, Does.Not.Contain("Logic: NukeHourAboutLogic"));

			var fontsStart = modYaml.IndexOf("Fonts:\n", StringComparison.Ordinal);
			var fontsEnd = modYaml.IndexOf("\nDefaultOrderGenerator:", fontsStart, StringComparison.Ordinal);
			Assert.That(fontsStart, Is.GreaterThanOrEqualTo(0));
			Assert.That(fontsEnd, Is.GreaterThan(fontsStart));
			var declaredFonts = Regex.Matches(modYaml.Substring(fontsStart, fontsEnd - fontsStart),
				@"(?m)^\t(?<name>[A-Za-z][A-Za-z0-9]*):\s*$")
				.Select(match => match.Groups["name"].Value).ToArray();
			var aboutFonts = Regex.Matches(panel, @"(?m)^[ \t]*Font:[ \t]*(?<name>\S+)[ \t]*$")
				.Select(match => match.Groups["name"].Value).Distinct().ToArray();
			Assert.That(aboutFonts, Is.Not.Empty);
			Assert.That(aboutFonts, Is.SubsetOf(declaredFonts),
				$"About uses fonts not declared by the RA2 manifest: {string.Join(", ", aboutFonts.Except(declaredFonts))}");
		}

		[Test]
		public void MainMenuLogicExposesAboutAndExitOnDesktopAndMobile()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "MainMenuLogic.cs"));

			Assert.That(source, Does.Contain("GetOrNull<ButtonWidget>(\"ABOUT_BUTTON\")"));
			Assert.That(source, Does.Contain("Platform.CurrentPlatform == PlatformType.OSX || Platform.UsesMobileLayout"));
			Assert.That(source, Does.Contain("aboutButton.Visible = showAboutButton;"));
			Assert.That(source, Does.Contain("aboutButton.OnClick = OpenAboutPanel;"));
			Assert.That(source, Does.Contain("controls.Add(aboutButton);"));
			Assert.That(source.IndexOf("controls.Add(aboutButton);", StringComparison.Ordinal),
				Is.LessThan(source.IndexOf("controls.Add(quitButton);", StringComparison.Ordinal)));
			Assert.That(source, Does.Contain("quitButton.Bounds.Y = aboutButton.Bounds.Y;"));
			Assert.That(source, Does.Contain("quitButton.Visible = true"));
			Assert.That(source, Does.Contain("mainMenuControls.Length >= 4"));

			var methodStart = source.IndexOf("void OpenAboutPanel()", StringComparison.Ordinal);
			Assert.That(methodStart, Is.GreaterThanOrEqualTo(0));
			var methodEnd = source.IndexOf("\n\t\tvoid ", methodStart + 1, StringComparison.Ordinal);
			Assert.That(methodEnd, Is.GreaterThan(methodStart));
			var method = source.Substring(methodStart, methodEnd - methodStart);
			var openWindow = method.IndexOf("var aboutPanel = Game.OpenWindow(\"ABOUT_PANEL\"", StringComparison.Ordinal);
			var hideMenu = method.IndexOf("SwitchMenu(MenuType.None);", StringComparison.Ordinal);
			Assert.That(method, Does.Contain("SwitchMenu(MenuType.None);"));
			Assert.That(method, Does.Contain("Game.OpenWindow(\"ABOUT_PANEL\""));
			Assert.That(openWindow, Is.GreaterThanOrEqualTo(0));
			Assert.That(hideMenu, Is.GreaterThan(openWindow),
				"Keep the main menu visible until the About window has opened successfully.");
			Assert.That(method, Does.Contain("Get<ButtonWidget>(\"BACK_BUTTON\").OnClick"));
			Assert.That(method, Does.Contain("Ui.CloseWindow();"));
			Assert.That(method, Does.Contain("SwitchMenu(MenuType.Main);"));
		}

		[Test]
		public void EnglishAboutCopyDescribesTheMobileMission()
		{
			AssertFluentLines("mods", "ra2", "fluent", "mod.ftl", new[]
			{
				"button-main-menu-about = About",
				"label-nuke-hour-about-title = NUKE HOUR",
				"label-nuke-hour-about-description = Play your legitimately owned PC games on mobile devices.",
				"label-nuke-hour-about-version = Version",
				"label-nuke-hour-about-website = Official website",
				"label-nuke-hour-about-url = https://nukehour.com"
			});
		}

		[Test]
		public void SimplifiedChineseAboutCopyDescribesTheMobileMission()
		{
			AssertFluentLines("mods", "ra2", "fluent", "zh-CN", "mod.ftl", new[]
			{
				"button-main-menu-about = 关于程序",
				"label-nuke-hour-about-title = NUKE HOUR",
				"label-nuke-hour-about-description = 在移动设备上运行你的正版 PC 游戏",
				"label-nuke-hour-about-version = 程序版本",
				"label-nuke-hour-about-website = 官方网站",
				"label-nuke-hour-about-url = https://nukehour.com"
			});
		}

		static void AssertFluentLines(string first, string second, string third, string fourth, string[] expected)
		{
			var lines = File.ReadAllLines(Path.Combine(RepositoryRoot(), first, second, third, fourth));
			foreach (var line in expected)
				Assert.That(lines, Does.Contain(line));
		}

		static void AssertFluentLines(
			string first, string second, string third, string fourth, string fifth, string[] expected)
		{
			var lines = File.ReadAllLines(Path.Combine(RepositoryRoot(), first, second, third, fourth, fifth));
			foreach (var line in expected)
				Assert.That(lines, Does.Contain(line));
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
