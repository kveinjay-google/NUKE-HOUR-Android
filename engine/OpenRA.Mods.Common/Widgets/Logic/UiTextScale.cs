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
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	/// <summary>
	/// Applies the user's font-size preference (Graphics.FontScale) to every UI
	/// font. On Android phone surfaces that render the widget canvas 1:1 the
	/// mobile canvas scale is folded in so text is as large as it is on
	/// devices where the engine applies its UI scale - this is what keeps
	/// settings text readable now that buttons are touch-sized.
	///
	/// Phone users reported that the old "150%" preset was still a touch small,
	/// so the phone baseline carries an extra 1.5x boost: what used to be 150%
	/// is now the default 100%, and larger 150% / 200% presets sit above it.
	/// </summary>
	public static class UiTextScale
	{
		/// <summary>Extra size boost folded into the phone baseline (old 150% becomes 100%).</summary>
		public const float PhoneBaseBoost = 1.5f;

		/// <summary>
		/// Font device-scale cap used inside the settings screens so their
		/// compact grid keeps readable-but-fitting text even when the global
		/// preference is set very large.
		/// </summary>
		public const float SettingsCapFactor = 2.2f;

		/// <summary>Presets shown in the settings dropdown (multipliers on top of the base scale).</summary>
		public static readonly float[] Presets = { 0.85f, 1f, 1.25f, 1.5f, 2f };

		public static bool PhoneTextScaleActive
		{
			get
			{
				var service = MobileUi.MobileUiService.Instance;
				return Platform.IsAndroid && service.IsEnabledMobile && MobileUi.PhoneInput.PhoneTouchMode;
			}
		}

		/// <summary>Total font device-scale: base (device/window scale, or the
		/// mobile canvas scale when the engine does not scale the window, plus
		/// the phone readability boost) times the user preference.</summary>
		public static float ComputeFactor()
		{
			var service = MobileUi.MobileUiService.Instance;
			var windowScale = Math.Max(0.5f, Game.Renderer?.WindowScale ?? 1f);
			var canvasScale = PhoneTextScaleActive ? Math.Max(0.5f, service.CanvasScale) : 1f;
			var baseScale = Math.Max(windowScale, canvasScale);
			if (PhoneTextScaleActive)
				baseScale *= PhoneBaseBoost;
			return baseScale * Math.Max(0.25f, Game.Settings?.Graphics.FontScale ?? 1f);
		}

		/// <summary>Pushes the factor into every loaded font. Idempotent; fonts
		/// re-render their glyphs lazily, so calling on settings change is cheap.</summary>
		public static void Apply()
		{
			ApplyFactor(ComputeFactor());
		}

		/// <summary>Font factor for the settings screens (capped for the compact grid).</summary>
		public static float SettingsFactor()
		{
			return Math.Min(ComputeFactor(), SettingsCapFactor);
		}

		/// <summary>Applies an explicit font device-scale factor to all fonts.</summary>
		public static void ApplyFactor(float factor)
		{
			var fonts = Game.Renderer?.Fonts;
			if (fonts == null || fonts.Count == 0)
				return;

#if DEBUG
			Log.Write("debug", $"UiTextScale.ApplyFactor factor={factor:0.###}");
#endif
			foreach (var font in fonts.Values)
				font.SetScale(factor);
		}

		/// <summary>Closest preset to the current preference (used to highlight the active option).</summary>
		public static float NearestPreset(float value)
		{
			var best = Presets[0];
			var bestDistance = Math.Abs(value - best);
			foreach (var preset in Presets.Skip(1))
			{
				var distance = Math.Abs(value - preset);
				if (distance < bestDistance)
				{
					best = preset;
					bestDistance = distance;
				}
			}

			return best;
		}

		/// <summary>Sortable, de-duplicated list of presets for the dropdown.</summary>
		public static IReadOnlyList<float> PresetOptions => Presets;
	}
}
