#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	/// <summary>Draws an image aspect-fit inside responsive widget bounds.</summary>
	public sealed class AspectImageWidget : ImageWidget
	{
		public bool AlignLeft;

		public AspectImageWidget() { }

		AspectImageWidget(AspectImageWidget other)
			: base(other)
		{
			AlignLeft = other.AlignLeft;
		}

		public override void Draw()
		{
			var sprite = GetSprite();
			var source = sprite.Size;
			var target = RenderBounds.Size;
			if (source.X <= 0 || source.Y <= 0 || target.Width <= 0 || target.Height <= 0)
				return;

			var scale = Math.Min((float)target.Width / source.X, (float)target.Height / source.Y);
			var size = new Size(
				Math.Max(1, (int)Math.Round(source.X * scale)),
				Math.Max(1, (int)Math.Round(source.Y * scale)));
			var origin = new float2(
				AlignLeft ? RenderBounds.X : RenderBounds.X + (target.Width - size.Width) / 2f,
				RenderBounds.Y + (target.Height - size.Height) / 2f);
			WidgetUtils.DrawSprite(sprite, origin, size);
		}

		public override Widget Clone() { return new AspectImageWidget(this); }
	}
}
