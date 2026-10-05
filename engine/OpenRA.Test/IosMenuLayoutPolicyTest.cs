#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Platforms.Default;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosMenuLayoutPolicyTest
	{
		[Test]
		public void MainMenuLanguageSelectionSavesBeforeSchedulingAnEffectiveLanguageReload()
		{
			var settings = new GameSettings { Language = LanguageSelectionPolicy.SystemPreference };
			var callbacks = new List<string>();

			var reload = MainMenuLogic.ApplyLanguageSelection(
				settings,
				"zh-Hans",
				LanguageSelectionPolicy.English,
				LanguageSelectionPolicy.SimplifiedChinese,
				() =>
				{
					Assert.That(settings.Language, Is.EqualTo(LanguageSelectionPolicy.SimplifiedChinese));
					callbacks.Add("save");
				},
				() => callbacks.Add("reload"));

			Assert.That(reload, Is.True);
			Assert.That(callbacks, Is.EqualTo(new[] { "save", "reload" }));
		}

		[TestCase("zh-Hans", "System", "zh-CN")]
		[TestCase("zh-Hans", "zh-CN", "System")]
		public void MainMenuLanguageSelectionOnlySavesWhenTheEffectiveLanguageIsUnchanged(
			string systemLanguageTag, string initialPreference, string selectedPreference)
		{
			var settings = new GameSettings { Language = initialPreference };
			var callbacks = new List<string>();

			var reload = MainMenuLogic.ApplyLanguageSelection(
				settings,
				systemLanguageTag,
				LanguageSelectionPolicy.SimplifiedChinese,
				selectedPreference,
				() => callbacks.Add("save"),
				() => callbacks.Add("reload"));

			Assert.That(reload, Is.False);
			Assert.That(settings.Language, Is.EqualTo(selectedPreference));
			Assert.That(callbacks, Is.EqualTo(new[] { "save" }));
		}

		[Test]
		public void IosMainMenuUsesSchemeCCommandRailAndUtilityShelf()
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(1558, 720, 844, 390, 47, 0, 47, 21));
			var layout = IosMainMenuLayout.Create(policy, 7);

			Assert.Multiple(() =>
			{
				Assert.That(layout.Controls, Has.Length.EqualTo(7));
				Assert.That(layout.Controls[0].X, Is.EqualTo(layout.Controls[1].X));
				Assert.That(layout.Controls[1].X, Is.EqualTo(layout.Controls[2].X));
				Assert.That(layout.Controls[0].Bottom, Is.LessThan(layout.Controls[1].Y));
				Assert.That(layout.Controls[1].Bottom, Is.LessThan(layout.Controls[2].Y));
				Assert.That(layout.Controls[3].Y, Is.EqualTo(layout.Controls[4].Y));
				Assert.That(layout.Controls[4].Y, Is.EqualTo(layout.Controls[5].Y));
				Assert.That(layout.Controls[5].Y, Is.EqualTo(layout.Controls[6].Y));
				Assert.That(layout.Situation.X, Is.GreaterThan(layout.Controls[0].Right));
				Assert.That(layout.Brand.X, Is.LessThanOrEqualTo(layout.Controls[0].X));
				Assert.That(layout.Brand.Bottom, Is.LessThanOrEqualTo(layout.Controls[0].Y));
			});
		}

		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21, "Ultrawide")]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20, "Tablet")]
		[TestCase(1366, 768, 1366, 768, 0, 0, 0, 0, "Standard")]
		public void IosMainMenuMatchesTheApprovedSchemeCFullScreenComposition(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom, string expectedProfile)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
					safeLeft, safeTop, safeRight, safeBottom));
			var layout = IosMainMenuLayout.Create(policy, 7);

			Assert.Multiple(() =>
			{
				Assert.That(layout.Profile.ToString(), Is.EqualTo(expectedProfile));
				Assert.That(layout.Panel, Is.EqualTo(policy.ViewportBounds));
				Assert.That(layout.Controls[0].X, Is.LessThan(layout.Panel.Width * 0.09));
				Assert.That(layout.Controls[0].Width, Is.InRange(
					(int)(layout.Panel.Width * 0.24), (int)(layout.Panel.Width * 0.38)));
				Assert.That(layout.Controls[0].Height, Is.GreaterThan(layout.Panel.Height * 0.11));
				Assert.That(layout.Controls[2].Bottom, Is.LessThan(layout.Panel.Height * 0.80));
				Assert.That(layout.Controls[3].Y, Is.GreaterThan(layout.Panel.Height * 0.82));
				Assert.That(layout.Controls[6].Right, Is.GreaterThan(layout.Panel.Width * 0.90));
			});
		}

		[TestCase(1558, 720, 844, 390, 47, 47)]
		[TestCase(2868, 1320, 956, 440, 62, 62)]
		[TestCase(1558, 720, 844, 390, 47, 0)]
		[TestCase(1558, 720, 844, 390, 0, 47)]
		public void PhoneFooterStaysInArtworkSlotsWhenSafeAreaChanges(
			int width, int height, int nativeWidth, int nativeHeight, double left, double right)
		{
			var baseline = IosMainMenuLayout.Create(IosMenuLayoutPolicy.Create(true,
				Snapshot(width, height, nativeWidth, nativeHeight, 0, 0, 0, 21)), 7);
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(width, height, nativeWidth, nativeHeight, left, 0, right, 21));
			var layout = IosMainMenuLayout.Create(policy, 7);
			for (var i = 3; i < 7; i++)
			{
				var expected = baseline.Controls[i];
				var actual = layout.Controls[i];
				Assert.That(actual.X, Is.EqualTo(Math.Max(expected.X, policy.ContentBounds.X)), $"slot {i}");
				Assert.That(actual.Right, Is.EqualTo(Math.Min(expected.Right, policy.ContentBounds.Right)), $"slot {i}");
				Assert.That(actual.Y, Is.EqualTo(expected.Y));
				Assert.That(actual.Width, Is.GreaterThanOrEqualTo(policy.MinimumTarget));
			}
		}

		[TestCase(1920, 1080)]
		[TestCase(1366, 768)]
		public void DesktopFooterUsesTheArtworkSlotsInsteadOfPhoneGeometry(int width, int height)
		{
			var layout = IosMainMenuLayout.Create(IosMenuLayoutPolicy.Create(false, width, height), 7);
			var left = new[] { .031, .277, .507, .737 };
			var widths = new[] { .230, .230, .220, .230 };
			for (var i = 0; i < 4; i++)
			{
				var button = layout.Controls[i + 3];
				Assert.That(button.X, Is.EqualTo((int)Math.Round(width * left[i])));
				Assert.That(button.Width, Is.EqualTo((int)Math.Round(width * widths[i])));
				Assert.That(button.Y, Is.EqualTo((int)Math.Round(height * .878)));
				Assert.That(button.Height, Is.EqualTo((int)Math.Round(height * .082)));
			}
		}

		[Test]
		public void Ra2MainMenuUsesSchemeCCommandRail()
		{
			var yaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "chrome", "mainmenu.yaml"));
			foreach (var id in new[] { "SINGLEPLAYER_BUTTON", "MULTIPLAYER_BUTTON", "CAMPAIGN_BUTTON" })
				Assert.That(yaml, Does.Contain($"AdaptiveRouteButton@{id}:"), id);
			foreach (var id in new[] { "SETTINGS_BUTTON", "ABOUT_BUTTON", "QUIT_BUTTON" })
				Assert.That(yaml, Does.Contain($"Button@{id}:"), id);
			Assert.That(yaml, Does.Not.Contain("Button@CONTENT_BUTTON:"));

			Assert.That(MainMenuControlBody(yaml, "SINGLEPLAYER_BUTTON"), Does.Contain("Background: cc-soviet-hit-target"));
			foreach (var id in new[] { "MULTIPLAYER_BUTTON", "CAMPAIGN_BUTTON" })
				Assert.That(MainMenuControlBody(yaml, id), Does.Contain("Background: cc-soviet-hit-target"), id);
			foreach (var id in new[] { "SETTINGS_BUTTON", "ABOUT_BUTTON", "QUIT_BUTTON" })
				Assert.That(MainMenuControlBody(yaml, id), Does.Contain("Background: cc-soviet-utility"), id);

			Assert.That(yaml, Does.Contain("Background: cc-soviet-shell-standard"));
			Assert.That(yaml, Does.Contain("Background: cc-soviet-shell-tablet"));
			Assert.That(yaml, Does.Contain("Background: cc-soviet-shell-ultrawide"));
			Assert.That(yaml, Does.Not.Contain("Button@LANGUAGE_BUTTON:"));
			Assert.That(yaml, Does.Not.Contain("Button@UI_STYLE_BUTTON:"));
			var settings = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "chrome", "settings-display.yaml"));
			Assert.That(settings, Does.Contain("DropDownButton@LANGUAGE_DROPDOWN:"));
			Assert.That(settings, Does.Contain("Button@UI_STYLE_BUTTON:"));
			Assert.That(yaml, Does.Not.Contain("Background: cc-home-card-"));
		}

		[Test]
		public void IosMainMenuKeepsAllFourFooterActionsVisible()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "MainMenuLogic.cs"));
			Assert.That(source, Does.Contain("Platform.CurrentPlatform == PlatformType.OSX || Platform.UsesMobileLayout"));
			Assert.That(source, Does.Contain("quitButton.Visible = true;"));
		}

		[Test]
		public void SpecialThanksUsesOnlyTheButtonFramesBakedIntoItsArtwork()
		{
			var yaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "chrome", "mainmenu.yaml"));
			yaml = yaml.Substring(yaml.IndexOf("SpecialThanksPanel@SPECIAL_THANKS_PANEL:", StringComparison.Ordinal));
			yaml = yaml.Substring(0, yaml.IndexOf("StretchBackground@ABOUT_PANEL:", StringComparison.Ordinal));
			foreach (var id in new[] { "SUPPORT_BUTTON", "MORE_BUTTON", "BACK_BUTTON" })
				Assert.That(MainMenuControlBody(yaml, id), Does.Contain("Background: \"\""), id);
		}

		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		public void IosMainMenuUsesSchemeCCommandRailAtSupportedSizes(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
					safeLeft, safeTop, safeRight, safeBottom));
			var layout = IosMainMenuLayout.Create(policy, 7);

			Assert.Multiple(() =>
			{
				Assert.That(layout.Controls[0].X, Is.EqualTo(layout.Controls[1].X));
				Assert.That(layout.Controls[0].Height, Is.EqualTo(layout.Controls[1].Height));
				Assert.That(layout.Controls[0].Bottom, Is.LessThan(layout.Controls[1].Y));
				Assert.That(layout.Controls[1].Bottom, Is.LessThan(layout.Controls[2].Y));
				Assert.That(layout.Controls[3].Y, Is.EqualTo(layout.Controls[4].Y));
				Assert.That(layout.Controls[4].Y, Is.EqualTo(layout.Controls[5].Y));
				Assert.That(layout.Controls[5].Y, Is.EqualTo(layout.Controls[6].Y));
				Assert.That(layout.Controls[0].Height, Is.GreaterThanOrEqualTo(policy.MinimumTarget));
				Assert.That(layout.Controls[3].Height, Is.GreaterThanOrEqualTo(policy.MinimumTarget));
				Assert.That(layout.Controls[3].Right, Is.LessThan(layout.Controls[4].X));
				Assert.That(layout.Controls[4].Right, Is.LessThan(layout.Controls[5].X));
				Assert.That(layout.Controls[5].Right, Is.LessThan(layout.Controls[6].X));
			});

			foreach (var control in layout.Controls)
				AssertPhysicalTarget(control, policy);
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1133, 744, 1133, 744, 0, 0, 0, 20)]
		[TestCase(1024, 768, 1024, 768, 0, 0, 0, 20)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		[TestCase(1366, 1024, 1366, 1024, 0, 0, 0, 20)]
		public void IosMainMenuPlacesSevenPhysicalTargetsInsideTheSafeContentWithoutOverlap(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
					safeLeft, safeTop, safeRight, safeBottom));
			var layout = IosMainMenuLayout.Create(policy);

			Assert.That(layout.Panel, Is.EqualTo(policy.ViewportBounds));
			Assert.That(layout.Controls, Has.Length.EqualTo(7));
			for (var i = 0; i < layout.Controls.Length; i++)
			{
				var control = layout.Controls[i];
				AssertPhysicalTarget(control, policy);
				var containingBounds = i < 3 ? policy.ContentBounds : policy.ViewportBounds;
				Assert.That(containingBounds.ToRectangle().Contains(control.ToRectangle()), Is.True);
				for (var j = i + 1; j < layout.Controls.Length; j++)
					Assert.That(control.ToRectangle().IntersectsWith(layout.Controls[j].ToRectangle()), Is.False);
			}
		}

		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		public void IosMainMenuKeepsSchemeCFrameBreathingRoom(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
					safeLeft, safeTop, safeRight, safeBottom));
			var layout = IosMainMenuLayout.Create(policy, 7);
			Assert.Multiple(() =>
			{
				Assert.That(layout.Controls, Has.Length.EqualTo(7));
				Assert.That(layout.Panel.ToRectangle().Contains(layout.Brand.ToRectangle()), Is.True);
				Assert.That(layout.Controls[0].X, Is.GreaterThanOrEqualTo(policy.ContentBounds.X));
				Assert.That(layout.Controls[6].Right, Is.LessThanOrEqualTo(policy.ContentBounds.Right));
				Assert.That(layout.Controls[6].Bottom, Is.LessThanOrEqualTo(layout.Panel.Bottom));
			});
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		public void IosMainMenuKeepsTwentyFourPhysicalPointsBesideItsControls(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
					safeLeft, safeTop, safeRight, safeBottom));
			var layout = IosMainMenuLayout.Create(policy, 7);
			var minimumInset = (int)Math.Ceiling(24 * policy.LogicalPerPoint);

			Assert.Multiple(() =>
			{
				Assert.That(layout.Controls[0].X, Is.GreaterThanOrEqualTo(minimumInset));
				Assert.That(layout.Panel.Width - layout.Controls[6].Right,
					Is.GreaterThanOrEqualTo(minimumInset));
			});
		}

		[TestCase("System", "zh-CN", "简体中文")]
		[TestCase("system", "en", "English")]
		[TestCase("zh-CN", "en", "简体中文")]
		[TestCase("en", "zh-CN", "English")]
		public void IosMainMenuUsesCompactLanguageNames(
			string preference, string uiLanguage, string expected)
		{
			Assert.That(MainMenuLogic.GetIosLanguageDisplayName(preference, uiLanguage), Is.EqualTo(expected));
		}

		[TestCase("SINGLEPLAYER_BUTTON", "IosTitle")]
		[TestCase("MULTIPLAYER_BUTTON", "IosTitle")]
		[TestCase("SETTINGS_BUTTON", "IosTitle")]
		[TestCase("SKIRMISH_BUTTON", "IosTitle")]
		[TestCase("MISSIONS_BUTTON", "IosTitle")]
		[TestCase("LOAD_BUTTON", "IosTitle")]
		[TestCase("LANGUAGE_BUTTON", "IosBold")]
		[TestCase("UI_STYLE_BUTTON", "IosBold")]
		[TestCase("BACK_BUTTON", "IosBold")]
		public void PrimaryCommandCenterRoutesUseTitleTypography(string id, string expected)
		{
			Assert.That(IosTouchMenuLogic.ButtonFont(id, "Bold"), Is.EqualTo(expected));
		}

		[TestCase("System", "zh-CN")]
		[TestCase("system", "zh-CN")]
		[TestCase("zh-CN", "en")]
		[TestCase("en", "System")]
		[TestCase("unsupported", "zh-CN")]
		public void MainMenuLanguageButtonCyclesThroughSupportedPreferences(
			string preference, string expected)
		{
			Assert.That(MainMenuLogic.NextLanguagePreference(preference), Is.EqualTo(expected));
		}

		[Test]
		public void IosMainMenuMeasuredLanguageLabelsFitTheSingleTapButtonOnEveryDevice()
		{
			using var fonts = new ActualIosMenuFonts();
			var labels = new[] { "简体中文", "English" };
			var snapshots = new[]
			{
				Snapshot(1560, 720, 932, 430, 59, 0, 59, 21),
				Snapshot(1558, 720, 844, 390, 47, 0, 47, 21),
				Snapshot(1133, 744, 1133, 744, 0, 0, 0, 20),
				Snapshot(1024, 768, 1024, 768, 0, 0, 0, 20),
				Snapshot(1180, 820, 1180, 820, 0, 0, 0, 20),
				Snapshot(1366, 1024, 1366, 1024, 0, 0, 0, 20),
			};
			foreach (var snapshot in snapshots)
			{
				var policy = IosMenuLayoutPolicy.Create(true, snapshot);
				var button = IosMainMenuLayout.Create(policy).Controls[5];
				var buttonUsableWidth = Math.Max(0, button.Width - 20);

				foreach (var label in labels)
					Assert.That(fonts.Bold.Measure(label).X, Is.LessThanOrEqualTo(buttonUsableWidth),
						$"{snapshot.NativePointSize}: main-menu button truncates {label}.");

				Assert.That(button.Width, Is.GreaterThanOrEqualTo(policy.MinimumTarget));
			}
		}

		[Test]
		public void MainMenuLogicOwnsIosRelayoutAndKeepsTheLanguageControlOptional()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "MainMenuLogic.cs"));

			Assert.That(source, Does.Contain("GetOrNull<ButtonWidget>(\"LANGUAGE_BUTTON\")"));
			Assert.That(source, Does.Contain("GetOrNull<ButtonWidget>(\"UI_STYLE_BUTTON\")"));
			Assert.That(source, Does.Contain("NextLanguagePreference(Game.Settings.Game.Language)"));
			Assert.That(source, Does.Not.Contain("ShowLanguageDropdown"));
			Assert.That(source, Does.Contain("IosScreenMetrics.SnapshotFor"));
			Assert.That(source, Does.Contain("IosTouchMenuLogic.ApplyIosFonts(mainMenu)"));
			Assert.That(source, Does.Contain("IosTouchWidgetPolicy.RelativeToRoot"));
			Assert.That(source, Does.Contain("mainMenu.Parent.RenderOrigin"));
			Assert.That(source, Does.Contain("iosMainMenuLayoutEnabled = mainMenuControls.Length >= 4;"));
			Assert.That(source, Does.Not.Contain("GetOrNull<ButtonWidget>(\"CONTENT_BUTTON\")"));
			Assert.That(source, Does.Not.Contain("menus.Bounds ="));
			Assert.That(source, Does.Contain("public override void Tick()"));
			var yaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "chrome", "mainmenu.yaml"));
			Assert.That(yaml, Does.Not.Contain("Logic: MainMenuLogic, IosTouchMenuLogic"));
			Assert.That(yaml, Does.Not.Contain("Button@LANGUAGE_BUTTON:"));
			Assert.That(yaml, Does.Not.Contain("DropDownButton@LANGUAGE_DROPDOWN:"));
			Assert.That(yaml, Does.Contain("Background: cc-soviet-hit-target"));

			var chrome = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "chrome.yaml"));
			foreach (var style in new[]
			{
				"cc-soviet-control:", "cc-soviet-control-hover:",
				"cc-soviet-control-pressed:", "cc-soviet-control-disabled:"
			})
				Assert.That(chrome, Does.Contain(style), style);

			var mainMenuButton = Regex.Match(chrome,
				@"(?ms)^cc-soviet-control:\s*\n(?<body>.*?)(?=^\S|\z)");
			Assert.That(mainMenuButton.Success, Is.True);
			Assert.That(mainMenuButton.Groups["body"].Value, Does.Contain("PanelRegion:"));
			Assert.That(mainMenuButton.Groups["body"].Value,
				Does.Contain("Image: cc-soviet-controls.png"));
			Assert.That(mainMenuButton.Groups["body"].Value,
				Does.Contain("PanelRegion: 0, 0, 56, 56, 400, 144, 56, 56"));
		}

		[Test]
		public void IosMainMenuPanelConvertsFromScreenSpaceWithoutAssumingAZeroParentOrigin()
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(1558, 720, 844, 390, 47, 0, 47, 21));
			var panel = IosMainMenuLayout.Create(policy).Panel.ToRectangle();
			var parentOrigin = new int2(72, 200);
			var relative = IosTouchWidgetPolicy.RelativeToRoot(panel, parentOrigin);

			Assert.That(relative.X + parentOrigin.X, Is.EqualTo(panel.X));
			Assert.That(relative.Y + parentOrigin.Y, Is.EqualTo(panel.Y));
			Assert.That(relative.Size, Is.EqualTo(panel.Size));
		}

		[Test]
		public void MainMenuTickDetectsWhenExternalLayoutRecalculationResetsAppliedBounds()
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(1558, 720, 844, 390, 47, 0, 47, 21));
			var layout = IosMainMenuLayout.Create(policy);
			var actualControls = layout.Controls.Select(bounds =>
				(Widget)new ContainerWidget { Bounds = bounds }).ToArray();
			Assert.That(IosMainMenuLayout.MatchesAppliedBounds(
				layout.Panel, layout.Panel, layout.Controls, actualControls), Is.True);
			var resetPanel = layout.Panel;
			resetPanel.Y++;
			Assert.That(IosMainMenuLayout.MatchesAppliedBounds(
				layout.Panel, resetPanel, layout.Controls, actualControls), Is.False);
			actualControls[2].Bounds.X++;
			Assert.That(IosMainMenuLayout.MatchesAppliedBounds(
				layout.Panel, layout.Panel, layout.Controls, actualControls), Is.False);

			var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "MainMenuLogic.cs"));

			Assert.That(source, Does.Contain("WidgetBounds lastIosPanelBounds;"));
			Assert.That(source, Does.Contain("WidgetBounds[] lastIosControlBounds;"));
			Assert.That(source, Does.Contain("IosMainMenuLayout.MatchesAppliedBounds("));
			Assert.That(source, Does.Contain("lastIosPanelBounds, mainMenu.Bounds"));
			Assert.That(source, Does.Contain("lastIosControlBounds, mainMenuControls"));
		}

		[TestCase(1180, 820, false)]
		[TestCase(844, 390, true)]
		public void IosPolicyUsesLandscapeSafeAreaAndResponsiveBreakpoint(int width, int height, bool compact)
		{
			var policy = IosMenuLayoutPolicy.Create(true, width, height);

			Assert.That(policy.Enabled, Is.True);
			Assert.That(policy.Compact, Is.EqualTo(compact));
			Assert.That(policy.MinimumTarget, Is.GreaterThanOrEqualTo(44));
			Assert.That(policy.ContentBounds.X, Is.GreaterThanOrEqualTo(16));
			Assert.That(policy.ContentBounds.Y, Is.GreaterThanOrEqualTo(12));
			Assert.That(policy.ContentBounds.Right, Is.LessThanOrEqualTo(width - 16));
			Assert.That(policy.ContentBounds.Bottom, Is.LessThanOrEqualTo(height - 12));
		}

		[Test]
		public void DesktopPolicyIsDisabledAndLeavesBoundsUnchanged()
		{
			var policy = IosMenuLayoutPolicy.Create(false, 1180, 820);
			var bounds = new WidgetBounds(10, 20, 30, 25);

			Assert.That(policy.Enabled, Is.False);
			Assert.That(policy.EnsureTouchTarget(bounds), Is.EqualTo(bounds));
		}

		[Test]
		public void IosPolicyExpandsShortControlsWithoutMovingTheirTopEdge()
		{
			var policy = IosMenuLayoutPolicy.Create(true, 1180, 820);
			var enlarged = policy.EnsureTouchTarget(new WidgetBounds(10, 20, 100, 25));

			Assert.That(enlarged, Is.EqualTo(new WidgetBounds(10, 20, 100, 48)));
		}

		[TestCase(0.5f, false, 0.4f)]
		[TestCase(0.5f, true, 0.6f)]
		[TestCase(0f, false, 0f)]
		[TestCase(1f, true, 1f)]
		public void TouchStepValuesAreClamped(float value, bool increment, float expected)
		{
			Assert.That(IosMenuLayoutPolicy.StepValue(value, 0f, 1f, 0.1f, increment),
				Is.EqualTo(expected).Within(0.001f));
		}

		[Test]
		public void WideLobbyGivesConfigurationMostOfTheUsableWidth()
		{
			var layout = IosLobbyLayout.Create(1148, 796, false);

			Assert.That(layout.Players.Width, Is.EqualTo(2 * (1148 - layout.Gap) / 3));
			Assert.That(layout.Players.Height, Is.GreaterThanOrEqualTo(400));
			Assert.That(layout.ChangeMap.X, Is.EqualTo(layout.Map.X));
			Assert.That(layout.ChangeMap.Width, Is.EqualTo(layout.Map.Width));
			Assert.That(layout.ChangeMap.Y, Is.EqualTo(layout.Map.Bottom + layout.Gap));
			Assert.That(layout.Chat.Height, Is.LessThan(layout.Players.Height / 2));
			Assert.That(layout.Tabs.Bottom, Is.LessThanOrEqualTo(layout.Players.Y));
		}

		[Test]
		public void LobbyRemovesHandicapAndKeepsTheRemainingColumnsReadable()
		{
			var policy = IosMenuLayoutPolicy.Create(true, 1560, 720);
			var multiplayer = IosLobbyLayout.Create(1560, 720, policy, false);
			var skirmish = IosLobbyLayout.Create(1560, 720, policy, true);

			Assert.That(skirmish.PlayerHandicap.Width, Is.Zero);
			Assert.That(skirmish.PlayerReady.Width, Is.Zero);
			Assert.That(multiplayer.PlayerHandicap.Width, Is.Zero);
			Assert.That(multiplayer.PlayerReady.Width, Is.GreaterThanOrEqualTo(policy.MinimumTarget));
			Assert.That(multiplayer.PlayerName.Width, Is.LessThan(multiplayer.PlayerFaction.Width));
			Assert.That(multiplayer.PlayerName.Width, Is.GreaterThanOrEqualTo(2 * policy.MinimumTarget));
			Assert.That(multiplayer.PlayerTeam.Right, Is.EqualTo(multiplayer.PlayerSpawn.X));
			Assert.That(multiplayer.PlayerSpawn.Right, Is.EqualTo(multiplayer.PlayerReady.X));
			Assert.That(multiplayer.PlayerReady.Right, Is.EqualTo(multiplayer.PlayerRow.Right));
			Assert.That(skirmish.PlayerName.Width - skirmish.PlayerSpawn.Width,
				Is.EqualTo(policy.MinimumTarget + skirmish.Gap).Within(1));
			Assert.That(skirmish.PlayerColor.Right, Is.EqualTo(skirmish.PlayerFaction.X));
			Assert.That(skirmish.PlayerFaction.Right, Is.EqualTo(skirmish.PlayerTeam.X));
			Assert.That(skirmish.PlayerTeam.Right, Is.EqualTo(skirmish.PlayerSpawn.X));
			Assert.That(skirmish.PlayerSpawn.Right, Is.EqualTo(skirmish.PlayerRow.Right));
			Assert.That(skirmish.Players.Width, Is.EqualTo(2 * skirmish.Map.Width));
		}

		[Test]
		public void CompactLobbyUsesFullHeightMapAndFooterMapAction()
		{
			var layout = IosLobbyLayout.Create(812, 366, true);

			Assert.That(layout.Map.Y, Is.EqualTo(layout.Main.Y));
			Assert.That(layout.Map.Bottom, Is.EqualTo(layout.Main.Bottom));
			Assert.That(layout.Start.Right, Is.LessThanOrEqualTo(layout.ChangeMap.X));
			Assert.That(layout.ChangeMap.Right, Is.LessThanOrEqualTo(layout.Disconnect.X));
			Assert.That(layout.ChangeMap.Y, Is.EqualTo(layout.Footer.Y));
			Assert.That(layout.ChangeMap.Bottom, Is.EqualTo(layout.Footer.Bottom));
			Assert.That(layout.Footer.Bottom, Is.LessThanOrEqualTo(366));
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		public void CompactLobbyReservesReadableChatHistory(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
					safeLeft, safeTop, safeRight, safeBottom));
			var layout = IosLobbyLayout.Create(policy.ContentBounds.Width, policy.ContentBounds.Height, policy);

			Assert.That(layout.ChatDisplay.Height / policy.LogicalPerPoint,
				Is.GreaterThanOrEqualTo(24), "CHAT_DISPLAY must remain readable on short landscape phones.");
			Assert.That(layout.ChatInput.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(layout.ChatDisplay.Bottom, Is.LessThanOrEqualTo(layout.ChatInput.Y));
			Assert.That(layout.PlayerList.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48),
				"The compact player list must show at least one complete touch row.");
			Assert.That(layout.Map.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(24),
				"The compact map preview must remain readable.");
		}

		[Test]
		public void ServerNoticeHeaderSegmentsRemeasureAndClampWithoutOverlap()
		{
			var segments = IosServerCreationLayout.HeaderSegments(300, 180, 160, 120);

			Assert.That(segments, Has.Length.EqualTo(3));
			Assert.That(segments[0].X, Is.Zero);
			Assert.That(segments[0].Right, Is.EqualTo(segments[1].X));
			Assert.That(segments[1].Right, Is.EqualTo(segments[2].X));
			Assert.That(segments[2].Right, Is.EqualTo(300));
			Assert.That(segments.All(segment => segment.Width > 0), Is.True);
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21, true)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21, true)]
		[TestCase(1133, 744, 1133, 744, 0, 0, 0, 20, false)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20, false)]
		[TestCase(1366, 1024, 1366, 1024, 0, 0, 0, 20, false)]
		public void SnapshotPolicyUsesSafeAreaNativeBreakpointAndPhysicalTargets(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom, bool isPhone)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);

			Assert.That(policy.Enabled, Is.True);
			Assert.That(policy.IsPhone, Is.EqualTo(isPhone));
			Assert.That(policy.Compact, Is.EqualTo(isPhone));
			Assert.That(policy.LogicalPerPoint, Is.EqualTo(snapshot.LogicalPerPoint).Within(0.0001));
			Assert.That(snapshot.SafeBounds.Contains(policy.ContentBounds.ToRectangle()), Is.True);
			Assert.That(policy.ContentBounds.Width / (double)snapshot.SafeBounds.Width, Is.GreaterThanOrEqualTo(0.9));
			Assert.That(policy.ContentBounds.Height / (double)snapshot.SafeBounds.Height, Is.GreaterThanOrEqualTo(0.9));
			Assert.That(policy.MinimumTarget / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(policy.HeaderHeight / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(policy.FooterHeight / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
		}

		[Test]
		public void CompatibilityOverloadTreatsItsDimensionsAsNativePoints()
		{
			var phone = IosMenuLayoutPolicy.Create(true, 844, 390);
			var ipad = IosMenuLayoutPolicy.Create(true, 1180, 820);

			Assert.That(phone.IsPhone, Is.True);
			Assert.That(phone.LogicalPerPoint, Is.EqualTo(1));
			Assert.That(ipad.IsPhone, Is.False);
		}

		[TestCase(1558, 720, 844, 390, 2)]
		[TestCase(720, 1558, 390, 844, 1)]
		[TestCase(1180, 820, 1180, 820, 3)]
		public void LobbyOptionRowsHaveLargeNonOverlappingControls(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight, int expectedColumns)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight, 0, 0, 0, 20));
			var width = policy.ContentBounds.Width;
			var checkboxRow = new WidgetBounds(0, 0, width, policy.OptionRowHeight(false));
			var dropdownRow = new WidgetBounds(0, 0, width, policy.OptionRowHeight(true));

			Assert.That(policy.OptionColumnCount, Is.EqualTo(expectedColumns));
			var checkboxControls = Enumerable.Range(0, 3)
				.Select(i => policy.OptionControlBounds(i, width, false)).ToArray();
			var dropdownControls = Enumerable.Range(0, 3)
				.Select(i => policy.OptionControlBounds(i, width, true)).ToArray();
			for (var i = 0; i < 3; i++)
			{
				AssertContained(checkboxRow, checkboxControls[i]);
				AssertContained(dropdownRow, dropdownControls[i]);
				AssertPhysicalTarget(checkboxControls[i], policy);
				AssertPhysicalTarget(dropdownControls[i], policy);
				var label = policy.OptionLabelBounds(i, width);
				AssertContained(dropdownRow, label);
				Assert.That(label.Bottom, Is.LessThanOrEqualTo(dropdownControls[i].Y));

				for (var j = 0; j < i; j++)
				{
					Assert.That(checkboxControls[i].ToRectangle().IntersectsWith(checkboxControls[j].ToRectangle()), Is.False);
					Assert.That(dropdownControls[i].ToRectangle().IntersectsWith(dropdownControls[j].ToRectangle()), Is.False);
				}
			}
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1133, 744, 1133, 744, 0, 0, 0, 20)]
		[TestCase(1024, 768, 1024, 768, 0, 0, 0, 20)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		[TestCase(1366, 1024, 1366, 1024, 0, 0, 0, 20)]
		public void LobbyGeometryKeepsAllSurfacesAndPlayerColumnsUsable(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
					safeLeft, safeTop, safeRight, safeBottom));
			var layout = IosLobbyLayout.Create(policy.ContentBounds.Width, policy.ContentBounds.Height, policy);
			var window = new WidgetBounds(0, 0, policy.ContentBounds.Width, policy.ContentBounds.Height);

			foreach (var region in new[]
			{
				layout.Header, layout.Tabs, layout.Main, layout.Servers, layout.Players, layout.Map, layout.ChangeMap,
				layout.Position, layout.Chat, layout.Footer, layout.Start, layout.Disconnect
			})
				AssertContained(window, region);

			Assert.That(layout.Players.Right, Is.LessThanOrEqualTo(layout.Map.X));
			Assert.That(layout.Servers, Is.EqualTo(layout.Main));
			if (policy.IsPhone)
			{
				Assert.That(layout.Map.Y, Is.EqualTo(layout.Main.Y));
				Assert.That(layout.Map.Bottom, Is.EqualTo(layout.Main.Bottom));
				Assert.That(layout.Start.Right, Is.LessThanOrEqualTo(layout.ChangeMap.X));
				Assert.That(layout.ChangeMap.Right, Is.LessThanOrEqualTo(layout.Disconnect.X));
				Assert.That(layout.ChangeMap.Y, Is.EqualTo(layout.Footer.Y));
			}
			else
			{
				Assert.That(layout.ChangeMap.X, Is.EqualTo(layout.Map.X));
				Assert.That(layout.ChangeMap.Width, Is.EqualTo(layout.Map.Width));
				Assert.That(layout.ChangeMap.Y, Is.EqualTo(layout.Map.Bottom + layout.Gap));
			}

			AssertPhysicalTarget(layout.ChangeMap, policy);
			AssertPhysicalTarget(layout.Start, policy);
			AssertPhysicalTarget(layout.Disconnect, policy);

			var playerRegion = new WidgetBounds(0, 0, layout.Players.Width, layout.Players.Height);
			AssertContained(playerRegion, layout.PlayerRow);
			Assert.That(layout.PlayerRow.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(layout.PlayerHandicap.Width, Is.Zero);
			var columns = new[]
			{
				layout.PlayerName, layout.PlayerColor, layout.PlayerFaction,
				layout.PlayerTeam, layout.PlayerSpawn, layout.PlayerReady
			};
			var minimumUnits = new[] { 2, 1, 3, 1, 1, 1 };
			Assert.That(layout.PlayerRow.Width, Is.GreaterThanOrEqualTo(9 * policy.MinimumTarget));
			for (var i = 0; i < columns.Length; i++)
			{
				var column = columns[i];
				AssertContained(new WidgetBounds(0, 0, layout.PlayerRow.Width, layout.PlayerRow.Height), column);
				Assert.That(column.Width, Is.GreaterThanOrEqualTo(minimumUnits[i] * policy.MinimumTarget));
				Assert.That(column.Width, Is.GreaterThan(0));
			}

			for (var i = 1; i < columns.Length; i++)
				Assert.That(columns[i - 1].Right, Is.LessThanOrEqualTo(columns[i].X));

			if (nativeWidth == 844)
				Assert.That(layout.PlayerRow.Width - 9 * policy.MinimumTarget, Is.GreaterThan(0));
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1133, 744, 1133, 744, 0, 0, 0, 20)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		[TestCase(1366, 1024, 1366, 1024, 0, 0, 0, 20)]
		public void CreateServerGeometryIsNearFullscreenAndContained(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = IosServerCreationLayout.Create(policy);

			Assert.That(policy.ContentBounds.Width / (double)snapshot.SafeBounds.Width, Is.GreaterThanOrEqualTo(0.9));
			Assert.That(policy.ContentBounds.Height / (double)snapshot.SafeBounds.Height, Is.GreaterThanOrEqualTo(0.9));
			foreach (var region in new[]
			{
				layout.Header, layout.Form, layout.MapPreview, layout.ChangeMap, layout.Footer,
				layout.CreateButton, layout.Back, layout.ServerNameRow, layout.PasswordRow,
				layout.PortRow, layout.Notices
			})
				AssertContained(layout.Window, region);

			Assert.That(layout.Form.Right, Is.LessThanOrEqualTo(layout.MapPreview.X));
			Assert.That(layout.Form.Width, Is.GreaterThan(layout.MapPreview.Width));
			Assert.That(layout.ChangeMap.X, Is.EqualTo(layout.MapPreview.X));
			Assert.That(layout.ChangeMap.Width, Is.EqualTo(layout.MapPreview.Width));
			Assert.That(layout.ChangeMap.Y, Is.EqualTo(layout.MapPreview.Bottom + layout.Gap));
			if (policy.IsPhone)
			{
				Assert.That(layout.ServerNameRow.Right, Is.LessThanOrEqualTo(layout.PasswordRow.X));
				Assert.That(layout.PasswordRow.Right, Is.LessThanOrEqualTo(layout.PortRow.X));
				Assert.That(layout.ServerNameRow.Y, Is.EqualTo(layout.PasswordRow.Y));
				Assert.That(layout.PasswordRow.Y, Is.EqualTo(layout.PortRow.Y));
				Assert.That(layout.ServerNameRow.Height,
					Is.GreaterThanOrEqualTo(policy.MinimumReadableTextHeight + policy.Gap + policy.MinimumTarget),
					"Phone form columns need a separate label band above their 48pt controls.");
			}
			else
			{
				Assert.That(layout.ServerNameRow.Bottom, Is.LessThanOrEqualTo(layout.PasswordRow.Y));
				Assert.That(layout.PasswordRow.Bottom, Is.LessThanOrEqualTo(layout.PortRow.Y));
			}

			Assert.That(layout.PortRow.Bottom, Is.LessThanOrEqualTo(layout.Notices.Y));
			Assert.That(layout.NoticesBody.Height / policy.LogicalPerPoint,
				Is.GreaterThanOrEqualTo(24), "The Notices body must remain readable and reachable.");
			Assert.That(layout.NoticesBody.Bottom, Is.LessThanOrEqualTo(layout.Footer.Y));
			AssertPhysicalTarget(layout.ChangeMap, policy);
			AssertPhysicalTarget(layout.CreateButton, policy);
			AssertPhysicalTarget(layout.Back, policy);
		}

		[Test]
		public void CommonLobbyAndRa2ServerRootsOptInAndPreserveAllControls()
		{
			var root = RepositoryRoot();
			var chrome = Path.Combine(root, "engine", "mods", "common", "chrome");
			var options = File.ReadAllText(Path.Combine(chrome, "lobby-options.yaml"));
			var players = File.ReadAllText(Path.Combine(chrome, "lobby-players.yaml"));
			var createServer = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "multiplayer-createserver.yaml"));
			var manifest = File.ReadAllText(Path.Combine(root, "mods", "ra2", "mod.yaml"));
			var ra2Chrome = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome.yaml"));

			Assert.That(options, Does.Contain("Container@LOBBY_OPTIONS_BIN:\n\tLogic: IosTouchMenuLogic"));
			Assert.That(players, Does.Contain("Container@LOBBY_PLAYER_BIN:\n\tLogic: IosTouchMenuLogic"));
			Assert.That(createServer, Does.Contain("Logic: ServerCreationLogic, IosTouchMenuLogic"));
			Assert.That(createServer, Does.Contain("Background: cc-mp-form-background"));
			Assert.That(createServer, Does.Not.Contain("Background: cc-multiplayer-frame"));
			Assert.That(createServer, Does.Contain("Background: cc-mp-control"));
			Assert.That(ra2Chrome, Does.Contain("dialog:\n\tInherits: menu-panel"));
			Assert.That(ra2Chrome, Does.Contain("button:\n\tInherits: menu-button"));
			foreach (var file in new[] { "lobby-options.yaml", "lobby-players.yaml" })
				Assert.That(manifest, Does.Contain("common|chrome/" + file));
			Assert.That(manifest, Does.Contain("ra2|chrome/multiplayer-createserver.yaml"));

			foreach (var id in new[]
			{
				"NAME", "COLOR", "FACTION", "TEAM_DROPDOWN", "HANDICAP_DROPDOWN",
				"SPAWN_DROPDOWN", "STATUS_CHECKBOX"
			})
				Assert.That(players, Does.Contain("@" + id + ":"), $"Lobby player surface lost {id}.");

			foreach (var id in new[]
			{
				"SERVER_NAME", "PASSWORD", "LISTEN_PORT", "ADVERTISE_CHECKBOX", "MAP_PREVIEW_ROOT",
				"MAP_BUTTON", "CREATE_BUTTON", "BACK_BUTTON"
			})
				Assert.That(createServer, Does.Contain("@" + id + ":"), $"Create Server surface lost {id}.");
		}

		[Test]
		public void ProductMenusRemoveExtrasTelemetryAndSpectatorEntryPoints()
		{
			var root = RepositoryRoot();
			var mainMenu = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "mainmenu.yaml"));
			var mainMenuLogic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "MainMenuLogic.cs"));
			var advanced = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "chrome",
				"settings-advanced.yaml"));
			var prompts = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "chrome",
				"mainmenu-prompts.yaml"));
			var advancedLogic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "Settings", "AdvancedSettingsLogic.cs"));
			var players = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "chrome",
				"lobby-players.yaml"));
			var lobbyLogic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "Lobby", "LobbyLogic.cs"));
			var commands = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"ServerTraits", "LobbyCommands.cs"));
			var session = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Game", "Network", "Session.cs"));

			Assert.That(mainMenu, Does.Not.Contain("EXTRAS_BUTTON"));
			Assert.That(mainMenu, Does.Not.Contain("EXTRAS_MENU"));
			Assert.That(mainMenuLogic, Does.Not.Contain("SystemInfoPromptLogic"));
			Assert.That(advanced, Does.Not.Contain("SENDSYSINFO"));
			Assert.That(prompts, Does.Not.Contain("MAINMENU_SYSTEM_INFO_PROMPT"));
			Assert.That(advancedLogic, Does.Not.Contain("SendSystemInformation"));
			Assert.That(players, Does.Not.Contain("TEMPLATE_NEW_SPECTATOR"));
			Assert.That(players, Does.Not.Contain("@SPECTATE"));
			Assert.That(players, Does.Not.Contain("TOGGLE_SPECTATORS"));
			Assert.That(lobbyLogic, Does.Not.Contain("newSpectatorTemplate"));
			Assert.That(commands, Does.Not.Contain("{ \"spectate\", Specate }"));
			Assert.That(commands, Does.Not.Contain("{ \"allow_spectators\", AllowSpectators }"));
			Assert.That(commands, Does.Not.Contain("{ \"make_spectator\", MakeSpectator }"));
			Assert.That(session, Does.Contain("public bool AllowSpectators = false;"));
		}

		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1133, 744, 1133, 744, 0, 0, 0, 20)]
		[TestCase(1180, 820, 1180, 820, 0, 0, 0, 20)]
		public void MultiplayerBrowserUsesContainedReadableColumnsAndActions(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var policy = IosMenuLayoutPolicy.Create(true,
				Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
					safeLeft, safeTop, safeRight, safeBottom));
			var layout = IosMultiplayerBrowserLayout.Create(policy);

			foreach (var region in new[]
			{
				layout.Header, layout.LocalMode, layout.OnlineMode, layout.Table, layout.TableHeader,
				layout.ServerList, layout.Details, layout.Footer, layout.Filters, layout.Reload,
				layout.RoomCodeInput, layout.RoomCodeButton, layout.DirectConnect, layout.CreateButton, layout.Back
			})
				AssertContained(layout.Window, region);

			Assert.That(layout.ServerList.Bottom, Is.LessThanOrEqualTo(layout.Footer.Y));
			Assert.That(layout.LocalMode.Right, Is.LessThanOrEqualTo(layout.OnlineMode.X));
			Assert.That(layout.OnlineMode.Right, Is.LessThanOrEqualTo(layout.Header.X));
			Assert.That(layout.RoomCodeInput.Right, Is.LessThanOrEqualTo(layout.Reload.X));
			Assert.That(layout.Reload.Right, Is.LessThanOrEqualTo(layout.RoomCodeButton.X));
			Assert.That(layout.Details.X, Is.GreaterThanOrEqualTo(layout.Table.Right));
			Assert.That(layout.ServerColumns.First().X, Is.Zero);
			Assert.That(layout.ServerColumns.Last().Right, Is.EqualTo(layout.ServerList.Width));
			for (var i = 1; i < layout.ServerColumns.Length; i++)
				Assert.That(layout.ServerColumns[i - 1].Right, Is.EqualTo(layout.ServerColumns[i].X));
			foreach (var column in layout.ServerColumns)
				Assert.That(column.Width / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			foreach (var action in new[]
			{
				layout.LocalMode, layout.OnlineMode, layout.Filters, layout.Reload, layout.RoomCodeInput,
				layout.RoomCodeButton, layout.DirectConnect, layout.CreateButton, layout.Back
			})
				AssertPhysicalTarget(action, policy);
		}

		[Test]
		public void MultiplayerPagesOptIntoTheResponsiveTouchLayout()
		{
			var chrome = Path.Combine(RepositoryRoot(), "engine", "mods", "common", "chrome");
			foreach (var file in new[]
			{
				"multiplayer-browser.yaml", "multiplayer-directconnect.yaml", "lobby-servers.yaml"
			})
				Assert.That(File.ReadAllText(Path.Combine(chrome, file)), Does.Contain("IosTouchMenuLogic"));
		}

		[Test]
		public void MainMenuDoesNotShipOrReferenceAfghanistanCrisisBrandArtwork()
		{
			var root = RepositoryRoot();
			foreach (var modRoot in new[]
			{
				Path.Combine(root, "mods", "ra2"),
				Path.Combine(root, "engine", "mods", "ra2")
			})
			{
				var mainMenu = File.ReadAllText(Path.Combine(modRoot, "chrome", "mainmenu.yaml"));
				var chrome = File.ReadAllText(Path.Combine(modRoot, "chrome.yaml"));

				Assert.That(mainMenu, Does.Not.Contain("Image@LOGO"));
				Assert.That(mainMenu, Does.Not.Contain("menu-logo"));
				Assert.That(chrome, Does.Not.Contain("menulogo"));
				Assert.That(chrome, Does.Not.Contain("menutitle"));

				foreach (var file in new[]
				{
					"menulogo.png", "menulogo-sm.png", "menutitle.png", "menutitle-sm.png"
				})
					Assert.That(File.Exists(Path.Combine(modRoot, "uibits", file)), Is.False,
						$"Obsolete Afghanistan Crisis brand art remains: {modRoot}/uibits/{file}");
			}
		}

		[Test]
		public void IosSourcesUseSnapshotsAndAdaptDynamicLobbyContent()
		{
			var root = RepositoryRoot();
			var logicRoot = Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic");
			var touch = File.ReadAllText(Path.Combine(logicRoot, "IosTouchMenuLogic.cs"));
			var lobby = File.ReadAllText(Path.Combine(logicRoot, "Lobby", "LobbyLogic.cs"));

			Assert.That(touch, Does.Contain("IosScreenMetrics.SnapshotFor"));
			Assert.That(touch, Does.Contain("LayoutLobbyOptionsRows"));
			Assert.That(touch, Does.Contain("EnableContentDragging = true"));
			Assert.That(touch, Does.Contain("ApplyServerCreationLayout"));
			Assert.That(lobby, Does.Contain("IosScreenMetrics.SnapshotFor"));
			Assert.That(lobby, Does.Contain("LayoutIosPlayerRows"));
			foreach (var setup in new[]
			{
				"SetupEditableNameWidget", "SetupEditableClassicColorWidget", "SetupEditableFactionWidget",
				"SetupEditableTeamWidget", "SetupEditableSpawnWidget",
				"SetupEditableReadyWidget"
			})
				Assert.That(lobby, Does.Contain(setup), $"LobbyLogic lost {setup}.");
			Assert.That(lobby, Does.Not.Contain("LobbyUtils.SetupEditableHandicapWidget"));
			Assert.That(lobby, Does.Not.Contain("LobbyUtils.SetupHandicapWidget"));
			Assert.That(lobby, Does.Contain("label.WordWrap = false"));
		}

		[Test]
		public void SkirmishLobbyUsesClassicColorsAndSkipsRemovedControls()
		{
			var root = RepositoryRoot();
			var lobby = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "Lobby", "LobbyLogic.cs"));
			var utils = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "Lobby", "LobbyUtils.cs"));
			var settings = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common",
				"Widgets", "Logic", "Settings", "DisplaySettingsLogic.cs"));

			Assert.That(lobby, Does.Contain("SetupEditableClassicColorWidget"));
			Assert.That(lobby, Does.Contain("if (!skirmishMode)\n\t\t\t\t\t\tLobbyUtils.SetupEditableReadyWidget"));
			Assert.That(lobby, Does.Contain("handicap.IsVisible = () => false"));
			Assert.That(utils, Does.Contain("ShowClassicColorDropDown"));
			Assert.That(settings, Does.Contain("ShowClassicColorDropDown(colorDropdown"));
			Assert.That(settings, Does.Not.Contain("ShowColorDropDown(colorDropdown, ps.Color"));
		}

		[TestCase("Tiny", false, false, "IosRegular")]
		[TestCase("Regular", false, false, "IosRegular")]
		[TestCase("TinyBold", false, false, "IosBold")]
		[TestCase("Regular", false, true, "IosBold")]
		[TestCase("Bold", true, false, "IosTitle")]
		public void IosMenuFontsReplaceDesktopSizedFonts(
			string original, bool title, bool action, string expected)
		{
			Assert.That(IosMenuLayoutPolicy.TouchFont(original, title, action), Is.EqualTo(expected));
		}

		[Test]
		public void ShippingConfigurationRootsDeclareTouchAdaptation()
		{
			var chrome = Path.Combine(RepositoryRoot(), "engine", "mods", "common", "chrome");
			foreach (var file in new[] { "lobby-options.yaml", "lobby-players.yaml", "multiplayer-createserver.yaml" })
			{
				var text = File.ReadAllText(Path.Combine(chrome, file));
				Assert.That(text, Does.Contain("IosTouchMenuLogic"), $"{file} is missing iOS touch adaptation.");
			}
		}

		static IosScreenSnapshot Snapshot(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			return new IosScreenSnapshot(
				new Size(effectiveWidth, effectiveHeight),
				new Size(nativeWidth, nativeHeight),
				new IosSafeAreaInsets(safeLeft, safeTop, safeRight, safeBottom));
		}

		static void AssertPhysicalTarget(WidgetBounds bounds, IosMenuLayoutPolicy policy)
		{
			Assert.That(bounds.Width / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(bounds.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
		}

		static void AssertContained(WidgetBounds outer, WidgetBounds inner)
		{
			Assert.That(outer.ToRectangle().Contains(inner.ToRectangle()), Is.True,
				$"Expected {inner.ToRectangle()} inside {outer.ToRectangle()}.");
		}

		static WidgetBounds MainMenuControlBounds(string yaml, string id)
		{
			var body = MainMenuControlBody(yaml, id);
			int Value(string key)
			{
				var value = Regex.Match(body, $@"(?m)^\s+{key}: (?<value>\d+)$");
				Assert.That(value.Success, Is.True, $"Missing {key} for {id}.");
				return int.Parse(value.Groups["value"].Value, CultureInfo.InvariantCulture);
			}

			return new WidgetBounds(Value("X"), Value("Y"), Value("Width"), Value("Height"));
		}

		static string MainMenuControlBody(string yaml, string id)
		{
			const string WidgetTypes = "AdaptiveRouteButton|MacHomeUtilityButton|Button|DropDownButton";
			const string NextWidgetTypes = WidgetTypes + "|Background|Label|Container";
			var match = Regex.Match(yaml,
				$@"(?ms)^\s+(?:{WidgetTypes})@{Regex.Escape(id)}:\s*\n" +
				$@"(?<body>.*?)(?=^\s+(?:{NextWidgetTypes})@|\z)");
			Assert.That(match.Success, Is.True, $"Missing main-menu control {id}.");
			return match.Groups["body"].Value;
		}

		sealed class ActualIosMenuFonts : IDisposable
		{
			readonly SheetBuilder sheetBuilder;

			public SpriteFont Regular { get; }
			public SpriteFont Bold { get; }

			public ActualIosMenuFonts()
			{
				Log.AddChannel("perf", null);
				var root = RepositoryRoot();
				var platform = new DefaultPlatform();
				sheetBuilder = new SheetBuilder(SheetType.BGRA, 2048);
				Regular = new SpriteFont(platform, "IosRegular", File.ReadAllBytes(Path.Combine(
					root, "mods", "ra2", "fonts", "NotoSansCJKsc-Regular.otf")), 24, 19, 1f, sheetBuilder);
				Bold = new SpriteFont(platform, "IosBold", File.ReadAllBytes(Path.Combine(
					root, "mods", "ra2", "fonts", "NotoSansCJKsc-Bold.otf")), 26, 20, 1f, sheetBuilder);
			}

			public void Dispose()
			{
				Regular.Dispose();
				Bold.Dispose();
				sheetBuilder.Dispose();
			}
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
