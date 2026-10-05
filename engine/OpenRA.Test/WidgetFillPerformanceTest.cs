using System.Collections.Generic;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class WidgetFillPerformanceTest
	{
		[TestCase(1000, 600, 1f)]
		[TestCase(1434, 660, 0.5f)]
		[TestCase(1180, 820, 2f)]
		public void SolidPanelUsesOneQuadAtEveryUiScale(int width, int height, float scale)
		{
			using var sheet = new Sheet(SheetType.BGRA, new Size(64, 64));
			var sprite = new Sprite(sheet, new Rectangle(3, 3, 1, 1), TextureChannel.RGBA, scale);
			var calls = 0;
			var position = float2.Zero;
			var size = float2.Zero;
			WidgetUtils.FillRectWithSprite(new Rectangle(20, 30, width, height), sprite, (s, p, z) =>
			{
				Assert.That(s, Is.SameAs(sprite));
				calls++;
				position = p;
				size = z;
			});
			Assert.That(calls, Is.EqualTo(1));
			Assert.That(position, Is.EqualTo(new float2(20, 30)));
			Assert.That(size, Is.EqualTo(new float2(width, height)));
		}

		[Test]
		public void PatternedArtworkStillTilesAndClipsLastTile()
		{
			using var sheet = new Sheet(SheetType.BGRA, new Size(64, 64));
			var sprite = new Sprite(sheet, new Rectangle(3, 3, 8, 8), TextureChannel.RGBA);
			var tiles = new List<(float2 Position, float2 Size)>();
			WidgetUtils.FillRectWithSprite(new Rectangle(20, 30, 18, 10), sprite,
				(s, p, z) => tiles.Add((p, z)));
			Assert.That(tiles.Count, Is.EqualTo(6));
			Assert.That(tiles[0], Is.EqualTo((new float2(20, 30), new float2(8, 8))));
			Assert.That(tiles[5], Is.EqualTo((new float2(36, 38), new float2(2, 2))));
		}

		[TestCase(0, 10)]
		[TestCase(10, 0)]
		[TestCase(-1, 10)]
		public void EmptyPanelDoesNotDraw(int width, int height)
		{
			using var sheet = new Sheet(SheetType.BGRA, new Size(64, 64));
			var sprite = new Sprite(sheet, new Rectangle(3, 3, 1, 1), TextureChannel.RGBA);
			WidgetUtils.FillRectWithSprite(new Rectangle(0, 0, width, height), sprite,
				(s, p, z) => Assert.Fail("Empty panel submitted a quad."));
		}
	}
}
