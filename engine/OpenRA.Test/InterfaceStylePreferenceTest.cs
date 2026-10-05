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
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class InterfaceStylePreferenceTest
	{
		[Test]
		public void NewSettingsDefaultToHighDefinitionUi()
		{
			var field = typeof(GameSettings).GetField("UseHighDefinitionUI");
			Assert.That(field, Is.Not.Null, "GameSettings must persist the selected interface style.");
			Assert.That(field.GetValue(new GameSettings()), Is.EqualTo(true));
			Assert.That(new GameSettings().EffectiveInterfaceStyle, Is.EqualTo(InterfaceStyleMode.ClassicHD));
		}

		[Test]
		public void MainMenuToggleUpdatesPreferenceBeforeSaving()
		{
			var field = typeof(GameSettings).GetField("UseHighDefinitionUI");
			var method = typeof(Mods.Common.Widgets.Logic.MainMenuLogic).GetMethod(
				"ToggleInterfaceStyle", BindingFlags.Public | BindingFlags.Static);
			Assert.Multiple(() =>
			{
				Assert.That(field, Is.Not.Null);
				Assert.That(method, Is.Not.Null);
			});
			if (field == null || method == null)
				return;

			var settings = new GameSettings();
			var saved = false;
			method.Invoke(null, new object[]
			{
				settings,
				(Action)(() =>
				{
					Assert.That(field.GetValue(settings), Is.EqualTo(false));
					saved = true;
				}),
			});

			Assert.That(saved, Is.True);
		}

		[Test]
		public void ProductionPresentationHonorsBothInterfaceStyles()
		{
			var method = typeof(Mods.Common.Widgets.ProductionIconPresentation).GetMethod(
				"IsHighDefinitionEnabled", BindingFlags.Public | BindingFlags.Static);
			Assert.That(method, Is.Not.Null);
			if (method == null)
				return;

			var settings = new GameSettings { InterfaceStyle = InterfaceStyleMode.Classic };
			Assert.That(method.Invoke(null, new object[] { settings }), Is.EqualTo(false));
			settings.InterfaceStyle = InterfaceStyleMode.ClassicHD;
			Assert.That(method.Invoke(null, new object[] { settings }), Is.EqualTo(true));
		}

		[Test]
		public void MainMenuAndFluentCatalogsExposeTheInterfaceStyleControl()
		{
			var root = RepositoryRoot();
			var menu = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "settings-display.yaml"));
			var logic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "MainMenuLogic.cs"));
			var english = File.ReadAllText(Path.Combine(root, "mods", "ra2", "fluent", "chrome.ftl"));
			var chinese = File.ReadAllText(Path.Combine(root, "mods", "ra2", "fluent", "zh-CN", "chrome.ftl"));

			Assert.Multiple(() =>
			{
				Assert.That(menu, Does.Contain("DropDownButton@UI_STYLE_BUTTON:"));
				foreach (var key in new[] { "label-ui-style-classic", "label-ui-style-classic-hd" })
				{
					Assert.That(english, Does.Contain(key + " ="));
					Assert.That(chinese, Does.Contain(key + " ="));
				}
				Assert.That(logic, Does.Contain("GetOrNull<ButtonWidget>(\"UI_STYLE_BUTTON\")"));
				Assert.That(logic, Does.Contain("VisibleInterfaceStyles"));
				Assert.That(english, Does.Contain("button-main-menu-ui-style-classic = Interface Style: Classic"));
				Assert.That(english, Does.Contain("button-main-menu-ui-style-hd = Interface Style: HD"));
				Assert.That(chinese, Does.Contain("button-main-menu-ui-style-classic = 界面风格：经典"));
				Assert.That(chinese, Does.Contain("button-main-menu-ui-style-hd = 界面风格：高清"));
			});
		}

		[Test]
		public void EveryProductionIconConsumerUsesThePreferenceAwarePolicy()
		{
			var root = RepositoryRoot();
			foreach (var relativePath in new[]
			{
				"engine/OpenRA.Mods.Common/Widgets/ProductionPaletteWidget.cs",
				"engine/OpenRA.Mods.Common/Widgets/SupportPowersWidget.cs",
				"engine/OpenRA.Mods.Common/Widgets/ObserverProductionIconsWidget.cs",
				"engine/OpenRA.Mods.Common/Widgets/ObserverSupportPowerIconsWidget.cs",
			})
			{
				var source = File.ReadAllText(Path.Combine(root, relativePath));
				Assert.That(source, Does.Contain("ProductionIconPresentation.ShouldUseHighDefinition("), relativePath);
			}

			var sidebarLogic = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "Ingame", "ClassicProductionLogic.cs"));
			Assert.That(sidebarLogic, Does.Not.Contain("production-icon-hd-"),
				"The preference applies to unit cameos; faction category and scroll controls retain their original art.");
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(
				directory.FullName, "engine", "OpenRA.Mods.Common")))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the engine root.");
		}
	}
}
