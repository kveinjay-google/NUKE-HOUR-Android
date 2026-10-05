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
	public sealed class BruteBehaviorRulesTest
	{
		[Test]
		public void BruteNeverTakesCoverWhenDamaged()
		{
			var brute = Block(Read("mods/ra2/rules/yuri-infantry.yaml"), "brute");

			StringAssert.Contains("Inherits@1: ^Infantry", brute);
			StringAssert.Contains("Inherits@AUTOTARGET: ^AutoTargetGroundAssaultMove", brute);
			StringAssert.Contains("Mobile:", brute);
			StringAssert.Contains("-TakeCover:", brute);
			StringAssert.DoesNotContain("ScaredyCat:", brute);
		}

		static string Block(string source, string name)
		{
			var marker = name + ":";
			var start = source.IndexOf(marker, StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing block {name}.");
			var end = source.IndexOf("\n\n", start, StringComparison.Ordinal);
			return end < 0 ? source[start..] : source[start..end];
		}

		static string Read(string relativePath)
		{
			return File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath));
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
