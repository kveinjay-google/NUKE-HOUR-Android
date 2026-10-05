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

namespace OpenRA.Graphics
{
	/// <summary>
	/// Converts desktop-oriented logical font metrics into sizes that remain
	/// readable on Android phones rendering the UI at native landscape pixels.
	/// </summary>
	public static class MobileFontSizePolicy
	{
		public const float AndroidScale = 1.8f;
		public const int AndroidMinimumSize = 32;

		public static (int Size, int Ascender) Resolve(int size, int ascender, bool android)
		{
			if (!android)
				return (size, ascender);

			var resolvedSize = Math.Max(AndroidMinimumSize, (int)Math.Round(size * AndroidScale));
			var scale = resolvedSize / (float)size;
			var resolvedAscender = Math.Max(1, (int)Math.Round(ascender * scale));
			return (resolvedSize, resolvedAscender);
		}
	}
}
