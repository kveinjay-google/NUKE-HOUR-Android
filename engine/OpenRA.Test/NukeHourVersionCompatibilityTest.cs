using System;
using System.Linq;
using NUnit.Framework;
using OpenRA.Network;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NukeHourVersionCompatibilityTest
	{
		static ModMetadata LoadMetadata(string yaml)
		{
			var node = MiniYaml.FromString(yaml, "test metadata").Single();
			return FieldLoader.Load<ModMetadata>(node.Value);
		}

		[Test]
		public void ProductAndCompatibilityVersionsFallBackForExistingMods()
		{
			var metadata = LoadMetadata("Metadata:\n\tVersion: release-1\n");

			Assert.That(metadata.DisplayVersionOrVersion, Is.EqualTo("release-1"));
			Assert.That(metadata.CompatibilityOrVersion, Is.EqualTo("release-1"));
		}

		[Test]
		public void ProductAndCompatibilityVersionsAreIndependent()
		{
			var metadata = LoadMetadata(
				"Metadata:\n\tVersion: nukehour-storage-v1\n" +
				"\tDisplayVersion: NUKE HOUR macOS 1.0.1 (Build 1)\n" +
				"\tCompatibility: nukehour-core-sha256-abc\n");

			Assert.That(metadata.Version, Is.EqualTo("nukehour-storage-v1"));
			Assert.That(metadata.DisplayVersionOrVersion,
				Is.EqualTo("NUKE HOUR macOS 1.0.1 (Build 1)"));
			Assert.That(metadata.CompatibilityOrVersion,
				Is.EqualTo("nukehour-core-sha256-abc"));
		}

		[Test]
		[NonParallelizable]
		public void NativeHostVersionOverridesSharedModDisplayOnly()
		{
			const string key = "OpenRA.HostProductVersion";
			var previous = AppDomain.CurrentDomain.GetData(key);
			try
			{
				var metadata = LoadMetadata(
					"Metadata:\n\tVersion: nukehour-storage-v1\n" +
					"\tDisplayVersion: NUKE HOUR macOS 1.0.18 (Build 18)\n" +
					"\tCompatibility: nukehour-core-sha256-abc\n");
				AppDomain.CurrentDomain.SetData(key, "NUKE HOUR iOS 1.0 (Build 84)");
				Assert.That(metadata.DisplayVersionOrVersion, Is.EqualTo("NUKE HOUR iOS 1.0 (Build 84)"));
				Assert.That(metadata.Version, Is.EqualTo("nukehour-storage-v1"));
				Assert.That(metadata.CompatibilityOrVersion, Is.EqualTo("nukehour-core-sha256-abc"));

				AppDomain.CurrentDomain.SetData(key, null);
				Assert.That(metadata.DisplayVersionOrVersion, Is.EqualTo("NUKE HOUR macOS 1.0.18 (Build 18)"));
			}
			finally
			{
				AppDomain.CurrentDomain.SetData(key, previous);
			}
		}

		[Test]
		public void ServerCompatibilityCanIgnoreProductVersion()
		{
			const string compatibility = "nukehour-core-sha256-abc";
			Assert.That(GameServer.IsAdvertisedModCompatible(
				"ra2", compatibility, "ra2", compatibility, false), Is.True);
		}
	}
}
