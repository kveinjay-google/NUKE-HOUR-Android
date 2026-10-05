using System;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.LoadScreens;

namespace OpenRA.Test
{
	[TestFixture]
	public class SplashWallpaperSelectionTest
	{
		[Test]
		public void FirstLaunchCanChooseEveryImageAndSingleImageNeedsNoRandomDraw()
		{
			var type = typeof(SheetLoadScreen).Assembly.GetType("OpenRA.Mods.Common.LoadScreens.SplashWallpaperSelection");
			Assert.That(type, Is.Not.Null);
			var select = type.GetMethod("SelectIndex");
			for (var i = 0; i < 9; i++)
			{
				var value = i;
				Assert.That(select.Invoke(null, new object[] { 9, -1, new Func<int, int>(bound => { Assert.That(bound, Is.EqualTo(9)); return value; }) }), Is.EqualTo(i));
			}
			Assert.That(select.Invoke(null, new object[] { 1, 0, new Func<int, int>(_ => throw new Exception("Single image must not draw")) }), Is.EqualTo(0));
		}

		[Test]
		public void EveryAlternateWallpaperIsReachableWithoutRepeatingPrevious()
		{
			var type = typeof(SheetLoadScreen).Assembly.GetType("OpenRA.Mods.Common.LoadScreens.SplashWallpaperSelection");
			Assert.That(type, Is.Not.Null);
			var select = type.GetMethod("SelectIndex");
			for (var previous = 0; previous < 9; previous++)
				for (var draw = 0; draw < 8; draw++)
				{
					var value = draw;
					var selected = (int)select.Invoke(null, new object[] { 9, previous, new Func<int, int>(bound => { Assert.That(bound, Is.EqualTo(8)); return value; }) });
					Assert.That(selected, Is.InRange(0, 8));
					Assert.That(selected, Is.Not.EqualTo(previous));
					Assert.That(selected, Is.EqualTo(draw < previous ? draw : draw + 1));
				}
		}
	}
}
