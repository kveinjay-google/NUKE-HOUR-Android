// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is
// made available under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailMapInstallerTest
	{
		string root;

		[SetUp]
		public void SetUp()
		{
			root = Path.Combine(Path.GetTempPath(), "openra-retail-map-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(root);
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(root))
				Directory.Delete(root, true);
		}

		[Test]
		public void PackageWithoutPlayableSlotsIsRejected()
		{
			var path = WriteMap("Players:\n\tPlayerReference@Neutral:\n\t\tName: Neutral\n\t\tOwnsWorld: True\n");

			Assert.That(IsValidOpenRaMap(path), Is.False);
		}

		[Test]
		public void PackageWithTwoPlayableSlotsAndSpawnsIsAccepted()
		{
			var path = WriteMap(
				"Players:\n" +
				"\tPlayerReference@Multi0:\n\t\tName: Multi0\n\t\tPlayable: True\n" +
				"\tPlayerReference@Multi1:\n\t\tName: Multi1\n\t\tPlayable: True\n\n" +
				"Actors:\n" +
				"\tActor0: mpspawn\n\t\tLocation: 1,1\n\t\tOwner: Neutral\n" +
				"\tActor1: mpspawn\n\t\tLocation: 2,2\n\t\tOwner: Neutral\n");

			Assert.That(IsValidOpenRaMap(path), Is.True);
		}

		[Test]
		public void LooseMapDiscoveryIncludesNestedFoldersAndZipPackages()
		{
			var nested = Path.Combine(root, "Steam", "Maps", "Custom");
			Directory.CreateDirectory(nested);
			File.WriteAllText(Path.Combine(nested, "arena.yrm"), "legacy");
			File.WriteAllText(Path.Combine(nested, "package.zip"), "package");
			File.WriteAllText(Path.Combine(nested, "notes.txt"), "ignored");

			var method = typeof(RetailMapInstaller).GetMethod(
				"EnumerateLooseMapPaths", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.That(method, Is.Not.Null);
			var paths = ((System.Collections.Generic.IEnumerable<string>)method.Invoke(
				null, new object[] { root })).Select(Path.GetFileName).ToArray();

			Assert.That(paths, Is.EquivalentTo(new[] { "arena.yrm", "package.zip" }));
		}

		static bool IsValidOpenRaMap(string path)
		{
			var method = typeof(RetailMapInstaller).GetMethod(
				"IsValidOpenRaMap", BindingFlags.NonPublic | BindingFlags.Static);
			Assert.That(method, Is.Not.Null);
			return (bool)method.Invoke(null, new object[] { path });
		}

		string WriteMap(string yaml)
		{
			var path = Path.Combine(root, "test.oramap");
			using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
			using (var writer = new StreamWriter(archive.CreateEntry("map.yaml").Open()))
				writer.Write("MapFormat: 12\n\nRequiresMod: ra2\n\n" + yaml);

			archive.CreateEntry("map.bin");
			return path;
		}
	}
}
