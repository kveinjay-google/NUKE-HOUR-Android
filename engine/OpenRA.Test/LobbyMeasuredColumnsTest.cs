using System;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class LobbyMeasuredColumnsTest
	{
		[TestCase(420)]
		[TestCase(650)]
		[TestCase(1000)]
		public void MeasuredColumnsFitTheRowAndKeepTouchTargets(int width)
		{
			var method = typeof(IosLobbyLayout).GetMethod("FitPlayerColumns");
			Assert.That(method, Is.Not.Null);
			var preferred = new[] { 160, 60, 125, 65, 100, 65 };
			var columns = (WidgetBounds[])method.Invoke(null, new object[] { width, 44, preferred });
			Assert.That(columns.Last().Right, Is.EqualTo(width));
			Assert.That(columns.All(c => c.Width >= 44), Is.True);
			for (var i = 1; i < columns.Length; i++)
				Assert.That(columns[i].X, Is.EqualTo(columns[i - 1].Right));
			if (width >= preferred.Sum())
			{
				Assert.That(columns[2].Width, Is.EqualTo(preferred[2]));
				Assert.That(columns[4].Width, Is.EqualTo(preferred[4]));
			}
		}
	}
}
