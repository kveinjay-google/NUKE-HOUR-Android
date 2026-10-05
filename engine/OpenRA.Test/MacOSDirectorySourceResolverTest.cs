#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Installer;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class MacOSDirectorySourceResolverTest
	{
		string root;

		[SetUp]
		public void SetUp()
		{
			root = Path.Combine(Path.GetTempPath(), $"nukehour-macos-source-{Guid.NewGuid():N}");
			Directory.CreateDirectory(root);
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(root))
				Directory.Delete(root, true);
		}

		static ModContent.ModSource Source()
		{
			var yaml = MiniYaml.FromString(
				"source: Local Install\n\tType: MacOSDirectory\n\tRequiredFiles: ra2.mix, language.mix\n",
				"MacOSDirectorySourceResolverTest").Single();
			return new ModContent.ModSource(yaml.Value);
		}

		static void Write(string directory, string filename)
		{
			Directory.CreateDirectory(directory);
			File.WriteAllText(Path.Combine(directory, filename), "retail test fixture");
		}

		[Test]
		public void FindsCaseInsensitiveRequiredFilesInNestedDirectory()
		{
			var install = Path.Combine(root, "Games", "Red Alert 2");
			Write(install, "RA2.MIX");
			Write(install, "Language.Mix");

			var found = MacOSDirectorySourceResolver.FindSourcePath(Source(), new[] { root });

			Assert.That(found, Is.EqualTo(install));
		}

		[Test]
		public void DoesNotSearchPastMaximumDepth()
		{
			var install = Path.Combine(root, "one", "two");
			Write(install, "ra2.mix");
			Write(install, "language.mix");

			var found = MacOSDirectorySourceResolver.FindSourcePath(
				Source(), new[] { root }, maximumDepth: 1);

			Assert.That(found, Is.Null);
		}

		[Test]
		public void DoesNotQueueDirectoriesPastMaximumCount()
		{
			var install = Path.Combine(root, "install");
			Write(install, "ra2.mix");
			Write(install, "language.mix");

			var bounded = MacOSDirectorySourceResolver.FindSourcePath(
				Source(), new[] { root }, maximumDirectories: 1);
			var found = MacOSDirectorySourceResolver.FindSourcePath(
				Source(), new[] { root }, maximumDirectories: 2);

			Assert.That(bounded, Is.Null);
			Assert.That(found, Is.EqualTo(install));
		}

		[Test]
		public void DoesNotFollowSymbolicLinkDirectories()
		{
			var outside = Path.Combine(Path.GetTempPath(), $"nukehour-macos-source-target-{Guid.NewGuid():N}");
			try
			{
				Write(outside, "ra2.mix");
				Write(outside, "language.mix");
				Directory.CreateSymbolicLink(Path.Combine(root, "linked-install"), outside);

				var found = MacOSDirectorySourceResolver.FindSourcePath(Source(), new[] { root });

				Assert.That(found, Is.Null);
			}
			finally
			{
				if (Directory.Exists(outside))
					Directory.Delete(outside, true);
			}
		}

		[Test]
		public void CommonRootsAreBoundedAndDeduplicated()
		{
			var roots = MacOSDirectorySourceResolver.CommonSearchRoots("/Users/tester", "/Volumes").ToArray();

			Assert.That(roots, Does.Contain("/Applications"));
			Assert.That(roots, Does.Contain("/Volumes"));
			Assert.That(roots, Does.Contain("/Users/tester/Library/Application Support/Steam/steamapps/common"));
			Assert.That(roots.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(roots.Length));
			Assert.That(roots.Length, Is.LessThanOrEqualTo(10));
		}
	}
}
