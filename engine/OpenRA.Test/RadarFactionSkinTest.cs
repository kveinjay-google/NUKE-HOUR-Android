using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	[NonParallelizable]
	public sealed class RadarFactionSkinTest
	{
		[TestCase("yuri", "radar-yuri", 15, 202)]
		[TestCase("russia", "radar-soviets", 15, 202)]
		[TestCase("america", "radar-allies", 15, 202)]
		public void OfflineRadarUsesItsOwnFactionSkinAndFitsItsWell(string faction, string collection, int x, int width)
		{
			var data = typeof(ChromeMetrics).GetField("data", BindingFlags.NonPublic | BindingFlags.Static);
			var previous = data.GetValue(null);
			try
			{
				data.SetValue(null, new Dictionary<string, string>
				{
					{ "FactionSuffix-yuri", "soviets" },
					{ "RadarFactionSuffix-yuri", "yuri" },
					{ "FactionSuffix-russia", "soviets" },
					{ "FactionSuffix-america", "allies" },
				});
				var image = new ImageWidget { ImageCollection = "radar", ImageName = "insignia",
					Bounds = new WidgetBounds(15, 49, 202, 157) };
				var configure = typeof(IngameRadarDisplayLogic).GetMethod("ConfigureOfflineInsignia");
				Assert.That(configure, Is.Not.Null, "The radar needs an independent faction skin.");
				configure.Invoke(null, new object[] { image, faction });
				Assert.That(image.ImageCollection, Is.EqualTo(collection));
				Assert.That(image.ImageName, Is.EqualTo("insignia"));
				Assert.That(image.Bounds.X, Is.EqualTo(x));
				Assert.That(image.Bounds.Width, Is.EqualTo(width));
				Assert.That(image.Bounds.Height, Is.EqualTo(157));
				Assert.That(image.CoverToFit, Is.EqualTo(faction == "yuri"));
				Assert.That(image.StretchToFit, Is.EqualTo(faction == "yuri"));
				Assert.That(ChromeMetrics.Get<string>("FactionSuffix-yuri"), Is.EqualTo("soviets"));
			}
			finally
			{
				data.SetValue(null, previous);
			}
		}
	}
}
