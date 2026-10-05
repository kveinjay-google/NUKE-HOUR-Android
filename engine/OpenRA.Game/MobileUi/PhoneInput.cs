#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.MobileUi
{
	/// <summary>
	/// Shared helpers that enlarge the *hit* rectangle of small visual icons to
	/// the mobile minimum touch target without changing what is drawn. Visual
	/// bounds stay untouched; only input hit-testing (EventBounds style) grows,
	/// clamped to the owning widget's bounds. Desktop and non-mobile profiles
	/// are no-ops.
	/// </summary>
	public static class PhoneInput
	{
		public static bool PhoneTouchMode
		{
			get
			{
				var service = MobileUiService.Instance;
				return service.IsEnabledMobile
					&& (service.Profile == MobileLayoutProfile.CompactPhoneLandscape
						|| service.Profile == MobileLayoutProfile.PhoneLandscape
						|| service.Profile == MobileLayoutProfile.LargePhoneLandscape
						|| service.Profile == MobileLayoutProfile.FoldableLandscape);
			}
		}

		/// <summary>
		/// Expands <paramref name="visual"/> around its centre until it reaches
		/// the mobile minimum touch target (UI px), then clamps it inside the
		/// owning widget's render bounds. No-op outside phone touch mode.
		/// </summary>
		public static Rectangle ExpandHitRect(Rectangle visual, Widget owner)
		{
			if (!PhoneTouchMode)
				return visual;

			var service = MobileUiService.Instance;
			var min = service.RoundDpToUi(service.Tokens.MinimumTouchTargetDp);

			var left = visual.Left - Math.Max(0, min - visual.Width) / 2;
			var top = visual.Top - Math.Max(0, min - visual.Height) / 2;
			var width = Math.Max(visual.Width, min);
			var height = Math.Max(visual.Height, min);
			var expanded = new Rectangle(left, top, width, height);

			if (owner == null)
				return expanded;

			var bounds = owner.RenderBounds;
			if (bounds.IsEmpty)
				return expanded;

			var cx = expanded.Left;
			var cy = expanded.Top;
			if (cx < bounds.Left)
				cx = bounds.Left;
			if (cy < bounds.Top)
				cy = bounds.Top;
			if (cx + expanded.Width > bounds.Right)
				cx = bounds.Right - expanded.Width;
			if (cy + expanded.Height > bounds.Bottom)
				cy = bounds.Bottom - expanded.Height;

			return new Rectangle(cx, cy, width, height);
		}
	}
}
