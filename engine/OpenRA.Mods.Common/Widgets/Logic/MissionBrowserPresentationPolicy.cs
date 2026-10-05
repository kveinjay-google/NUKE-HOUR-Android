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

using System.Collections.Generic;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public static class MissionBrowserPresentationPolicy
	{
		public static bool ShowCustomOptions(bool fixedRules) => !fixedRules;

		public static string ResolveOptionValue(
			bool fixedRules, string defaultValue, string rememberedValue, IEnumerable<string> allowedValues)
		{
			if (fixedRules || string.IsNullOrWhiteSpace(rememberedValue))
				return defaultValue;

			foreach (var value in allowedValues)
				if (value == rememberedValue)
					return rememberedValue;

			return defaultValue;
		}
	}
}
