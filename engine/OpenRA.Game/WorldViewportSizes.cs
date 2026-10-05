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

namespace OpenRA
{
	public class WorldViewportSizes : IGlobalModData
	{
		public readonly int2 CloseWindowHeights = new(480, 600);
		public readonly int2 MediumWindowHeights = new(600, 900);
		public readonly int2 FarWindowHeights = new(900, 1300);

		public readonly float DefaultScale = 1.0f;
		public readonly float ExtendedZoomScale = 0.8f;
		public readonly float MaxZoomScale = 2.0f;
		public readonly int MaxZoomWindowHeight = 240;
		public readonly bool AllowNativeZoom = true;

		public readonly Size MinEffectiveResolution = new(1024, 720);

		public int2 GetSizeRange(WorldViewport distance)
		{
			return distance == WorldViewport.Close ? CloseWindowHeights
				: distance == WorldViewport.Medium ? MediumWindowHeights
				: FarWindowHeights;
		}

		public float GetMinimumZoom(WorldViewport distance, int windowHeight)
		{
			if (AllowNativeZoom && distance == WorldViewport.Extended)
				return DefaultScale * ExtendedZoomScale;

			if (AllowNativeZoom && distance == WorldViewport.Native)
				return DefaultScale;

			var range = GetSizeRange(distance);
			return CalculateMinimumZoom(windowHeight, range.X, range.Y) * DefaultScale;
		}

		public float GetMaximumZoom(WorldViewport distance, float minimumZoom, int windowHeight)
		{
			// Extended adds a farther step without taking away the existing native-to-close zoom range.
			var zoomBase = distance == WorldViewport.Extended ? DefaultScale : minimumZoom;
			return Math.Min(zoomBase * MaxZoomScale, windowHeight * DefaultScale / MaxZoomWindowHeight);
		}

		static float CalculateMinimumZoom(float windowHeight, float minHeight, float maxHeight)
		{
			// Check the easy case: the native resolution is within the maximum limit.
			// This also catches a user-forced resolution below the minimum window size.
			if (windowHeight <= maxHeight)
				return 1;

			// Find a clean fraction that brings the view within the desired range to reduce aliasing.
			var step = 1f;
			while (true)
			{
				var testZoom = 1f;
				while (true)
				{
					var nextZoom = testZoom + step;
					if (windowHeight < minHeight * nextZoom)
						break;

					testZoom = nextZoom;
				}

				if (windowHeight < maxHeight * testZoom)
					return testZoom;

				step /= 2;
			}
		}
	}
}
