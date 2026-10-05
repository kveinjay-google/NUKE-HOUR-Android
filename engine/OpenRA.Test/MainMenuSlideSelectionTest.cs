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
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class MainMenuSlideSelectionTest
	{
		static readonly Rectangle[] Targets =
		{
			new(20, 20, 200, 60),
			new(20, 100, 200, 60),
			new(20, 180, 200, 60),
		};

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "OpenRA.Mods.RA2"))))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null);
			return directory!.FullName;
		}

		[Test]
		public void PressAndVerticalSlideSelectsTheButtonUnderTheFinger()
		{
			var enabled = new[] { true, true, true };
			var selected = MainMenuSlideSelectionPolicy.Resolve(Targets, enabled, new int2(80, 50), -1);
			Assert.That(selected, Is.EqualTo(0));

			selected = MainMenuSlideSelectionPolicy.Resolve(Targets, enabled, new int2(80, 130), selected);
			Assert.That(selected, Is.EqualTo(1));

			selected = MainMenuSlideSelectionPolicy.Resolve(Targets, enabled, new int2(80, 210), selected);
			Assert.That(selected, Is.EqualTo(2));
		}

		[Test]
		public void GapsDisabledTargetsAndLeavingTheMenuKeepTheLastValidSelection()
		{
			var enabled = new[] { true, false, true };
			Assert.That(MainMenuSlideSelectionPolicy.Resolve(Targets, enabled, new int2(80, 90), 0), Is.EqualTo(0));
			Assert.That(MainMenuSlideSelectionPolicy.Resolve(Targets, enabled, new int2(80, 130), 0), Is.EqualTo(0));
			Assert.That(MainMenuSlideSelectionPolicy.Resolve(Targets, enabled, new int2(500, 500), 2), Is.EqualTo(2));
		}

		[Test]
		public void MainMenuPlacesTheSlideSelectorAboveItsButtons()
		{
			var root = RepositoryRoot();
			var yaml = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "mainmenu.yaml"));
			var selector = yaml.IndexOf("MainMenuSlideSelector@SLIDE_SELECTOR:", System.StringComparison.Ordinal);
			var quit = yaml.IndexOf("MacHomeUtilityButton@QUIT_BUTTON:", System.StringComparison.Ordinal);
			var nextMenu = yaml.IndexOf("Background@MAP_EDITOR_MENU:", System.StringComparison.Ordinal);

			Assert.That(selector, Is.GreaterThan(quit));
			Assert.That(selector, Is.LessThan(nextMenu));
			StringAssert.Contains("Width: PARENT_WIDTH", yaml[selector..]);
			StringAssert.Contains("Height: PARENT_HEIGHT", yaml[selector..]);
		}

		[Test]
		public void SlideSelectorPreservesDesktopInputAndUsesTheNormalButtonActivationPath()
		{
			var root = RepositoryRoot();
			var source = File.ReadAllText(Path.Combine(root, "OpenRA.Mods.RA2", "Widgets",
				"MainMenuSlideSelectorWidget.cs"));

			StringAssert.Contains("if (TouchOnly && !Platform.UsesMobileLayout)", source);
			StringAssert.Contains("mi.Event == MouseInputEvent.Cancel", source);
			StringAssert.Contains("button.HandleMouseInput(down);", source);
			StringAssert.Contains("button.HandleMouseInput(up);", source);
			StringAssert.Contains("targets[i].Depressed = originalStates[i].Depressed || selected;", source);
			StringAssert.Contains("targets[i].Highlighted = originalStates[i].Highlighted || selected;", source);

		}

		[Test]
		public void IosAotRegistryIncludesMainMenuSlideSelector()
		{
			var root = RepositoryRoot();
			var registry = File.ReadAllText(Path.Combine(root, "ios", "OpenRA.iOS",
				"GeneratedAotObjectRegistry.g.cs"));
			StringAssert.Contains("\"OpenRA.Mods.RA2.Widgets.MainMenuSlideSelectorWidget\"", registry);
			StringAssert.Contains(
				"GeneratedAotObjectRegistry.RegisterBasic(typeof(global::OpenRA.Mods.RA2.Widgets.MainMenuSlideSelectorWidget)",
				registry);
		}
	}
}
