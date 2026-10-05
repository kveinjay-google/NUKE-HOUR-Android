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
	/// Landscape layout bucket used by the Android phone UI. Buckets are derived
	/// from usable dp size (not device brand/model).
	/// </summary>
	public enum MobileLayoutProfile
	{
		Desktop,
		CompactPhoneLandscape,
		PhoneLandscape,
		LargePhoneLandscape,
		FoldableLandscape,
		TabletLandscape
	}

	/// <summary>User-facing UI size preference applied to design tokens.</summary>
	public enum MobileUiSizePreference
	{
		Standard100 = 100,
		Comfortable115 = 115,
		Large130 = 130,
		ExtraLarge145 = 145
	}

	/// <summary>
	/// Design tokens in dp/sp. Produced once per layout change from a profile,
	/// a UI-size preference and the system font scale; never queried per frame.
	/// </summary>
	public sealed class MobileUiTokens
	{
		public MobileUiTokens(int minimumTouchTargetDp, int frequentTouchTargetDp, int primaryButtonHeightDp,
			int secondaryButtonHeightDp, int listRowHeightDp, int tabHeightDp, int pagePaddingDp,
			int itemSpacingDp, int sectionSpacingDp, int bodyFontSp, int buttonFontSp, int titleFontSp,
			int captionFontSp, int productionPanelWidthDp, int productionCardWidthDp, int productionCardHeightDp,
			int productionIconWidthDp, int productionIconHeightDp, int productionColumns)
		{
			MinimumTouchTargetDp = minimumTouchTargetDp;
			FrequentTouchTargetDp = frequentTouchTargetDp;
			PrimaryButtonHeightDp = primaryButtonHeightDp;
			SecondaryButtonHeightDp = secondaryButtonHeightDp;
			ListRowHeightDp = listRowHeightDp;
			TabHeightDp = tabHeightDp;
			PagePaddingDp = pagePaddingDp;
			ItemSpacingDp = itemSpacingDp;
			SectionSpacingDp = sectionSpacingDp;
			BodyFontSp = bodyFontSp;
			ButtonFontSp = buttonFontSp;
			TitleFontSp = titleFontSp;
			CaptionFontSp = captionFontSp;
			ProductionPanelWidthDp = productionPanelWidthDp;
			ProductionCardWidthDp = productionCardWidthDp;
			ProductionCardHeightDp = productionCardHeightDp;
			ProductionIconWidthDp = productionIconWidthDp;
			ProductionIconHeightDp = productionIconHeightDp;
			ProductionColumns = productionColumns;
		}

		public int MinimumTouchTargetDp { get; }
		public int FrequentTouchTargetDp { get; }
		public int PrimaryButtonHeightDp { get; }
		public int SecondaryButtonHeightDp { get; }
		public int ListRowHeightDp { get; }
		public int TabHeightDp { get; }
		public int PagePaddingDp { get; }
		public int ItemSpacingDp { get; }
		public int SectionSpacingDp { get; }
		public int BodyFontSp { get; }
		public int ButtonFontSp { get; }
		public int TitleFontSp { get; }
		public int CaptionFontSp { get; }
		public int ProductionPanelWidthDp { get; }
		public int ProductionCardWidthDp { get; }
		public int ProductionCardHeightDp { get; }
		public int ProductionIconWidthDp { get; }
		public int ProductionIconHeightDp { get; }
		public int ProductionColumns { get; }
	}
}
