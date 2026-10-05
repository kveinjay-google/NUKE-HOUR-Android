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
using OpenRA.MobileUi;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	/// <summary>
	/// Scales a small desktop dialog (e.g. the color chooser) up for Android
	/// phone touch use. The factor is computed once from the dialog's designed
	/// size and the safe content area; every widget's local bounds are then
	/// multiplied so the whole subtree keeps its proportions. Pure math lives
	/// in <see cref="ComputeScaleFactor"/> so it is unit-testable. The desktop
	/// tree itself is never modified outside phone touch mode.
	/// </summary>
	public static class PhoneDialogScaler
	{
		public const float MinimumFactor = 1.25f;
		public const float MaximumFactor = 3f;
		public const float SafeFraction = 0.92f;

		public static bool IsActive
		{
			get
			{
				var service = MobileUiService.Instance;
				return Platform.IsAndroid && service.IsEnabledMobile && PhoneInput.PhoneTouchMode;
			}
		}

		/// <summary>Pure factor computation (usable width/height already in UI px).</summary>
		public static float ComputeScaleFactor(int designedWidth, int designedHeight, int usableWidth, int usableHeight)
		{
			if (designedWidth <= 0 || designedHeight <= 0 || usableWidth <= 0 || usableHeight <= 0)
				return 1f;

			var widthFactor = usableWidth * SafeFraction / designedWidth;
			var heightFactor = usableHeight * SafeFraction / designedHeight;
			var factor = Math.Min(widthFactor, heightFactor);
			// Never enforce the preferred minimum when the safe area is too
			// short: fitting on screen is more important than enlargement.
			factor = Math.Min(MaximumFactor, Math.Max(1f, factor));
			return Math.Max(1f, factor);
		}

		/// <summary>
		/// Scales the subtree rooted at <paramref name="root"/> by the factor
		/// needed to fit it inside the safe content area, then returns the
		/// applied factor (1 when inactive). Only runs on Android phone
		/// profiles; the root's absolute position is left to the caller.
		/// </summary>
		public static float Apply(Widget root)
		{
			if (root == null || !IsActive)
				return 1f;

			var service = MobileUiService.Instance;
			var safe = service.LogicalSafeBounds;
			var factor = ComputeScaleFactor(root.Bounds.Width, root.Bounds.Height, safe.Width, safe.Height);
			if (factor <= 1f)
				return 1f;

			ScaleSubtree(root, factor);
#if DEBUG
			Log.Write("debug", $"phone dialog scaled factor={factor:0.##} designed=({root.Bounds.Width},{root.Bounds.Height})");
#endif
			return factor;
		}

		static void ScaleSubtree(Widget widget, float factor)
		{
			if (widget == null)
				return;

			widget.Bounds = new WidgetBounds(
				(int)Math.Round(widget.Bounds.X * factor),
				(int)Math.Round(widget.Bounds.Y * factor),
				Math.Max(1, (int)Math.Round(widget.Bounds.Width * factor)),
				Math.Max(1, (int)Math.Round(widget.Bounds.Height * factor)));

			foreach (var child in widget.Children)
				ScaleSubtree(child, factor);
		}
	}
}
