using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OpenRA.FileSystem;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NukeHourMapIdentityTest
	{
		sealed class Package : IReadOnlyPackage
		{
			readonly Dictionary<string, byte[]> files;
			public Package(IEnumerable<KeyValuePair<string, byte[]>> files) { this.files = files.ToDictionary(f => f.Key, f => f.Value); }
			public string Name => "identity-test";
			public IEnumerable<string> Contents => files.Keys;
			public Stream GetStream(string filename) => new MemoryStream(files[filename], false);
			public bool Contains(string filename) => files.ContainsKey(filename);
			public IReadOnlyPackage OpenPackage(string filename, OpenRA.FileSystem.FileSystem context) => null;
			public void Dispose() { }
		}

		static Dictionary<string, byte[]> Files(string metadata = "RequiresMod: ra2\n", int format = 12) => new()
		{
			["map.yaml"] = Encoding.UTF8.GetBytes($"MapFormat: {format}\n{metadata}"),
			["map.bin"] = new byte[] { 0, 1, 2, 255 },
			["rules.yaml"] = Encoding.UTF8.GetBytes("World:\n"),
			["scripts/start.lua"] = Encoding.UTF8.GetBytes("return 1\n"),
			["map.png"] = new byte[] { 1, 2, 3 }
		};

		static string Uid(IEnumerable<KeyValuePair<string, byte[]>> files) => Map.ComputeUID(new Package(files));

		[Test]
		public void MatchesSharedServerFixture() => Assert.That(Uid(Files()), Is.EqualTo("018fa7896c272002da2319dd6fc71be70f9a4b37"));

		[Test]
		public void PreviewAndArchiveOrderDoNotChangeIdentity()
		{
			var files = Files();
			var expected = Uid(files);
			files["map.png"] = new byte[] { 9, 8 };
			Assert.That(Uid(files.Reverse()), Is.EqualTo(expected));
			files.Remove("map.png");
			Assert.That(Uid(files), Is.EqualTo(expected));
		}

		[TestCase("map.yaml")]
		[TestCase("map.bin")]
		[TestCase("rules.yaml")]
		[TestCase("scripts/start.lua")]
		public void GameplayChangesIdentity(string filename)
		{
			var files = Files();
			var expected = Uid(files);
			files[filename] = files[filename].Concat(new byte[] { 10 }).ToArray();
			Assert.That(Uid(files), Is.Not.EqualTo(expected));
		}

		[Test]
		public void NamesAndFileBoundariesArePartOfIdentity()
		{
			var files = Files();
			files["a.lua"] = Encoding.UTF8.GetBytes("a");
			files["b.lua"] = Encoding.UTF8.GetBytes("bc");
			var expected = Uid(files);
			files["a.lua"] = Encoding.UTF8.GetBytes("ab");
			files["b.lua"] = Encoding.UTF8.GetBytes("c");
			Assert.That(Uid(files), Is.Not.EqualTo(expected));
			expected = Uid(files);
			files["c.lua"] = files["b.lua"];
			files.Remove("b.lua");
			Assert.That(Uid(files), Is.Not.EqualTo(expected));
		}

		[TestCase("RequiresMod: ra2 # comment\n")]
		[TestCase("  RequiresMod :  ra2\n")]
		[TestCase("Title: x\u2028RequiresMod: other\nRequiresMod: ra2\n")]
		[TestCase("Title: x\u2029RequiresMod: other\nRequiresMod: ra2\n")]
		public void RecognizesMiniYamlMetadata(string metadata)
		{
			var files = Files(metadata);
			var expected = Uid(files);
			files["map.png"] = new byte[] { 4 };
			Assert.That(Uid(files.Reverse()), Is.EqualTo(expected));
		}

		[TestCase("utf-16-le", "993525bc5dbf9abf86b45d52a0dd51b7fd4587c5")]
		[TestCase("utf-16-be", "509a6ed5a66156399c4201160954b39ffe23aa84")]
		[TestCase("utf-32-le", "16c39b813e1d465ac7148c7599f07d40a6edc292")]
		[TestCase("utf-32-be", "3762671d17fde10f22672f99c3100924aebc1dfa")]
		public void RecognizesBomEncodedMetadata(string encodingName, string expected)
		{
			var files = Files();
			Encoding encoding = encodingName.StartsWith("utf-16", System.StringComparison.Ordinal)
				? new UnicodeEncoding(encodingName.EndsWith("be", System.StringComparison.Ordinal), true)
				: new UTF32Encoding(encodingName.EndsWith("be", System.StringComparison.Ordinal), true);
			files["map.yaml"] = encoding.GetPreamble().Concat(encoding.GetBytes(Encoding.UTF8.GetString(files["map.yaml"]))).ToArray();
			Assert.That(Uid(files), Is.EqualTo(expected));
			files["map.png"] = new byte[] { 4 };
			Assert.That(Uid(files.Reverse()), Is.EqualTo(expected));
		}

		[TestCase("RequiresMod: ra\n", 12)]
		[TestCase("RequiresMod: ra\n", 11)]
		[TestCase("World:\n\tRequiresMod: ra2\n", 12)]
		[TestCase("RequiresMod: ra2x\n", 12)]
		public void OtherModsKeepLegacyIdentity(string metadata, int format)
		{
			var files = Files(metadata, format);
			var legacy = files.Where(f => format >= 12 || f.Key != "map.png").SelectMany(f => f.Value).ToArray();
			Assert.That(Uid(files), Is.EqualTo(CryptoUtil.SHA1Hash(legacy)));
		}
	}
}
