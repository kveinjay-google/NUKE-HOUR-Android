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
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets
{
	/// <summary>
	/// One-shot phone pass applied right after a desktop-sized window (lobby,
	/// dialogs, etc.) is built: interactive controls whose height is below the
	/// mobile minimum are enlarged visually to the minimum target. Width is
	/// left alone to avoid breaking horizontal layouts; a later guided pass can
	/// reposition overflow if any. No-op on desktop/iOS and on non-mobile
	/// profiles.
	/// </summary>
	public static class MobileUiPhonePass
	{
		public static void EnlargeInteractiveHeights(Widget root)
		{
			var service = MobileUiService.Instance;
			if (!service.IsEnabledMobile || service.Tokens == null)
				return;

			var profile = service.Profile;
			if (profile != MobileLayoutProfile.CompactPhoneLandscape
				&& profile != MobileLayoutProfile.PhoneLandscape
				&& profile != MobileLayoutProfile.LargePhoneLandscape
				&& profile != MobileLayoutProfile.FoldableLandscape)
				return;

			var minHeight = service.RoundDpToUi(service.Tokens.MinimumTouchTargetDp);
			if (minHeight <= 0 || root == null)
				return;

			Walk(root, minHeight);
		}

		static void Walk(Widget widget, int minHeight)
		{
			if (widget == null)
				return;

			if (IsInteractive(widget) && widget.Bounds.Height > 0 && widget.Bounds.Height < minHeight)
				widget.Bounds.Height = minHeight;

			foreach (var child in widget.Children)
				Walk(child, minHeight);
		}

		/// <summary>Enlarge a dropdown option row to the mobile list-row height.</summary>
		public static void EnlargeDropdownItem(ScrollItemWidget item)
		{
			var service = MobileUiService.Instance;
			if (!service.IsEnabledMobile || service.Tokens == null || item == null)
				return;

			var profile = service.Profile;
			if (profile != MobileLayoutProfile.CompactPhoneLandscape
				&& profile != MobileLayoutProfile.PhoneLandscape
				&& profile != MobileLayoutProfile.LargePhoneLandscape
				&& profile != MobileLayoutProfile.FoldableLandscape)
				return;

			var rowHeight = service.RoundDpToUi(Math.Max(service.Tokens.MinimumTouchTargetDp, service.Tokens.ListRowHeightDp));
			if (rowHeight > 0 && item.Bounds.Height > 0 && item.Bounds.Height < rowHeight)
				item.Bounds.Height = rowHeight;
		}

		/// <summary>
		/// Makes the right-edge scrollbar of every scroll list under the root
		/// thick enough to drag with a finger (phone profiles only).
		/// </summary>
		public static void ThickenPhoneScrollbars(Widget root)
		{
			if (!PhoneProfileActive || root == null)
				return;

			var service = MobileUiService.Instance;
			var barWidth = Math.Max(1, service.RoundDpToUi(32));
			var minThumb = Math.Max(1, service.RoundDpToUi(48));
			WalkScrollPanels(root, barWidth, minThumb);
		}

		static bool PhoneProfileActive
		{
			get
			{
				var service = MobileUiService.Instance;
				if (!service.IsEnabledMobile || service.Tokens == null)
					return false;

				switch (service.Profile)
				{
					case MobileLayoutProfile.CompactPhoneLandscape:
					case MobileLayoutProfile.PhoneLandscape:
					case MobileLayoutProfile.LargePhoneLandscape:
					case MobileLayoutProfile.FoldableLandscape:
						return true;
					default:
						return false;
				}
			}
		}

		static void WalkScrollPanels(Widget widget, int barWidth, int minThumb)
		{
			if (widget == null)
				return;

			if (widget is ScrollPanelWidget scroll && scroll.ScrollBar != ScrollBar.Hidden)
			{
				if (barWidth > scroll.ScrollbarWidth)
					scroll.ScrollbarWidth = barWidth;
				if (minThumb > scroll.MinimumThumbSize)
					scroll.MinimumThumbSize = minThumb;
			}

			foreach (var child in widget.Children)
				WalkScrollPanels(child, barWidth, minThumb);
		}

		static bool IsInteractive(Widget widget)
			=> widget is ButtonWidget
				|| widget is DropDownButtonWidget
				|| widget is TextFieldWidget
				|| widget is CheckboxWidget
				|| widget is ScrollItemWidget;
	}
}
