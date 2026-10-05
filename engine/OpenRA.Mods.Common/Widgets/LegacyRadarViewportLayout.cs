using System;
using OpenRA.Primitives;
using OpenRA.Widgets;
namespace OpenRA.Mods.Common.Widgets
{
	public readonly struct RadarViewportLayout
	{
		public float Scale { get; }
		public int2 PreviewOrigin { get; }
		public Rectangle MapRectangle { get; }

		RadarViewportLayout(float scale, int2 previewOrigin, Rectangle mapRectangle)
		{
			Scale = scale;
			PreviewOrigin = previewOrigin;
			MapRectangle = mapRectangle;
		}

		public static RadarViewportLayout Create(Rectangle renderBounds, Rectangle mapBounds)
		{
			var renderWidth = Math.Max(1, renderBounds.Width);
			var renderHeight = Math.Max(1, renderBounds.Height);
			var mapWidth = Math.Max(1, mapBounds.Width);
			var mapHeight = Math.Max(1, mapBounds.Height);
			var scale = Math.Min(renderWidth / (float)mapWidth, renderHeight / (float)mapHeight);
			var width = Math.Min(renderWidth, (int)(scale * mapWidth));
			var height = Math.Min(renderHeight, (int)(scale * mapHeight));
			var origin = new int2((renderWidth - width) / 2, (renderHeight - height) / 2);
			var mapRectangle = new Rectangle(
				renderBounds.X + origin.X, renderBounds.Y + origin.Y, width, height);
			return new RadarViewportLayout(scale, origin, mapRectangle);
		}
	}
}
