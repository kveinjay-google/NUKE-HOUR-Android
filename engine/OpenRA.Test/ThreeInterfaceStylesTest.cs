using System;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public class ThreeInterfaceStylesTest
	{
		[TestCase(InterfaceStyleMode.Classic, InterfaceStyleMode.Classic)]
		[TestCase(InterfaceStyleMode.ClassicHD, InterfaceStyleMode.ClassicHD)]
		[TestCase(InterfaceStyleMode.ModernHD, InterfaceStyleMode.ClassicHD)]
		public void HiddenModernStyleFallsBackToClassicHd(InterfaceStyleMode style, InterfaceStyleMode expected)
		{
			var original = new GameSettings { InterfaceStyle = style };
			var restored = new GameSettings();
			FieldLoader.Load(restored, FieldSaver.SaveDifferences(original, new GameSettings()));
			Assert.That(restored.EffectiveInterfaceStyle, Is.EqualTo(expected));
			Assert.That(ProductionIconPresentation.IsHighDefinitionEnabled(restored), Is.EqualTo(expected != InterfaceStyleMode.Classic));
		}

		[Test]
		public void VisibleStylesCycleBetweenClassicAndClassicHd()
		{
			var field = typeof(GameSettings).GetField("InterfaceStyle");
			Assert.That(field, Is.Not.Null, "Three styles cannot be represented by the legacy boolean.");
			var settings = new GameSettings();
			var saved = 0;
			Assert.That(field!.GetValue(settings).ToString(), Is.EqualTo("ClassicHD"));
			foreach (var expected in new[] { "Classic", "ClassicHD", "Classic" })
			{
				MainMenuLogic.ToggleInterfaceStyle(settings, () => saved++);
				Assert.That(field!.GetValue(settings).ToString(), Is.EqualTo(expected));
				Assert.That(ProductionIconPresentation.IsHighDefinitionEnabled(settings), Is.EqualTo(expected != "Classic"));
			}
			Assert.That(saved, Is.EqualTo(3));
		}

		[TestCase(false, "Classic")]
		[TestCase(true, "ClassicHD")]
		public void LegacyPreferenceMigratesWithoutChangingAppearance(bool hd, string expected)
		{
			var property = typeof(GameSettings).GetProperty("EffectiveInterfaceStyle");
			Assert.That(property, Is.Not.Null);
			var settings = new GameSettings
			{
				InterfaceStyle = InterfaceStyleMode.Legacy,
				UseHighDefinitionUI = hd
			};
			Assert.That(property!.GetValue(settings).ToString(), Is.EqualTo(expected));
		}

		[Test]
		public void SettingsOnlyExposeFinishedInterfaceStyles()
		{
			Assert.That(MainMenuLogic.VisibleInterfaceStyles(),
				Is.EqualTo(new[] { InterfaceStyleMode.Classic, InterfaceStyleMode.ClassicHD }));
		}
	}
}
