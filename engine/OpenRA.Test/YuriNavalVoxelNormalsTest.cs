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
	public sealed class YuriNavalVoxelNormalsTest
	{
		[TestCase("mods/ra2/rules/yuri-naval.yaml", "yhvr")]
		[TestCase("mods/ra2/rules/yuri-naval.yaml", "bsub")]
		[TestCase("mods/ra2/rules/yuri-naval.yaml", "bmisl")]
		[TestCase("engine/mods/ra2/rules/yuri-naval.yaml", "yhvr")]
		[TestCase("engine/mods/ra2/rules/yuri-naval.yaml", "bsub")]
		[TestCase("engine/mods/ra2/rules/yuri-naval.yaml", "bmisl")]
		public void RetailYuriNavalVoxelsUseRedAlert2Normals(string relativePath, string actorName)
		{
			var actor = ActorBlock(File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath)), actorName);
			StringAssert.Contains("RenderVoxels:", actor);
			StringAssert.DoesNotContain("NormalsPalette: ts-normals", actor,
				$"Retail {actorName}.vxl declares Red Alert 2 normals and renders black with the Tiberian Sun table.");
		}

		static string ActorBlock(string source, string actorName)
		{
			var marker = actorName + ":\n";
			var start = source.IndexOf(marker, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing actor {actorName}.");
			var end = source.IndexOf("\n\n", start, StringComparison.Ordinal);
			return end < 0 ? source[start..] : source[start..end];
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !File.Exists(Path.Combine(
				directory.FullName, "packaging", "nukehour-version.json")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}
	}
}
