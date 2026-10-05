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
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.Mods.Cnc.FileSystem;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailContentImporterTest
	{
		string root;
		string source;
		string content;

		[SetUp]
		public void SetUp()
		{
			root = Path.Combine(Path.GetTempPath(), "openra-retail-import-" + Guid.NewGuid().ToString("N"));
			source = Path.Combine(root, "source");
			content = Path.Combine(root, "Content");
			Directory.CreateDirectory(source);
		}

		[TearDown]
		public void TearDown()
		{
			if (Directory.Exists(root))
				Directory.Delete(root, true);
		}

		string WriteSource(string name, string value = "data")
		{
			if (Path.GetExtension(name).Equals(".mix", StringComparison.OrdinalIgnoreCase))
			{
				var entryName = Path.GetFileName(name).ToLowerInvariant() switch
				{
					"ra2.mix" => "local.mix",
					"language.mix" => "audio.mix",
					"ra2md.mix" => "localmd.mix",
					"langmd.mix" => "audiomd.mix",
					_ => "payload.bin",
				};
				return WriteMixSource(name, entryName);
			}

			var path = Path.Combine(source, name);
			File.WriteAllText(path, value);
			return path;
		}

		string WriteMixSource(string name, string entryName = "payload.bin", bool entryOutsideArchive = false)
		{
			var path = Path.Combine(source, name);
			using var stream = File.Create(path);
			using var writer = new BinaryWriter(stream);
			writer.Write((ushort)0);
			writer.Write((ushort)0);
			writer.Write((ushort)1);
			writer.Write((uint)1);
			writer.Write(PackageEntry.HashFilename(entryName, PackageHashType.CRC32));
			writer.Write(entryOutsideArchive ? 1000u : 0u);
			writer.Write(1u);
			writer.Write((byte)42);
			return path;
		}

		RetailImportRequest BaseRequest(params string[] extras)
		{
			var files = new[] { WriteSource("ra2.mix"), WriteSource("language.mix") }.Concat(extras);
			return new RetailImportRequest(files, content, long.MaxValue);
		}

		[Test]
		public async Task CompleteBaseContentIsPublishedWithHashes()
		{
			var request = BaseRequest();
			using var sha = SHA256.Create();
			using var input = File.OpenRead(request.SourceFiles.Single(path =>
				Path.GetFileName(path).Equals("ra2.mix", StringComparison.OrdinalIgnoreCase)));
			var expected = BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
			var result = await new RetailContentImporter().ImportAsync(request, CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.None));
			Assert.That(File.Exists(Path.Combine(content, "ra2", "ra2.mix")), Is.True);
			Assert.That(result.Hashes["ra2.mix"], Is.EqualTo(expected));
			Assert.That(result.Status.CanEnterGame, Is.False);
		}

		[Test]
		public async Task ImportProgressCountsActualBytesAndMarksUnmeasuredFinalization()
		{
			var request = BaseRequest();
			long lastCopied = 0;
			var finalized = false;
			var expectedTotal = request.SourceFiles.Sum(path => new FileInfo(path).Length);
			var result = await new RetailContentImporter().ImportAsync(request, CancellationToken.None, (copied, total) =>
			{
				if (total == 0)
				{
					finalized = true;
					return;
				}
				Assert.That(total, Is.EqualTo(expectedTotal));
				Assert.That(copied, Is.InRange(lastCopied, total));
				lastCopied = copied;
			});
			Assert.That(result.Published, Is.True);
			Assert.That(lastCopied, Is.EqualTo(expectedTotal));
			Assert.That(finalized, Is.True);
		}

		[Test]
		public async Task ASecondImportAddsMapArchivesWithoutDiscardingExistingContent()
		{
			var importer = new RetailContentImporter();
			Assert.That((await importer.ImportAsync(BaseRequest(), CancellationToken.None)).Published, Is.True);

			var mapArchive = WriteSource("multimd.mix", "maps");
			var result = await importer.ImportAsync(
				new RetailImportRequest(new[] { mapArchive }, content, long.MaxValue), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(result.Status.CanEnterGame, Is.True);
			Assert.That(File.Exists(Path.Combine(content, "ra2", "ra2.mix")), Is.True);
			Assert.That(File.Exists(Path.Combine(content, "ra2", "language.mix")), Is.True);
			Assert.That(File.Exists(Path.Combine(content, "ra2", "multimd.mix")), Is.True);
		}

		[Test]
		public async Task RetailArchiveNamesArePublishedWithCanonicalLowercaseCasing()
		{
			var result = await new RetailContentImporter().ImportAsync(
				new RetailImportRequest(new[]
				{
					WriteSource("RA2.MIX"),
					WriteSource("LANGUAGE.MIX"),
					WriteSource("MULTIMD.MIX"),
				}, content, long.MaxValue), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(Directory.EnumerateFiles(Path.Combine(content, "ra2"))
				.Select(Path.GetFileName), Is.EquivalentTo(new[]
				{
					"ra2.mix", "language.mix", "multimd.mix",
				}));
			Assert.That(result.Hashes.Keys, Is.EquivalentTo(new[]
			{
				"ra2.mix", "language.mix", "multimd.mix",
			}));
		}

		[Test]
		public void ExistingRetailArchivesAreMigratedToCanonicalLowercaseCasing()
		{
			var retailRoot = Path.Combine(content, "ra2");
			Directory.CreateDirectory(retailRoot);
			File.WriteAllText(Path.Combine(retailRoot, "RA2.MIX"), "base");
			File.WriteAllText(Path.Combine(retailRoot, "Multi.mix"), "maps");

			var normalize = typeof(RetailContentImporter).GetMethod(
				"NormalizeInstalledContent", BindingFlags.Public | BindingFlags.Static);
			Assert.That(normalize, Is.Not.Null,
				"Installed content needs an upgrade path that does not require re-importing retail files.");
			normalize.Invoke(null, new object[] { content });

			Assert.That(Directory.EnumerateFiles(retailRoot).Select(Path.GetFileName),
				Is.EquivalentTo(new[] { "ra2.mix", "multi.mix" }));
			Assert.That(File.ReadAllText(Path.Combine(retailRoot, "ra2.mix")), Is.EqualTo("base"));
			Assert.That(File.ReadAllText(Path.Combine(retailRoot, "multi.mix")), Is.EqualTo("maps"));
		}

		[TestCase("payload.dylib")]
		[TestCase("payload.exe")]
		[TestCase("payload.dll")]
		public async Task ExecutableExtensionsAreRejected(string name)
		{
			var result = await new RetailContentImporter().ImportAsync(
				BaseRequest(WriteSource(name)), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.ExecutableContent));
			Assert.That(result.Published, Is.False);
		}

		[Test]
		public async Task ExecutableMagicIsRejectedEvenWithMixExtension()
		{
			var executable = Path.Combine(source, "payload.mix");
			File.WriteAllBytes(executable, new byte[] { 0xCF, 0xFA, 0xED, 0xFE, 1, 2, 3, 4 });

			var result = await new RetailContentImporter().ImportAsync(
				BaseRequest(executable), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.ExecutableContent));
		}

		[Test]
		public async Task UnsupportedExtensionsAreRejected()
		{
			var result = await new RetailContentImporter().ImportAsync(
				BaseRequest(WriteSource("notes.txt")), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.UnsupportedFile));
		}

		[TestCase("custom.oramap")]
		[TestCase("custom.zip")]
		public async Task ValidOpenRaMapPackagesAreAccepted(string name)
		{
			var map = WriteOpenRaMapSource(name);
			var result = await new RetailContentImporter().ImportAsync(
				BaseRequest(map), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.None));
			Assert.That(result.Status.CanEnterGame, Is.True);
			Assert.That(File.Exists(Path.Combine(content, "ra2", name)), Is.True);
		}

		[TestCase("broken.oramap")]
		[TestCase("broken.zip")]
		public async Task InvalidOpenRaMapPackagesAreRejectedBeforePublishing(string name)
		{
			var result = await new RetailContentImporter().ImportAsync(
				BaseRequest(WriteSource(name, "not a map package")), CancellationToken.None);

			Assert.That(result.Published, Is.False);
			Assert.That(result.Error, Is.EqualTo(RetailImportError.InvalidArchive));
			Assert.That(Directory.Exists(Path.Combine(content, "ra2")), Is.False);
		}

		[Test]
		public async Task CancellationDoesNotPublishContent()
		{
			using var cancellation = new CancellationTokenSource();
			cancellation.Cancel();

			var result = await new RetailContentImporter().ImportAsync(BaseRequest(), cancellation.Token);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.Cancelled));
			Assert.That(Directory.Exists(Path.Combine(content, "ra2")), Is.False);
		}

		[Test]
		public async Task InsufficientSpaceIsRejectedBeforeCopy()
		{
			var result = await new RetailContentImporter().ImportAsync(
				new RetailImportRequest(BaseRequest().SourceFiles, content, 1), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.InsufficientSpace));
			Assert.That(Directory.Exists(Path.Combine(content, ".staging")), Is.False);
		}

		[Test]
		public async Task DuplicateNamesAreRejectedCaseInsensitively()
		{
			var duplicateDirectory = Path.Combine(source, "duplicate");
			Directory.CreateDirectory(duplicateDirectory);
			var duplicate = Path.Combine(duplicateDirectory, "RA2.MIX");
			File.WriteAllText(duplicate, "duplicate");

			var result = await new RetailContentImporter().ImportAsync(
				BaseRequest(duplicate), CancellationToken.None);

			Assert.That(result.Error, Is.EqualTo(RetailImportError.DuplicateFile));
		}

		[Test]
		public async Task StaleStagingIsCleanedBeforeSuccessfulImport()
		{
			var stale = Path.Combine(content, ".staging", "interrupted");
			Directory.CreateDirectory(stale);
			File.WriteAllText(Path.Combine(stale, "partial.mix"), "partial");

			var result = await new RetailContentImporter().ImportAsync(BaseRequest(), CancellationToken.None);

			Assert.That(result.Published, Is.True);
			Assert.That(Directory.EnumerateFileSystemEntries(Path.Combine(content, ".staging")), Is.Empty);
		}

		[Test]
		public async Task RejectedIncrementalImportPreservesPreviousReadyDirectory()
		{
			var importer = new RetailContentImporter();
			Assert.That((await importer.ImportAsync(BaseRequest(), CancellationToken.None)).Published, Is.True);
			File.WriteAllText(Path.Combine(content, "ra2", "marker.dat"), "previous");
			var executable = Path.Combine(source, "replacement.mix");
			File.WriteAllBytes(executable, new byte[] { 0x4D, 0x5A, 1, 2, 3, 4 });

			var failed = await importer.ImportAsync(
				new RetailImportRequest(new[] { executable }, content, long.MaxValue),
				CancellationToken.None);

			Assert.That(failed.Error, Is.EqualTo(RetailImportError.ExecutableContent));
			Assert.That(File.ReadAllText(Path.Combine(content, "ra2", "marker.dat")), Is.EqualTo("previous"));
		}

		[Test]
		public async Task TruncatedKnownMixArchiveCannotSatisfyRuntimeReadiness()
		{
			var result = await new RetailContentImporter().ImportAsync(
				new RetailImportRequest(new[]
				{
					WriteMixSource("ra2.mix", "local.mix"),
					WriteMixSource("language.mix", "audio.mix"),
					WriteInvalidMixSource("multimd.mix"),
				}, content, long.MaxValue), CancellationToken.None);

			Assert.That(result.Published, Is.False);
			Assert.That(result.Error, Is.Not.EqualTo(RetailImportError.None));
		}

		string WriteInvalidMixSource(string name)
		{
			var path = Path.Combine(source, name);
			File.WriteAllText(path, "not a mix archive");
			return path;
		}

		string WriteOpenRaMapSource(string name)
		{
			var path = Path.Combine(source, name);
			using var archive = System.IO.Compression.ZipFile.Open(
				path, System.IO.Compression.ZipArchiveMode.Create);
			using (var writer = new StreamWriter(archive.CreateEntry("map.yaml").Open()))
				writer.Write(
					"MapFormat: 12\nRequiresMod: ra2\nPlayers:\n" +
					"\tPlayerReference@Multi0:\n\t\tPlayable: True\n" +
					"\tPlayerReference@Multi1:\n\t\tPlayable: True\n" +
					"Actors:\n\tActor0: mpspawn\n\tActor1: mpspawn\n");

			archive.CreateEntry("map.bin");
			return path;
		}

		[Test]
		public void MixReaderRejectsEntriesOutsideTheArchiveDataArea()
		{
			Assert.Throws<InvalidDataException>(() =>
			{
				using var stream = File.OpenRead(WriteMixSource("invalid.mix", entryOutsideArchive: true));
				using var _ = new MixLoader.MixFile(stream, "invalid.mix", new[] { "payload.bin" });
			});
		}

		[Test]
		public void MixReaderExposesRawEntryCountWithoutRequiringAFilenameDatabase()
		{
			using var stream = File.OpenRead(WriteMixSource("valid.mix"));
			using var mix = new MixLoader.MixFile(stream, "valid.mix", new[] { "payload.bin" });
			var entryCount = typeof(MixLoader.MixFile).GetProperty("EntryCount");

			Assert.That(entryCount, Is.Not.Null,
				"Retail validation needs the raw archive count even when filenames are unresolved.");
			Assert.That(entryCount?.GetValue(mix), Is.EqualTo(1));
		}
	}
}
