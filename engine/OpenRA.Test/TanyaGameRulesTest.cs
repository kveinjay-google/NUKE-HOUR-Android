using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.FileSystem;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Cnc.Traits;
using OpenRA.Mods.RA2.Traits;
using OpenRA.Network;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class TanyaGameRulesTest
	{
		static string Root()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
				directory = directory.Parent;
			return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
		}

		static MiniYaml Actor(string file, string actor) =>
			MiniYaml.FromString(File.ReadAllText(Path.Combine(Root(), "mods/ra2/rules", file)), file)
				.Single(n => n.Key == actor).Value;

		[TestCase(false, false, false, false, "ra2", false)]
		[TestCase(true, true, false, false, "ra2", true)]
		[TestCase(true, true, true, false, "ra2", true)]
		[TestCase(true, true, false, true, "ra2", true)]
		[TestCase(true, true, true, true, "yr", false)]
		public void ImportedContentControlsLobbyDefaultAndAvailability(
			bool ra2, bool language, bool ra2md, bool langmd, string expectedDefault, bool expectedLocked)
		{
			var files = new HashSet<string>();
			if (ra2) files.Add("ra2.mix");
			if (language) files.Add("language.mix");
			if (ra2md) files.Add("ra2md.mix");
			if (langmd) files.Add("langmd.mix");
			var result = Ra2GameRulesInfo.LobbyDefaults(new ContentFiles(files));
			Assert.That(result.DefaultValue, Is.EqualTo(expectedDefault));
			Assert.That(result.Locked, Is.EqualTo(expectedLocked));
		}

		[TestCase(null, false)]
		[TestCase("ra2", false)]
		[TestCase("yr", true)]
		public void SimulationUsesSynchronizedRoomRuleRatherThanLocalContent(string value, bool canDemolishVehicles)
		{
			var settings = new Session.Global();
			if (value != null)
				settings.LobbyOptions["ra2-game-rules"] = new Session.LobbyOptionState { Value = value, PreferredValue = value };
			var restored = Session.Global.Deserialize(settings.Serialize().Value);
			var rules = new Ra2GameRules(restored);
			var enabled = rules.ProvidesPrerequisites.Contains("ruleset.yuris-revenge");
			Assert.That(enabled, Is.EqualTo(canDemolishVehicles));

			var info = FieldLoader.Load<DemolishableInfo>(Actor("defaults.yaml", "^Vehicle").NodeWithKey("Demolishable").Value);
			Assert.That(info.RequiresCondition, Is.Not.Null, "Ground vehicle demolition must be mode gated.");
			var demolition = new Demolishable(info);
			foreach (var observer in demolition.GetVariableObservers())
				observer.Notifier(null, new Dictionary<string, int> { ["yuri-rules"] = enabled ? 1 : 0 });
			Assert.That(demolition.IsTraitDisabled, Is.EqualTo(!canDemolishVehicles));
		}

		[TestCase(0, 0, 1)]
		[TestCase(0, 1, 0)]
		[TestCase(1, 0, 0)]
		[TestCase(2, 0, 0)]
		public void TanyaBatchProductionRespectsOneUnitIncludingSurvivingClone(int owned, int queued, int expected)
		{
			var buildable = FieldLoader.Load<BuildableInfo>(Actor("allied-infantry.yaml", "tany").NodeWithKey("Buildable").Value);
			Assert.That(buildable.BuildLimit, Is.EqualTo(1));
			Assert.That(ProductionBatchPolicy.ResolveOrderCount(5, false, false, queued, queued, 0, 999,
				buildable.BuildLimit, owned), Is.EqualTo(expected));
		}

		[Test]
		public void TanyaAllowsOneSuccessfulCloneEvenWithMultipleCapturedVats()
		{
			var yaml = Actor("allied-infantry.yaml", "tany").NodeWithKey("Cloneable").Value
				.WithNodesAppended(Actor("defaults.yaml", "^Infantry").NodeWithKey("Cloneable").Value.Nodes);
			var info = FieldLoader.Load<CloneableInfo>(yaml);
			var cloneable = new Cloneable(info);
			Assert.That(cloneable.CanClone, Is.True);
			cloneable.NotifyCloneProduced();
			Assert.That(cloneable.CanClone, Is.False);
		}

		[Test]
		public void OrdinaryInfantryStillAllowsMultipleVatsToClone()
		{
			var info = FieldLoader.Load<CloneableInfo>(Actor("defaults.yaml", "^Infantry").NodeWithKey("Cloneable").Value);
			var cloneable = new Cloneable(info);
			cloneable.NotifyCloneProduced();
			cloneable.NotifyCloneProduced();
			Assert.That(cloneable.CanClone, Is.True);
		}

		sealed class ContentFiles : IReadOnlyFileSystem
		{
			readonly HashSet<string> names;
			public ContentFiles(HashSet<string> names) { this.names = names; }
			public bool Exists(string filename) => names.Contains(filename);
			public Stream Open(string filename) => throw new NotSupportedException();
			public bool TryOpen(string filename, out Stream stream) { stream = null; return false; }
			public bool TryGetPackageContaining(string path, out IReadOnlyPackage package, out string filename)
			{ package = null; filename = null; return false; }
			public bool IsExternalFile(string filename) => false;
		}
	}
}
