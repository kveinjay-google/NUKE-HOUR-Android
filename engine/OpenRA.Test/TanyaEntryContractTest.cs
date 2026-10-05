using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using OpenRA.Mods.RA2.Missions;

namespace OpenRA.Test
{
	[TestFixture]
	public class TanyaEntryContractTest
	{
		[Test]
		public void LandRouteStopsBeforeWaterAndGoesAroundObstacles()
		{
			var start = new CPos(0, 0);
			var waterWaypoint = new CPos(4, 0);
			var path = TanyaLandEntry.FindPath(start, waterWaypoint,
				c => c.X >= 0 && c.X < 4 && c.Y >= 0 && c.Y < 3 && c != new CPos(1, 0));
			Assert.That(path[0], Is.EqualTo(new CPos(3, 0)));
			Assert.That(path, Does.Not.Contain(waterWaypoint));
			Assert.That(path, Does.Not.Contain(new CPos(1, 0)));
			Assert.That(path[path.Count - 1], Is.EqualTo(new CPos(0, 1)));
			for (var i = 1; i < path.Count; i++)
				Assert.That((path[i] - path[i - 1]).LengthSquared, Is.EqualTo(1));
		}

		[Test]
		public void IsolatedEntryDoesNotInventRouteThroughWater()
		{
			Assert.That(TanyaLandEntry.FindPath(new CPos(0, 0), new CPos(5, 0), _ => false), Is.Empty);
		}
		[Test]
		public void EntryNeverUsesAmphibiousShortcutToWaterWaypoint()
		{
			var text = File.ReadAllText(Path.Combine(Root(), "OpenRA.Mods.RA2/Traits/RetailCampaignRuntime.cs"));
			StringAssert.Contains("TanyaLandEntry.FindPath", text);
			StringAssert.Contains("GetTerrainInfo(cell).Type != \"Water\"", text);
			StringAssert.DoesNotContain("mobile.SetCenterPosition(tanya, position)", text,
				"Do not animate straight through terrain from the distant staging position.");
		}
		static string Root([CallerFilePath] string path = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), "../.."));
		[Test]
		public void OpeningHasBoundedEntryAndRenderTimeCameraLock()
		{
			var text = File.ReadAllText(Path.Combine(Root(), "OpenRA.Mods.RA2/Traits/RetailCampaignRuntime.cs"));
			StringAssert.Contains("TickTanyaEntry();", text);
			StringAssert.Contains("void ITickRender.TickRender", text);
			StringAssert.Contains("mobile.ReturnToCell(tanya)", text);
			StringAssert.Contains("FinishTanyaEntry", text);
			StringAssert.Contains("ticks >= tanyaEntryDeadline", text);
		}

		[Test]
		public void VehiclesAndShipsAcceptTanyaExplosivesAndPistolsCannotTargetThem()
		{
			var rules = File.ReadAllText(Path.Combine(Root(), "mods/ra2/rules/defaults.yaml"));
			foreach (var actor in new[] { "^Vehicle:", "^Ship:" })
			{
				var start = rules.IndexOf(actor, System.StringComparison.Ordinal);
				var end = rules.IndexOf("\n^", start + actor.Length, System.StringComparison.Ordinal);
				var section = rules.Substring(start, end - start);
				StringAssert.Contains("Demolishable:", section);
				StringAssert.Contains("AllowedSaboteurs: tany", section);
			}
			var weapons = File.ReadAllText(Path.Combine(Root(), "mods/ra2/weapons/mgs.yaml"));
			StringAssert.Contains("DoublePistols:\n\tInherits: MP5\n\tValidTargets: Infantry", weapons);
		}
	}
}
