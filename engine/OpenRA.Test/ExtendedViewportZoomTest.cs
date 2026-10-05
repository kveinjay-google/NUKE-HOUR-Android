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
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class ExtendedViewportZoomTest
	{
		[Test]
		public void NewInstallDefaultsToExtendedViewport()
		{
			var settings = new GraphicSettings();

			Assert.That(settings.ViewportDistance, Is.EqualTo(WorldViewport.Extended));
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "engine", "OpenRA.Game")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[Test]
		public void ExtendedPresetAddsOneBoundedGameplayZoomLevel()
		{
			Assert.That(Enum.GetNames<WorldViewport>(), Does.Contain("Extended"));
			var method = typeof(WorldViewportSizes).GetMethod(
				"GetMinimumZoom", BindingFlags.Instance | BindingFlags.Public);
			Assert.That(method, Is.Not.Null);
			if (method == null)
				return;

			var sizes = new WorldViewportSizes();
			float Minimum(string preset, int height) => (float)method.Invoke(
				sizes, new object[] { Enum.Parse<WorldViewport>(preset), height })!;

			Assert.That(Minimum("Extended", 2048), Is.EqualTo(0.8f).Within(0.0001f));
			Assert.That(Minimum("Native", 2048), Is.EqualTo(1f).Within(0.0001f));
			Assert.That(Minimum("Far", 2048), Is.EqualTo(2f).Within(0.0001f));
			Assert.That(Minimum("Medium", 2048), Is.EqualTo(3f).Within(0.0001f));
			Assert.That(Minimum("Close", 2048), Is.EqualTo(4f).Within(0.0001f));
		}

		[Test]
		public void ExtendedPresetKeepsTheExistingNearZoomLimit()
		{
			var minimumMethod = typeof(WorldViewportSizes).GetMethod(
				"GetMinimumZoom", BindingFlags.Instance | BindingFlags.Public);
			var maximumMethod = typeof(WorldViewportSizes).GetMethod(
				"GetMaximumZoom", BindingFlags.Instance | BindingFlags.Public);
			Assert.That(minimumMethod, Is.Not.Null);
			Assert.That(maximumMethod, Is.Not.Null,
				"The farther preset must not reduce the player's existing close-zoom range.");
			if (minimumMethod == null || maximumMethod == null)
				return;

			var sizes = new WorldViewportSizes();
			var extended = Enum.Parse<WorldViewport>("Extended");
			var minimum = (float)minimumMethod.Invoke(sizes, new object[] { extended, 2048 })!;
			var maximum = (float)maximumMethod.Invoke(sizes, new object[] { extended, minimum, 2048 })!;

			Assert.That(maximum, Is.EqualTo(2f).Within(0.0001f));
		}

		[Test]
		public void ExtendedPresetRemainsAvailableAtPhoneLandscapeHeights()
		{
			var sizes = new WorldViewportSizes();
			var phoneSizes = DisplaySettingsLogic.GetAvailableViewportSizes(sizes, 1290);
			var tabletSizes = DisplaySettingsLogic.GetAvailableViewportSizes(sizes, 2048);

			Assert.That(phoneSizes, Is.EqualTo(new[]
			{
				WorldViewport.Close,
				WorldViewport.Medium,
				WorldViewport.Far,
				WorldViewport.Extended
			}));
			Assert.That(tabletSizes, Is.EqualTo(new[]
			{
				WorldViewport.Close,
				WorldViewport.Medium,
				WorldViewport.Far,
				WorldViewport.Native,
				WorldViewport.Extended
			}));
		}

		[Test]
		public void DisplaySettingsOffersLocalizedExtendedPresetAfterNative()
		{
			var root = RepositoryRoot();
			var logic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic",
				"Settings", "DisplaySettingsLogic.cs"));
			var english = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "fluent", "common.ftl"));
			var chinese = File.ReadAllText(Path.Combine(
				root, "engine", "mods", "common", "fluent", "zh-CN", "common.ftl"));

			StringAssert.Contains("case WorldViewport.Extended:", logic);
			StringAssert.Contains("validSizes.Add(WorldViewport.Extended);", logic);
			Assert.That(logic.IndexOf("validSizes.Add(WorldViewport.Extended);", StringComparison.Ordinal),
				Is.GreaterThan(logic.IndexOf("validSizes.Add(WorldViewport.Native);", StringComparison.Ordinal)));
			StringAssert.Contains(".extended = Extended", english);
			StringAssert.Contains(".extended = 超远", chinese);
		}

		[Test]
		public void BattlefieldCameraSelectionAppliesAndPersistsImmediately()
		{
			var settings = new GraphicSettings();
			var applied = 0;
			var saved = 0;

			DisplaySettingsLogic.ApplyBattlefieldCameraSelection(
				settings,
				WorldViewport.Far,
				() => applied++,
				() => saved++);

			Assert.That(settings.ViewportDistance, Is.EqualTo(WorldViewport.Far));
			Assert.That(applied, Is.EqualTo(1));
			Assert.That(saved, Is.EqualTo(1));
		}
	}
}
