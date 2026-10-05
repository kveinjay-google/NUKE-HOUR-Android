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

namespace OpenRA.MobileUi
{
	/// <summary>
	/// Runtime cache of the Android screen metrics and the derived tokens /
	/// conversion factors. The Android host updates this once per layout change
	/// (surface size, insets, fold, rotation); nothing reads Android APIs here
	/// and nothing polls per frame.
	/// </summary>
	public sealed class MobileUiService
	{
		public static MobileUiService Instance { get; } = new();

		// Platform metrics, updated by the host on change.
		public int PixelWidth { get; private set; }
		public int PixelHeight { get; private set; }
		public int DrawableWidth { get; private set; }
		public int DrawableHeight { get; private set; }
		public int LogicalWidth { get; private set; }
		public int LogicalHeight { get; private set; }
		public float Density { get; private set; } = 1;
		public float FontScale { get; private set; } = 1;
		public float RefreshRate { get; private set; } = 60;

		// Derived layout state.
		public MobileLayoutProfile Profile { get; private set; } = MobileLayoutProfile.Desktop;
		public float UsableWidthDp { get; private set; }
		public float UsableHeightDp { get; private set; }
		public MobileUiTokens Tokens { get; private set; }
		public MobileUiSizePreference UISize { get; private set; } = MobileUiSizePreference.Standard100;
		public float UiScale { get; private set; } = 1;

		// Safe-area insets (dp) captured by the host: system bars, cutout,
		// gesture navigation.
		public float InsetLeftDp { get; private set; }
		public float InsetTopDp { get; private set; }
		public float InsetRightDp { get; private set; }
		public float InsetBottomDp { get; private set; }

		/// <summary>
		/// Ratio of the actual render (widget) canvas to the mobile logical
		/// canvas. When the engine applies its UI scale to the window the two
		/// canvases match and this is 1; some Android surfaces render 1:1
		/// (engine UI scale left at 1) so the widget canvas is the physical
		/// pixel canvas and every dp conversion has to be scaled up for phone
		/// pages to fill the whole screen.
		/// </summary>
		public float CanvasScale
		{
			get
			{
				if (!IsEnabledMobile || LogicalWidth <= 0)
					return 1f;

				try
				{
					var canvasWidth = Game.Renderer?.Resolution.Width ?? 0;
					if (canvasWidth > 0 && canvasWidth != LogicalWidth)
						return canvasWidth / (float)LogicalWidth;
				}
				catch
				{
					// Renderer not available yet (tests/headless): the logical
					// canvas is the widget canvas.
				}

				return 1f;
			}
		}

		/// <summary>Render (widget) canvas size in UI px.</summary>
		public int CanvasWidth => Math.Max(1, (int)Math.Round(LogicalWidth * CanvasScale));
		public int CanvasHeight => Math.Max(1, (int)Math.Round(LogicalHeight * CanvasScale));

		/// <summary>Set by the Android host; gates phone-only runtime behaviour.</summary>
		public bool IsEnabledMobile { get; set; }

		public event Action LayoutChanged;

		MobileUiService() { }

		/// <summary>Update from the Android host. Safe to call on the UI thread on
		/// layout changes only.</summary>
		public void Update(int pixelWidth, int pixelHeight, int drawableWidth, int drawableHeight,
			int logicalWidth, int logicalHeight, float density, float fontScale, float refreshRate,
			float usableWidthDp, float usableHeightDp, MobileUiSizePreference uiSize,
			float insetLeftDp = 0, float insetTopDp = 0, float insetRightDp = 0, float insetBottomDp = 0)
		{
			PixelWidth = pixelWidth;
			PixelHeight = pixelHeight;
			DrawableWidth = drawableWidth;
			DrawableHeight = drawableHeight;
			LogicalWidth = logicalWidth;
			LogicalHeight = logicalHeight;
			Density = density;
			FontScale = fontScale;
			RefreshRate = refreshRate;
			UsableWidthDp = usableWidthDp;
			UsableHeightDp = usableHeightDp;
			UISize = uiSize;
			InsetLeftDp = insetLeftDp;
			InsetTopDp = insetTopDp;
			InsetRightDp = insetRightDp;
			InsetBottomDp = insetBottomDp;

			Profile = MobileLayoutProfileResolver.Resolve(usableWidthDp, usableHeightDp);
			UiScale = logicalWidth > 0 ? (float)pixelWidth / logicalWidth : 1f;
			Tokens = MobileUiTokensFactory.ApplyUserSize(
				MobileUiTokensFactory.BaseTokens(Profile), uiSize, fontScale);

			LayoutChanged?.Invoke();
		}

		// Convenience conversion helpers using the cached values. These return
		// render-canvas (widget) pixels so full-screen phone pages built from
		// dp tokens fill the actual window on every device, independent of
		// whether the engine's UI scale is applied to the window.

		public float DpToUi(float dp) => MobileUiMath.DpToUi(dp, Density, UiScale) * CanvasScale;
		public float SpToUi(float sp) => MobileUiMath.SpToUi(sp, Density, FontScale, UiScale) * CanvasScale;
		public int RoundDpToUi(float dp) => (int)Math.Round(MobileUiMath.DpToUi(dp, Density, UiScale) * CanvasScale);
		public int RoundSpToUi(float sp) => (int)Math.Round(MobileUiMath.SpToUi(sp, Density, FontScale, UiScale) * CanvasScale);

		/// <summary>
		/// Safe area rectangle in render-canvas (widget) pixels, excluding
		/// system bars / cutout.
		/// </summary>
		public Rectangle LogicalSafeBounds
		{
			get
			{
				var left = (int)Math.Round(DpToUi(InsetLeftDp));
				var top = (int)Math.Round(DpToUi(InsetTopDp));
				var width = (int)Math.Round(CanvasWidth - DpToUi(InsetLeftDp + InsetRightDp));
				var height = (int)Math.Round(CanvasHeight - DpToUi(InsetTopDp + InsetBottomDp));
				return new Rectangle(left, top, Math.Max(0, width), Math.Max(0, height));
			}
		}
	}
}
