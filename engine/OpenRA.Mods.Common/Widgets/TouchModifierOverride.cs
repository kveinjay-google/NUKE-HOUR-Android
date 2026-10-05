#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

namespace OpenRA.Mods.Common.Widgets
{
	public static class TouchModifierOverride
	{
		public static bool Enabled { get; private set; }

		public static void Toggle()
		{
			Enabled = !Enabled;
		}

		public static void Reset()
		{
			Enabled = false;
		}

		public static Modifiers Apply(Modifiers modifiers, bool touchPlatform)
		{
			return touchPlatform && Enabled ? modifiers | Modifiers.Ctrl : modifiers;
		}
	}
}
