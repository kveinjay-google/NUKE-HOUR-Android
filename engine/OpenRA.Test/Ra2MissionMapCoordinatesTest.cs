#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.IO;
using NUnit.Framework;
using OpenRA.Mods.RA2.Missions;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class Ra2MissionMapCoordinatesTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "OpenRA.Mods.RA2")))
				directory = directory.Parent;
			return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
		}

		[TestCase(71062, 62, 71)]
		[TestCase(14063, 63, 14)]
		[TestCase(58107, 107, 58)]
		public void PackedCoordinatesUseRowThousandsAndColumnRemainder(int encoded, int rx, int ry)
		{
			Assert.That(Ra2MissionMapCoordinates.DecodePacked(encoded), Is.EqualTo(new int2(rx, ry)));
		}

		[Test]
		public void FirstMissionTanyaMapsToTheSouthStagingMargin()
		{
			Assert.That(Ra2MissionMapCoordinates.ToMapPosition(62, 108, new int2(50, 64)),
				Is.EqualTo(new MPos(1, 119)));
		}

		[Test]
		public void MissionActorImportUsesFullTileStorageInsteadOfPlayableBounds()
		{
			var importer = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "OpenRA.Mods.RA2", "UtilityCommands", "ImportRA2MapCommand.cs"));

			StringAssert.Contains("if (mission && !map.Tiles.Contains(cell))", importer);
			StringAssert.DoesNotContain("if (mission && !map.Contains(cell))", importer);
		}
	}
}
