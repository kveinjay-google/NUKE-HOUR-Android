// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.IO;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class DreadnoughtMissileRulesTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && (!Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
				!Directory.Exists(Path.Combine(directory.FullName, "mods", "ra2"))))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
		}

		[TestCase("mods/ra2/rules/soviet-naval.yaml")]
		[TestCase("engine/mods/ra2/rules/soviet-naval.yaml")]
		public void DreadnoughtMissileDefinesContinuousVoxelOrientation(string relativePath)
		{
			var rules = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
			var actorStart = rules.IndexOf("dmisl:\n", StringComparison.Ordinal);
			var missile = rules.Substring(actorStart);

			StringAssert.Contains("BodyOrientation:\n\t\tQuantizedFacings: 0", missile);
		}

		[TestCase("mods/ra2/rules/soviet-naval.yaml")]
		[TestCase("engine/mods/ra2/rules/soviet-naval.yaml")]
		public void DreadnoughtMissileSubmitsItsVoxelBodyForRendering(string relativePath)
		{
			var rules = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
			var actorStart = rules.IndexOf("dmisl:\n", StringComparison.Ordinal);
			Assert.That(actorStart, Is.GreaterThanOrEqualTo(0));
			var missile = rules.Substring(actorStart);

			StringAssert.Contains("RenderVoxels:", missile);
			StringAssert.Contains("\n\tWithVoxelBody:\n", missile,
				"RenderVoxels only prepares voxel rendering; WithVoxelBody must submit the missile model.");
		}

		[TestCase("mods/ra2/rules/soviet-naval.yaml")]
		[TestCase("engine/mods/ra2/rules/soviet-naval.yaml")]
		public void DreadnoughtHullUsesRedAlert2VoxelNormals(string relativePath)
		{
			var rules = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
			var actorStart = rules.IndexOf("dred:\n", StringComparison.Ordinal);
			var actorEnd = rules.IndexOf("\ndmisl:\n", actorStart, StringComparison.Ordinal);
			Assert.That(actorStart, Is.GreaterThanOrEqualTo(0));
			Assert.That(actorEnd, Is.GreaterThan(actorStart));

			var dreadnought = rules.Substring(actorStart, actorEnd - actorStart);
			StringAssert.Contains("RenderVoxels:", dreadnought);
			StringAssert.DoesNotContain("NormalsPalette: ts-normals", dreadnought,
				"The retail RA2 Dreadnought voxels must use the default Red Alert 2 normals palette.");
		}
	}
}
