#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailCampaignOffMapBuildingTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "engine")))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
		}

		[Test]
		public void FrozenBuildingsInRetailStagingMarginsUseAValidProjectedFallback()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Mods.Common", "Traits", "Modifiers", "FrozenUnderFog.cs"));

			StringAssert.Contains("if (projectedFootprint.Length == 0)", source);
			StringAssert.Contains("(PPos)map.Clamp(init.Self.Location.ToMPos(map))", source);
		}
	}
}
