#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using OpenRA.Primitives;

namespace OpenRA.Mods.RA2.Missions
{
	public static class Ra2MissionMapCoordinates
	{
		public static int2 DecodePacked(int encoded)
		{
			var ry = encoded / 1000;
			var rx = encoded - ry * 1000;
			return new int2(rx, ry);
		}

		public static MPos ToMapPosition(int rx, int ry, int2 fullSize)
		{
			var dx = rx - ry + fullSize.X - 1;
			var dy = rx + ry - fullSize.X - 1;
			return new MPos(dx / 2, dy);
		}
	}
}
