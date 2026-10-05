using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public class RoomMapArchiveTest
	{
		const string ValidYaml = "MapFormat: 12\nRequiresMod: ra2\nTitle: Test\nAuthor: Test\nTileset: TEMPERATE\nMapSize: 4,4\nBounds: 0,0,4,4\nVisibility: Lobby\nCategories: Conquest\nPlayers:\n\tPlayerReference@Neutral:\n\t\tName: Neutral\n\t\tOwnsWorld: True\n\tPlayerReference@Multi0:\n\t\tName: Multi0\n\t\tPlayable: True\nActors:\n";
		static byte[] Archive(string extra = null, string yaml = ValidYaml, byte[] extraData = null, byte[] terrain = null)
		{
			using var stream = new MemoryStream();
			using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
			{
				using (var entry = zip.CreateEntry("map.yaml").Open())
					entry.Write(Encoding.UTF8.GetBytes(yaml));
				using (var entry = zip.CreateEntry("map.bin").Open())
				{
					var data = new byte[85];
					data[0] = 1; data[1] = 4; data[3] = 4;
					entry.Write(terrain ?? data);
				}
				if (extra != null)
					using (var entry = zip.CreateEntry(extra).Open())
						entry.Write(extraData ?? new byte[] { 1 });
			}
			return stream.ToArray();
		}

		[Test]
		public void TerrainMapPassesAndWrongDigestIsRejected()
		{
			var bytes = Archive();
			Assert.That(RoomMapArchive.Validate(bytes).Length, Is.EqualTo(40));
			Assert.Throws<InvalidDataException>(() => RoomMapArchive.Validate(bytes, new string('0', 40)));
			Assert.Throws<InvalidDataException>(() => RoomMapArchive.Validate(bytes, null, new string('0', 64)));
		}

		[TestCase("../map.bin")]
		[TestCase("MAP.YAML")]
		[TestCase("script.lua")]
		[TestCase("assets.mix")]
		public void RejectsPathsDuplicatesAndBundledAssets(string extra)
			=> Assert.Throws<InvalidDataException>(() => RoomMapArchive.Validate(Archive(extra)));

		[TestCase("Rules: evil.yaml\n")]
		[TestCase("Rules:\n\tWorld:\n\t\tLuaScript:\n\t\t\tScripts: evil.lua\n")]
		[TestCase("Sequences: sprites.yaml\n")]
		public void RejectsExternalFilesAndUnsafeRules(string extra)
			=> Assert.Throws<InvalidDataException>(() => RoomMapArchive.Validate(Archive(null,
				"MapFormat: 12\nRequiresMod: ra2\n" + extra)));

		[Test]
		public void RejectsUnboundedPngChunk()
		{
			var png = new byte[24];
			new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0);
			png[8] = 64;
			Encoding.ASCII.GetBytes("IHDR").CopyTo(png, 12);
			png[19] = 1; png[23] = 1;
			Assert.Throws<InvalidDataException>(() => RoomMapArchive.Validate(Archive("map.png", extraData: png)));
		}

		[TestCase("MapSize: 4,4", "MapSize: 60000,60000")]
		[TestCase("Players:", "NotPlayers:")]
		[TestCase("Bounds: 0,0,4,4", "Bounds: 0,0,8000,8000")]
		public void RejectsUnusableMetadata(string oldValue, string newValue)
			=> Assert.Throws<InvalidDataException>(() => RoomMapArchive.Validate(Archive(yaml: ValidYaml.Replace(oldValue, newValue))));

		[Test]
		public void RejectsInvalidTerrainAndExternalFluent()
		{
			Assert.Throws<InvalidDataException>(() => RoomMapArchive.Validate(Archive(terrain: new byte[] { 1, 2, 3 })));
			Assert.Throws<InvalidDataException>(() => RoomMapArchive.Validate(Archive(yaml: ValidYaml + "FluentMessages: ra2|../../private.txt\n")));
		}

		[Test]
		public void OptionalRealImportedMapUsesTheSafeCodec()
		{
			var path = Environment.GetEnvironmentVariable("NUKEHOUR_TEST_ROOM_MAP");
			if (string.IsNullOrEmpty(path))
				Assert.Ignore("Set NUKEHOUR_TEST_ROOM_MAP for the private imported-map fixture.");
			Assert.That(RoomMapArchive.Validate(File.ReadAllBytes(path)), Has.Length.EqualTo(40));
		}

		[Test]
		public void RejectsExpandedZipBomb()
		{
			using var stream = new MemoryStream();
			using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
				using (var entry = zip.CreateEntry("map.yaml").Open())
					entry.Write(new byte[RoomMapArchive.MaxExpandedBytes + 1]);
			Assert.Throws<InvalidDataException>(() => RoomMapArchive.Validate(stream.ToArray()));
		}
	}
}
