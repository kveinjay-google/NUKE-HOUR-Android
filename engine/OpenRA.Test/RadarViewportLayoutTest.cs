#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available under the terms of the GNU General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or (at
 * your option) any later version. For more information, see COPYING.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RadarViewportLayoutTest
	{
		[Test]
		public void EnlargedRadarPreviewFollowsItsWidgetAndNeverEscapes()
		{
			var mapBounds = new Rectangle(0, 0, 128, 128);
			var originalBounds = new Rectangle(15, 49, 202, 157);
			var enlargedBounds = new Rectangle(2031, 83, 340, 264);

			var original = RadarViewportLayout.Create(originalBounds, mapBounds);
			var enlarged = RadarViewportLayout.Create(enlargedBounds, mapBounds);

			Assert.Multiple(() =>
			{
				Assert.That(original.MapRectangle, Is.EqualTo(new Rectangle(37, 49, 157, 157)));
				Assert.That(enlarged.MapRectangle, Is.EqualTo(new Rectangle(2069, 83, 264, 264)));
				Assert.That(enlarged.MapRectangle.Width, Is.GreaterThan(original.MapRectangle.Width));
				Assert.That(enlargedBounds.Contains(enlarged.MapRectangle), Is.True);
			});
		}

		[TestCase(2031, 83, 340, 264, 0, 0, 256, 128)]
		[TestCase(2031, 83, 340, 264, 0, 0, 128, 256)]
		[TestCase(2006, 0, 394, 463, 0, 0, 1, 1)]
		public void RadarPreviewAlwaysStaysInsideItsRenderBounds(
			int x, int y, int width, int height,
			int mapX, int mapY, int mapWidth, int mapHeight)
		{
			var renderBounds = new Rectangle(x, y, width, height);
			var layout = RadarViewportLayout.Create(
				renderBounds, new Rectangle(mapX, mapY, mapWidth, mapHeight));

			Assert.That(renderBounds.Contains(layout.MapRectangle), Is.True);
		}
	}
}
