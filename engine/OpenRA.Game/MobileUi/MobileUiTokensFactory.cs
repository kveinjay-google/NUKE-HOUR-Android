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
	/// Builds the design-token set for a layout profile, UI-size preference and
	/// system font scale. Tokens are authored in dp/sp and never depend on a
	/// specific device; conversion to OpenRA logical pixels is done once per
	/// layout change by <see cref="MobileUiService"/>.
	/// </summary>
	public static class MobileUiTokensFactory
	{
		/// <summary>Tokens before the UI-size preference and font scale are applied.</summary>
		public static MobileUiTokens BaseTokens(MobileLayoutProfile profile)
		{
			// Phone profiles: two-column production, large touch targets.
			var compact = profile == MobileLayoutProfile.CompactPhoneLandscape
				|| profile == MobileLayoutProfile.PhoneLandscape
				|| profile == MobileLayoutProfile.LargePhoneLandscape;

			return new MobileUiTokens(
				minimumTouchTargetDp: 48,
				frequentTouchTargetDp: 56,
				primaryButtonHeightDp: 60,
				secondaryButtonHeightDp: 52,
				listRowHeightDp: 56,
				tabHeightDp: 52,
				pagePaddingDp: 14,
				itemSpacingDp: 10,
				sectionSpacingDp: 18,
				bodyFontSp: 16,
				buttonFontSp: 17,
				titleFontSp: 22,
				captionFontSp: 13,
				productionPanelWidthDp: compact ? 200 : 340,
				productionCardWidthDp: compact ? 84 : 96,
				productionCardHeightDp: compact ? 78 : 96,
				productionIconWidthDp: compact ? 62 : 72,
				productionIconHeightDp: compact ? 44 : 56,
				productionColumns: compact ? 2 : 3);
		}

		/// <summary>Scales dp tokens by the user UI-size preference; sp tokens are
		/// additionally multiplied by the system font scale (accessibility). All
		/// scaling is applied here, once per layout change.</summary>
		public static MobileUiTokens ApplyUserSize(MobileUiTokens baseTokens, MobileUiSizePreference preference, float systemFontScale)
		{
			var factor = (int)preference / 100f;
			return new MobileUiTokens(
				ScaleDp(baseTokens.MinimumTouchTargetDp, factor),
				ScaleDp(baseTokens.FrequentTouchTargetDp, factor),
				ScaleDp(baseTokens.PrimaryButtonHeightDp, factor),
				ScaleDp(baseTokens.SecondaryButtonHeightDp, factor),
				ScaleDp(baseTokens.ListRowHeightDp, factor),
				ScaleDp(baseTokens.TabHeightDp, factor),
				ScaleDp(baseTokens.PagePaddingDp, factor),
				ScaleDp(baseTokens.ItemSpacingDp, factor),
				ScaleDp(baseTokens.SectionSpacingDp, factor),
				ScaleSp(baseTokens.BodyFontSp, factor, systemFontScale),
				ScaleSp(baseTokens.ButtonFontSp, factor, systemFontScale),
				ScaleSp(baseTokens.TitleFontSp, factor, systemFontScale),
				ScaleSp(baseTokens.CaptionFontSp, factor, systemFontScale),
				ScaleDp(baseTokens.ProductionPanelWidthDp, factor),
				ScaleDp(baseTokens.ProductionCardWidthDp, factor),
				ScaleDp(baseTokens.ProductionCardHeightDp, factor),
				ScaleDp(baseTokens.ProductionIconWidthDp, factor),
				ScaleDp(baseTokens.ProductionIconHeightDp, factor),
				baseTokens.ProductionColumns);

			static int ScaleDp(int value, float f)
			{
				var scaled = value * f;
				return scaled <= 0 ? 1 : (int)scaled;
			}

			static int ScaleSp(int value, float f, float fontScale)
			{
				var scaled = value * f * fontScale;
				return scaled <= 0 ? 1 : (int)scaled;
			}
		}
	}
}
