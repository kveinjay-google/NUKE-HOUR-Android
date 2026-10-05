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

namespace OpenRA.MobileUi
{
	/// <summary>
	/// dp/sp to OpenRA UI-logical-pixel conversion.
	///
	/// Coordinate model (matching the Android host):
	///   - Android dp = physical px / density (density = px per dp).
	///   - OpenRA UI logical pixels are the widget coordinate space. On the
	///     Android host one logical unit is drawn as UiScale physical pixels,
	///     where UiScale = physical width / effective logical width.
	///   - dpToUi(dp)  = dp * density / uiScale
	///   - spToUi(sp)  = sp * density * fontScale / uiScale  (sp already includes
	///     the token font scaling, so no user factor is applied here)
	/// All values are cached per layout change; nothing here reads the platform.
	/// </summary>
	public static class MobileUiMath
	{
		public static float DpToUi(float dp, float density, float uiScale)
		{
			if (uiScale <= 0 || density <= 0)
				return 0;

			return dp * density / uiScale;
		}

		public static float SpToUi(float sp, float density, float fontScale, float uiScale)
		{
			if (uiScale <= 0 || density <= 0 || fontScale <= 0)
				return 0;

			return sp * density * fontScale / uiScale;
		}

		public static int RoundToUiInt(float dp, float density, float uiScale)
			=> (int)Math.Round(DpToUi(dp, density, uiScale));

		public static int RoundToSpUiInt(float sp, float density, float fontScale, float uiScale)
			=> (int)Math.Round(SpToUi(sp, density, fontScale, uiScale));

		/// <summary>Clamp so a target never falls below the minimum touch target (dp).</summary>
		public static float EnsureTouchTarget(float dp, float minimumTouchDp)
			=> Math.Max(dp, minimumTouchDp);
	}
}
