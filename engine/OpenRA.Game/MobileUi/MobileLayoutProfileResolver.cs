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

namespace OpenRA.MobileUi
{
	/// <summary>
	/// Resolves usable dp width/height into a landscape layout profile.
	/// Rules follow the Android phone UI task contract:
	///   height &lt; 400dp -> CompactPhoneLandscape
	///   height 400-479dp -> PhoneLandscape
	///   height &gt;= 480dp and width &lt; 840dp -> LargePhoneLandscape
	///   width &gt;= 840dp and height &lt; 600dp -> FoldableLandscape
	///   width &gt;= 840dp and height &gt;= 600dp -> TabletLandscape
	/// Non-mobile (desktop-sized) surfaces resolve to Desktop.
	/// </summary>
	public static class MobileLayoutProfileResolver
	{
		public static MobileLayoutProfile Resolve(float usableWidthDp, float usableHeightDp)
		{
			// Order is deliberate and deterministic:
			//  1. tall + wide enough -> tablet
			//  2. very wide but not tall -> foldable
			//  3. tall but narrower than 840dp -> large phone
			//  4. 400-479dp tall (typical phone landscape) -> phone
			//  5. shorter than 400dp -> compact phone
			if (usableWidthDp >= 900f && usableHeightDp >= 600f)
				return MobileLayoutProfile.TabletLandscape;

			if (usableWidthDp >= 900f && usableHeightDp < 600f)
				return MobileLayoutProfile.FoldableLandscape;

			if (usableHeightDp >= 480f && usableWidthDp < 840f)
				return MobileLayoutProfile.LargePhoneLandscape;

			if (usableHeightDp >= 400f)
				return MobileLayoutProfile.PhoneLandscape;

			if (usableHeightDp >= 300f)
				return MobileLayoutProfile.CompactPhoneLandscape;

			// Very small surfaces or desktop logical canvases fall back.
			return MobileLayoutProfile.Desktop;
		}
	}
}
