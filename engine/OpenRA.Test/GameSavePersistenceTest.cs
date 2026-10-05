using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public class GameSavePersistenceTest
	{
		string directory;
		[SetUp]
		public void SetUp() => directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "nukehour-save-" + Guid.NewGuid())).FullName;
		[TearDown]
		public void TearDown() => Directory.Delete(directory, true);

		[Test]
		public void PrematureSaveDoesNotDestroyExistingSave()
		{
			var path = Path.Combine(directory, "existing.orasav");
			File.WriteAllText(path, "previous save");
			Assert.Throws<InvalidOperationException>(() => new GameSave().Save(path));
			Assert.That(File.ReadAllText(path), Is.EqualTo("previous save"));
		}

		[TestCase(0)]
		[TestCase(4)]
		[TestCase(11)]
		public void TruncatedSaveIsRejectedAsInvalidData(int length)
		{
			var path = Path.Combine(directory, "broken.orasav");
			File.WriteAllBytes(path, new byte[length]);
			Assert.Throws<InvalidDataException>(() => new GameSave(path));
		}

		[Test]
		public void SavesCanBeLoadedAndSavedAgainWithChineseNamesAndTraitData()
		{
			var path = Path.Combine(directory, "战役一.orasav");
			using (var file = File.Create(path))
			{
				file.Write(GameSave.MetadataMarker);
				file.Write(50);
				file.Write(48);
				file.Write(new byte[Order.SyncHashOrderLength]);
				file.WriteLengthPrefixedString(Encoding.UTF8, new List<MiniYamlNode> { new Session.Global { Map = "test-map" }.Serialize() }.WriteToString());
				file.WriteLengthPrefixedString(Encoding.UTF8, "");
				file.WriteLengthPrefixedString(Encoding.UTF8, "");
				var traitOffset = (int)file.Position;
				file.Write(GameSave.TraitDataMarker);
				file.WriteLengthPrefixedString(Encoding.UTF8, "0:\n\tCamera: 100,200\n");
				file.Write(0);
				file.Write(traitOffset);
				file.Write(GameSave.EOFMarker);
			}

			var save = new GameSave(path);
			save.Save(path);
			var loaded = new GameSave(path);
			Assert.That(loaded.GlobalSettings.Map, Is.EqualTo("test-map"));
			Assert.That(loaded.LastOrdersFrame, Is.EqualTo(50));
			Assert.That(loaded.LastSyncFrame, Is.EqualTo(48));
			Assert.That(loaded.TraitData[0].Nodes[0].Value.Value, Is.EqualTo("100,200"));
			Assert.That(Directory.GetFiles(directory).Length, Is.EqualTo(1));
		}
	}
}
