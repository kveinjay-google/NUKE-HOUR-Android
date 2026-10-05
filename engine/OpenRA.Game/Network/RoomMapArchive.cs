using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OpenRA.FileSystem;

namespace OpenRA.Network
{
	// Deliberately supports terrain/actor maps, not arbitrary downloadable mods.
	public static class RoomMapArchive
	{
		public const int MaxBytes = 4 * 1024 * 1024;
		public const int MaxExpandedBytes = 16 * 1024 * 1024;
		public const int ChunkBytes = 12 * 1024;
		static readonly HashSet<string> Files = new(StringComparer.Ordinal) { "map.yaml", "map.bin", "map.png" };
		static readonly HashSet<string> RootKeys = new(StringComparer.Ordinal)
		{
			"MapFormat", "RequiresMod", "Title", "Author", "Tileset", "MapSize", "Bounds",
			"Visibility", "Categories", "Players", "Actors", "Rules", "LockPreview"
		};

		public static bool ValidUid(string value) => value?.Length == 40 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
		public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

		sealed class MemoryPackage : IReadOnlyPackage
		{
			readonly Dictionary<string, byte[]> entries;
			public MemoryPackage(Dictionary<string, byte[]> entries) { this.entries = entries; }
			public string Name => "room-map.oramap";
			public IEnumerable<string> Contents => entries.Keys;
			public Stream GetStream(string filename) => entries.TryGetValue(filename, out var bytes) ? new MemoryStream(bytes, false) : null;
			public bool Contains(string filename) => entries.ContainsKey(filename);
			public IReadOnlyPackage OpenPackage(string filename, FileSystem.FileSystem context) => null;
			public void Dispose() { }
		}

		static byte[] ReadBounded(Stream stream, int limit)
		{
			using var output = new MemoryStream();
			var buffer = new byte[8192];
			int count;
			while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
			{
				if (output.Length + count > limit)
					throw new InvalidDataException("Map exceeds transfer limits.");
				output.Write(buffer, 0, count);
			}
			return output.ToArray();
		}

		static int[] Integers(string value, int count)
		{
			var parts = value?.Split(',');
			if (parts == null || parts.Length != count)
				throw new InvalidDataException("Invalid map geometry.");
			var result = new int[count];
			for (var i = 0; i < count; i++)
				if (!int.TryParse(parts[i], out result[i]))
					throw new InvalidDataException("Invalid map geometry.");
			return result;
		}

		static int[] ValidateYaml(byte[] bytes)
		{
			if (bytes.Length > 1024 * 1024)
				throw new InvalidDataException("Map YAML exceeds transfer limits.");
			var text = new UTF8Encoding(false, true).GetString(bytes);
			if (text.Split('\n').Any(line => line.TakeWhile(c => c == '\t').Count() > 12))
				throw new InvalidDataException("Map YAML nesting exceeds transfer limits.");
			var nodes = MiniYaml.FromString(text, "room-map.yaml");
			if (nodes.Select(n => n.Key).Distinct(StringComparer.Ordinal).Count() != nodes.Count ||
				nodes.Any(n => !RootKeys.Contains(n.Key)) ||
				nodes.FirstOrDefault(n => n.Key == "RequiresMod")?.Value.Value != "ra2")
				throw new InvalidDataException("Unsupported map metadata.");
			var metadata = nodes.ToDictionary(n => n.Key, n => n.Value);
			foreach (var key in RootKeys.Where(k => k != "Rules" && k != "LockPreview"))
				if (!metadata.ContainsKey(key))
					throw new InvalidDataException("Incomplete map metadata.");
			var size = Integers(metadata["MapSize"].Value, 2);
			var bounds = Integers(metadata["Bounds"].Value, 4);
			if (size.Any(n => n < 1 || n > 512) || bounds.Any(n => n < 0) || bounds[2] == 0 || bounds[3] == 0 ||
				(long)bounds[0] + bounds[2] > size[0] || (long)bounds[1] + bounds[3] > size[1] ||
				!int.TryParse(metadata["MapFormat"].Value, out var format) || format < 11 || format > 12 ||
				metadata["Actors"].Nodes.Length > 20000 || metadata["Players"].Nodes.Length > MapPlayers.MaximumPlayerCount)
				throw new InvalidDataException("Map geometry or player limits exceeded.");
			var players = metadata["Players"].Nodes.Select(p => p.Value.ToDictionary()).ToArray();
			bool Flag(Dictionary<string, MiniYaml> player, string key) => player.TryGetValue(key, out var value) &&
				bool.TryParse(value.Value, out var enabled) && enabled;
			if (players.Any(p => !p.ContainsKey("Name") || string.IsNullOrWhiteSpace(p["Name"].Value)) ||
				players.Select(p => p["Name"].Value).Distinct(StringComparer.Ordinal).Count() != players.Length ||
				!players.Any(p => Flag(p, "Playable")) || players.Count(p => Flag(p, "OwnsWorld")) != 1)
				throw new InvalidDataException("Map needs playable slots and one world owner.");
			foreach (var root in nodes.Where(n => n.Key == "Rules"))
			{
				if (!string.IsNullOrEmpty(root.Value.Value))
					throw new InvalidDataException("External map rules are not transferable.");
				foreach (var actor in root.Value.Nodes)
				{
					if ((actor.Key != "World" && actor.Key != "^BaseWorld") || !string.IsNullOrEmpty(actor.Value.Value))
						throw new InvalidDataException("Custom actor rules require a separately installed mod.");
					foreach (var trait in actor.Value.Nodes)
						if ((trait.Key.Split('@')[0] != "ElevatedBridgePlaceholder" && trait.Key != "TerrainLighting") ||
							!string.IsNullOrEmpty(trait.Value.Value) || trait.Value.Nodes.Any(n => n.Value.Nodes.Length != 0))
							throw new InvalidDataException("Map scripts and custom traits are not transferable.");
				}
			}
			return size;
		}

		static void ValidateTerrain(byte[] bytes, int[] size)
		{
			if (bytes.Length < 5 || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(1)) != size[0] ||
				BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(3)) != size[1])
				throw new InvalidDataException("Map terrain dimensions mismatch.");
			var cells = size[0] * size[1];
			if (bytes[0] == 1)
			{
				if (bytes.Length != 5 + cells * 5)
					throw new InvalidDataException("Invalid map terrain length.");
			}
			else if (bytes[0] == 2 && bytes.Length >= 17)
			{
				var ranges = new List<(uint Start, long End)>();
				for (var i = 0; i < 3; i++)
				{
					var offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(5 + 4 * i));
					if (offset == 0)
						continue;
					var end = offset + (long)cells * (i == 0 ? 3 : i == 1 ? 1 : 2);
					if (offset < 17 || end > bytes.Length || ranges.Any(r => offset < r.End && end > r.Start))
						throw new InvalidDataException("Invalid map terrain offsets.");
					ranges.Add((offset, end));
				}
				if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(5)) == 0)
					throw new InvalidDataException("Map has no terrain layer.");
			}
			else
				throw new InvalidDataException("Invalid map terrain format.");
		}

		static void ValidatePreview(byte[] png)
		{
			if (png.Length < 45 || png.Length > 2 * 1024 * 1024 ||
				!png.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
				throw new InvalidDataException("Invalid map preview.");
			var position = 8;
			var ended = false;
			var chunks = 0;
			while (position <= png.Length - 12)
			{
				var length = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position, 4));
				var type = Encoding.ASCII.GetString(png, position + 4, 4);
				if (++chunks > 1024 || length > png.Length - position - 12 ||
					(position == 8 ? type != "IHDR" || length != 13 : type == "IHDR"))
					throw new InvalidDataException("Invalid map preview chunk.");
				if (position == 8)
				{
					var width = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4));
					var height = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4));
					if (width == 0 || height == 0 || width > 1024 || height > 1024 || png[24] != 8)
						throw new InvalidDataException("Map preview dimensions exceed limits.");
				}
				position += (int)length + 12;
				if (type == "IEND") { ended = length == 0 && position == png.Length; break; }
			}
			if (!ended)
				throw new InvalidDataException("Incomplete map preview.");
			try { _ = new FileFormats.Png(new MemoryStream(png, false)); }
			catch (Exception e) { throw new InvalidDataException("Invalid map preview data.", e); }
		}

		public static string Validate(byte[] bytes, string expectedUid = null, string expectedSha256 = null)
		{
			if (bytes == null || bytes.Length == 0 || bytes.Length > MaxBytes)
				throw new InvalidDataException("Map exceeds transfer limits.");
			if (expectedSha256 != null && !string.Equals(Hash(bytes), expectedSha256, StringComparison.Ordinal))
				throw new InvalidDataException("Map archive checksum mismatch.");
			var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
			using var zip = new ZipArchive(new MemoryStream(bytes, false), ZipArchiveMode.Read);
			if (zip.Entries.Count is < 2 or > 3)
				throw new InvalidDataException("Map has unsupported files.");
			var expanded = 0;
			foreach (var entry in zip.Entries)
			{
				if (!Files.Contains(entry.FullName) || entries.ContainsKey(entry.FullName) ||
					(entry.ExternalAttributes >> 16 & 0xf000) == 0xa000 ||
					entry.Length > MaxExpandedBytes - expanded)
					throw new InvalidDataException("Unsafe map archive entry.");
				using var input = entry.Open();
				var data = ReadBounded(input, MaxExpandedBytes - expanded);
				expanded += data.Length;
				entries.Add(entry.FullName, data);
			}
			if (!entries.ContainsKey("map.yaml") || !entries.ContainsKey("map.bin"))
				throw new InvalidDataException("Map terrain and metadata are required.");
			int[] size;
			try { size = ValidateYaml(entries["map.yaml"]); }
			catch (Exception e) { throw new InvalidDataException("Invalid map metadata.", e); }
			ValidateTerrain(entries["map.bin"], size);
			if (entries.TryGetValue("map.png", out var png))
				ValidatePreview(png);
			using var package = new MemoryPackage(entries);
			var uid = Map.ComputeUID(package);
			if (expectedUid != null && !string.Equals(uid, expectedUid, StringComparison.Ordinal))
				throw new InvalidDataException("Map gameplay checksum mismatch.");
			return uid;
		}

		public static byte[] Export(IReadOnlyPackage package)
		{
			using var output = new MemoryStream();
			var expanded = 0;
			using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
				foreach (var name in package.Contents.OrderBy(n => n, StringComparer.Ordinal))
				{
					if (!Files.Contains(name))
						throw new InvalidDataException("Map contains files not supported by automatic transfer.");
					using var source = package.GetStream(name);
					var bytes = ReadBounded(source, MaxExpandedBytes - expanded);
					expanded += bytes.Length;
					using var entry = zip.CreateEntry(name).Open();
					entry.Write(bytes);
				}
			var result = output.ToArray();
			Validate(result);
			return result;
		}

		public static void Install(ModData modData, byte[] bytes, string uid, string sha256)
		{
			Validate(bytes, uid, sha256);
			if (modData.MapCache[uid].Status == MapStatus.Available)
				return;
			var root = Path.Combine(Platform.SupportDir, "maps", modData.Manifest.Id, "nukehour-storage-v1");
			Directory.CreateDirectory(root);
			var cachedFiles = new DirectoryInfo(root).EnumerateFiles("room-*.oramap").ToArray();
			if (cachedFiles.Length >= 64 || cachedFiles.Sum(f => f.Length) + bytes.Length > 64L * 1024 * 1024)
				throw new InvalidDataException("Room map cache is full. Remove unused downloaded maps before retrying.");
			var name = "room-" + uid + "-" + Guid.NewGuid().ToString("N") + ".oramap";
			var destination = Path.Combine(root, name);
			var staging = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
			try
			{
				File.WriteAllBytes(staging, bytes);
				File.Move(staging, destination);
				var parent = new Folder(root);
				modData.MapCache.LoadMap(name, parent, MapClassification.User, modData.Manifest.Get<MapGrid>(), null);
				if (modData.MapCache[uid].Status != MapStatus.Available)
				{
					File.Delete(destination);
					throw new InvalidDataException("Received map could not be loaded.");
				}
			}
			finally
			{
				if (File.Exists(staging))
					File.Delete(staging);
			}
		}
	}
}
