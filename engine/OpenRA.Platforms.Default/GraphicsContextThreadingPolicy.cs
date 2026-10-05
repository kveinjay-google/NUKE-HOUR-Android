#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

namespace OpenRA.Platforms.Default
{
	internal static class GraphicsContextThreadingPolicy
	{
		public static bool UseDedicatedThread(PlatformType platform, WindowMode windowMode)
		{
			// SDL Android backs up and restores the current EGL context while pumping
			// lifecycle events. Rendering must own that same thread so surface recreation
			// rebinds the real context instead of saving a null context on the game thread.
			if (platform == PlatformType.iOS || platform == PlatformType.Android)
				return false;

			return platform != PlatformType.Windows || windowMode != WindowMode.Windowed;
		}
	}
}
