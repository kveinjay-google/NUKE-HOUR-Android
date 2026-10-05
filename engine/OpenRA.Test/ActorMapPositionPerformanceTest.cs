// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, licensed under the GNU General Public License v3.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.HitShapes;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class ActorMapPositionPerformanceTest
	{
		const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
		const int MapCells = 100;
		static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
		static object Get(object target, string field) => target.GetType().GetField(field, Fields).GetValue(target);
		static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
		static WPos At(int x, int y) => new WPos(x * 1024 + 512, y * 1024 + 512, 0);

		sealed class Position : IOccupySpace
		{
			public WPos CenterPosition { get; set; }
			public CPos TopLeft => new CPos(CenterPosition.X / 1024, CenterPosition.Y / 1024);
			public (CPos Cell, SubCell SubCell)[] OccupiedCells() => Array.Empty<(CPos, SubCell)>();
		}

		// ActorMap's constructor and all queries/ticks are production code. Only unrelated
		// World construction and mutable actor positions are replaced by minimal state.
		sealed class Fixture
		{
			public readonly World World = Empty<World>();
			public readonly ActorMap Map;
			public readonly List<Actor> Actors = new();
			public readonly List<string> Callbacks = new();
			public readonly List<int> Triggers = new();
			readonly bool legacyRemoval;
			readonly HashSet<Actor> removals;
			readonly Array bins;

			public Fixture(bool legacyRemoval = false)
			{
				this.legacyRemoval = legacyRemoval;
				var map = Empty<Map>();
				Set(map, "Grid", new MapGrid(new MiniYaml(null)));
				Set(map, "<MapSize>k__BackingField", new int2(MapCells, MapCells));
				var shape = new HitShapeInfo();
				Set(shape, "Type", new CircleShape());
				var rules = Empty<Ruleset>();
				Set(rules, "Actors", new ActorInfoDictionary(new Dictionary<string, ActorInfo>
				{
					{ "unit", new ActorInfo("unit", shape) }
				}));
				Set(map, "<Rules>k__BackingField", rules);
				Set(World, "Map", map);
				Map = new ActorMap(World, new ActorMapInfo());
				Set(World, "ActorMap", Map);
				removals = (HashSet<Actor>)Get(Map, "removeActorPosition");
				bins = (Array)Get(Map, "bins");
			}

			public Actor Actor(WPos pos, bool add = true)
			{
				var actor = Empty<Actor>();
				Set(actor, "World", World);
				Set(actor, "ActorID", (uint)Actors.Count + 1);
				Set(actor, "Info", new ActorInfo("unit"));
				Set(actor, "<IsInWorld>k__BackingField", true);
				Set(actor, "<OccupiesSpace>k__BackingField", new Position { CenterPosition = pos });
				Actors.Add(actor);
				if (add)
					Map.AddPosition(actor, actor.OccupiesSpace);
				return actor;
			}

			public void SetPosition(int index, WPos pos) => ((Position)Actors[index].OccupiesSpace).CenterPosition = pos;
			public void Add(int index) => Map.AddPosition(Actors[index], Actors[index].OccupiesSpace);
			public void Remove(int index) => Map.RemovePosition(Actors[index], Actors[index].OccupiesSpace);
			public void Update(int index, WPos pos)
			{
				SetPosition(index, pos);
				Map.UpdatePosition(Actors[index], Actors[index].OccupiesSpace);
			}

			public void InWorld(int index, bool value) => Set(Actors[index], "<IsInWorld>k__BackingField", value);

			public void Trigger(WPos pos, int radius)
			{
				var index = Triggers.Count;
				Triggers.Add(Map.AddProximityTrigger(pos, new WDist(radius), WDist.Zero,
					a => Callbacks.Add($"{index}:enter:{a.ActorID}"), a => Callbacks.Add($"{index}:exit:{a.ActorID}")));
			}

			public void Tick()
			{
				if (legacyRemoval && removals.Count > 0)
				{
					// Frozen original removal pass. The production tick still performs final-position
					// sampling, HashSet insertion order, adds, dirty-trigger processing and callbacks.
					foreach (var bin in bins)
					{
						var actors = (List<Actor>)Get(bin, "Actors");
						if (actors.RemoveAll(removals.Contains) > 0)
							foreach (var trigger in (IEnumerable)Get(bin, "ProximityTriggers"))
								Set(trigger, "Dirty", true);
					}

					removals.Clear();
				}

				((ITick)Map).Tick(null);
			}

			public uint[][] CommittedBins() => bins.Cast<object>()
				.Select(b => ((List<Actor>)Get(b, "Actors")).Select(a => a.ActorID).ToArray()).ToArray();

			public Func<int> CountRemovalChecks()
			{
				var count = 0;
				Set(Map, "actorShouldBeRemoved", new Predicate<Actor>(a => { count++; return removals.Contains(a); }));
				return () => count;
			}
		}

		static readonly (WPos A, WPos B)[] Queries =
		{
			(new WPos(-200000, -200000, 0), new WPos(250000, 250000, 0)), // Full map including off-map clamped bins.
			(new WPos(0, 0, 0), new WPos(10239, 10239, 0)), // One bin.
			(new WPos(9500, 9500, 0), new WPos(22000, 22000, 0)), // Crosses both bin boundaries.
			(new WPos(22000, 22000, 0), new WPos(9500, 9500, 0)), // Reversed endpoints.
			(new WPos(10240, -1, 0), new WPos(10240, 102401, 0)), // Exact inclusive boundary.
			(new WPos(-50000, -50000, 0), new WPos(-1, -1, 0)),
			(new WPos(102400, 102400, 0), new WPos(250000, 250000, 0)),
			(new WPos(-1, -1, 0), new WPos(102401, 102401, 0))
		};

		static void Equal(Fixture actual, Fixture legacy, string phase)
		{
			for (var i = 0; i < Queries.Length; i++)
			{
				var query = Queries[i];
				Assert.That(actual.Map.ActorsInBox(query.A, query.B).Select(a => a.ActorID).ToArray(),
					Is.EqualTo(legacy.Map.ActorsInBox(query.A, query.B).Select(a => a.ActorID).ToArray()), $"{phase}, query {i}");
			}

			// Query filtering can hide stale duplicate memberships, so also compare raw ordered bins.
			Assert.That(actual.CommittedBins(), Is.EqualTo(legacy.CommittedBins()), $"{phase}, committed bins");
			Assert.That(actual.Callbacks, Is.EqualTo(legacy.Callbacks), $"{phase}, ordered proximity callbacks");
		}

		public enum Scenario { Static, Movement, Congestion, Teleport, Death, Transport }

		[Test]
		public void PositionUpdatesMatchFrozenLegacyAcrossPopulationAndWorkloads(
			[Values(100, 300, 600, 1000)] int population, [Values] Scenario scenario)
		{
			var actual = new Fixture();
			var legacy = new Fixture(true);
			foreach (var fixture in new[] { actual, legacy })
			{
				for (var i = 0; i < population; i++)
					fixture.Actor(scenario == Scenario.Congestion ? At(11 + i % 4, 11 + i / 4 % 4) : At(i % 10 * 10 + 1, i / 10 % 10 * 10 + 1));
				fixture.Trigger(At(15, 15), 17000);
				fixture.Trigger(At(50, 50), 38000);
				fixture.Trigger(At(95, 95), 14000);
				fixture.Trigger(new WPos(-5000, -5000, 0), 20000);
			}

			Equal(actual, legacy, "initial queued adds");
			actual.Tick();
			legacy.Tick();
			Equal(actual, legacy, "initial flush");
			for (var tick = 0; tick < 16; tick++)
			{
				foreach (var fixture in new[] { actual, legacy })
				{
					for (var i = 0; i < population; i++)
					{
						switch (scenario)
						{
							case Scenario.Movement:
								fixture.Update(i, At((i % 10 * 10 + tick * 3) % MapCells, (i / 10 % 10 * 10 + tick * 7) % MapCells));
								break;
							case Scenario.Congestion:
								fixture.Update(i, At(9 + (i + tick) % 4, 9 + (i / 4 + tick) % 4));
								break;
							case Scenario.Teleport:
								if ((i + tick) % 7 == 0)
									fixture.Update(i, At((i * 17 + tick * 31) % 140 - 20, (i * 23 + tick * 11) % 140 - 20));
								break;
							case Scenario.Death:
								if (i % 16 == tick)
								{
									fixture.InWorld(i, false);
									fixture.Remove(i);
								}

								break;
							case Scenario.Transport:
								if (i % 8 == tick % 8)
								{
									fixture.InWorld(i, tick >= 8);
									if (tick < 8)
										fixture.Remove(i);
									else
									{
										fixture.SetPosition(i, At((i * 7) % MapCells, (i * 11) % MapCells));
										fixture.Add(i);
									}

								}

								break;
						}
					}

					if (tick == 6)
						fixture.Map.UpdateProximityTrigger(fixture.Triggers[0], At(75, 75), new WDist(22000), WDist.Zero);
					if (tick == 12)
						fixture.Map.RemoveProximityTrigger(fixture.Triggers[2]);
				}

				Equal(actual, legacy, $"{scenario}/{population} tick {tick} before flush");
				actual.Tick();
				legacy.Tick();
				Equal(actual, legacy, $"{scenario}/{population} tick {tick} after flush");
			}
		}

		[Test]
		public void RepeatedAddsAcrossBinsAndMixedRemovalsPreserveDuplicatesAndFinalPosition()
		{
			var actual = new Fixture();
			var legacy = new Fixture(true);
			void Step(string phase, Action<Fixture> action)
			{
				action(actual);
				action(legacy);
				Equal(actual, legacy, phase + " before flush");
				actual.Tick();
				legacy.Tick();
				Equal(actual, legacy, phase + " after flush");
			}

			Step("first adds", f =>
			{
				for (var i = 0; i < 6; i++)
					f.Actor(At(i * 10 + 1, i * 10 + 1));
				f.Actor(At(99, 99), false); // Absent removal target.
				f.Trigger(At(15, 15), 18000);
				f.Trigger(At(55, 55), 18000);
			});
			Step("same bin duplicates", f => { f.Add(0); f.Add(0); });
			Assert.That(actual.CommittedBins().SelectMany(a => a).Count(a => a == 1), Is.EqualTo(2));
			Step("second committed bin", f => { f.SetPosition(0, At(55, 55)); f.Add(0); });
			Step("third committed bin", f => { f.SetPosition(0, new WPos(10240, 10240, 0)); f.Add(0); });
			Assert.That(actual.CommittedBins().SelectMany(a => a).Count(a => a == 1), Is.EqualTo(4));
			Step("remove duplicates before readd", f =>
			{
				f.Remove(0);
				f.Add(0);
				f.SetPosition(0, At(80, 80)); // Position is sampled at tick, after AddPosition.
				f.Remove(6);
				f.Map.RemovePosition(null, null);
				f.Remove(4);
				f.Add(4);
				f.Remove(4); // Add/remove order does not cancel the final add.
			});
			Assert.That(actual.CommittedBins().SelectMany(a => a).Count(a => a == 1), Is.EqualTo(1));
			Step("off-map negative clamp", f => f.Update(0, new WPos(-20481, -20481, 0)));
			Step("off-map positive clamp", f => f.Update(0, new WPos(204800, 204800, 0)));
			Step("permanent remove", f => { f.Remove(0); f.Remove(4); });
			Step("reintroduce removed actor", f => { f.SetPosition(0, At(15, 15)); f.Add(0); });
			Step("remove and update more than once", f => { f.Update(0, At(19, 19)); f.Update(0, At(20, 20)); });
			Step("empty removals", f => { f.Remove(6); f.Map.RemovePosition(null, null); });
		}

		[Test]
		public void ProximityCallbacksCanQueuePositionUpdatesForTheNextTick()
		{
			var actual = new Fixture();
			var legacy = new Fixture(true);
			foreach (var f in new[] { actual, legacy })
			{
				f.Actor(At(1, 1));
				f.Actor(At(80, 80));
				f.Map.AddProximityTrigger(At(1, 1), new WDist(5000), WDist.Zero, a =>
				{
					f.Callbacks.Add("enter:" + a.ActorID);
					if (a.ActorID == 1)
						f.Update(1, At(2, 2));
				}, a => f.Callbacks.Add("exit:" + a.ActorID));
			}

			for (var tick = 0; tick < 3; tick++)
			{
				Equal(actual, legacy, "callback before tick " + tick);
				actual.Tick();
				legacy.Tick();
				Equal(actual, legacy, "callback after tick " + tick);
			}

			Assert.That(actual.Callbacks, Is.EqualTo(new[] { "enter:1", "enter:2" }));
		}

		[TestCase(100)]
		[TestCase(300)]
		[TestCase(600)]
		[TestCase(1000)]
		[Explicit("Experimental recorded-bin candidate not adopted; all-moving CPU and initial allocations regressed. Requires experimental patch.")]
		public void MovingOneActorChecksOnlyItsCommittedBinPopulation(int population)
		{
			var fixture = new Fixture();
			for (var i = 0; i < population; i++)
				fixture.Actor(At(i % 10 * 10 + 1, i / 10 % 10 * 10 + 1));
			fixture.Tick();
			var checks = fixture.CountRemovalChecks();
			fixture.Update(0, At(2, 2)); // Even movement within the same bin performs removal and append.
			fixture.Tick();
			TestContext.WriteLine($"ActorMap removal checks: {checks()}, population={population}, expected bin population={population / 100}");
			Assert.That(checks(), Is.EqualTo(population / 100), "Unrelated bins must not scan their actor lists.");
		}

		[Test, Explicit("Experimental recorded-bin candidate not adopted; all-moving CPU and initial allocations regressed. Requires experimental patch.")]
		public void MembershipListsAreReusedForMovementAndReleasedAfterPermanentRemoval()
		{
			var fixture = new Fixture();
			fixture.Actor(At(1, 1));
			fixture.Actor(At(80, 80), false);
			fixture.Tick();
			var memberships = (Dictionary<Actor, List<int>>)Get(fixture.Map, "committedActorBins");
			var actor = fixture.Actors[0];
			var list = memberships[actor];
			fixture.Add(0);
			fixture.Tick();
			Assert.That(list.Count, Is.EqualTo(1), "Repeated adds preserve actor duplicates but record each bin once.");
			fixture.SetPosition(0, At(55, 55));
			fixture.Add(0);
			fixture.Tick();
			Assert.That(list.Count, Is.EqualTo(2), "All committed bins must be recorded, even without an intervening remove.");
			fixture.Update(0, At(90, 90));
			fixture.Tick();
			Assert.That(memberships[actor], Is.SameAs(list), "Moving actors retain their membership list allocation.");
			Assert.That(list.Count, Is.EqualTo(1));
			fixture.Remove(0);
			fixture.Remove(1);
			fixture.Map.RemovePosition(null, null);
			fixture.Tick();
			Assert.That(memberships.Count, Is.Zero, "Permanently removed actors must not be retained by the membership index.");
			fixture.Add(0);
			fixture.Tick();
			Assert.That(memberships.Count, Is.EqualTo(1));
			Assert.That(memberships[actor], Is.Not.SameAs(list));
			fixture.Remove(0);
			fixture.Tick();
			Assert.That(memberships.Count, Is.Zero);
		}

		public enum MovingPopulation { OneActor, TenPercent, AllActors }

		[Test, Explicit("Informational production ActorMap tick benchmark; run the same fixture against frozen baseline source for comparison.")]
		public void ReportProductionTickCostByMovingPopulation(
			[Values(100, 300, 600, 1000)] int population, [Values] MovingPopulation workload)
		{
			var fixture = new Fixture();
			for (var i = 0; i < population; i++)
				fixture.Actor(At(i % 10 * 10 + 1, i / 10 % 10 * 10 + 1));
			var ticker = (ITick)fixture.Map;
			var timer = new Stopwatch();
			var initialBefore = GC.GetAllocatedBytesForCurrentThread();
			ticker.Tick(null);
			var initialCommitBytes = GC.GetAllocatedBytesForCurrentThread() - initialBefore;
			var movingCount = workload == MovingPopulation.OneActor ? 1 :
				workload == MovingPopulation.TenPercent ? population / 10 : population;
			void Queue(int tick)
			{
				for (var i = 0; i < movingCount; i++)
					fixture.Update(i, At((i % 10 * 10 + tick * 3) % MapCells, (i / 10 % 10 * 10 + tick * 7) % MapCells));
			}

			for (var tick = 0; tick < 40; tick++)
			{
				Queue(tick);
				ticker.Tick(null);
			}

			const int samples = 200;
			long bytes = 0;
			for (var tick = 0; tick < samples; tick++)
			{
				Queue(tick + 40);
				var before = GC.GetAllocatedBytesForCurrentThread();
				timer.Start();
				ticker.Tick(null);
				timer.Stop();
				bytes += GC.GetAllocatedBytesForCurrentThread() - before;
			}

			TestContext.WriteLine($"ActorMap production tick: population={population}, moving={movingCount}, workload={workload}, " +
				$"warmup=40, samples={samples}, initial commit={initialCommitBytes} bytes, warmed={bytes / (double)samples:F2} bytes/tick, " +
				$"tick-only={timer.Elapsed.TotalMilliseconds / samples:F6} ms/tick. CPU time is informational; queueing and queries are excluded.");
		}

		[Test]
		public void WarmedMovementDoesNotAllocatePerTickMembershipCollections()
		{
			var fixture = new Fixture();
			for (var i = 0; i < 1000; i++)
				fixture.Actor(At(i % 10 * 10 + 1, i / 10 % 10 * 10 + 1));
			fixture.Tick();
			for (var tick = 0; tick < 30; tick++)
			{
				fixture.Update(0, At(tick % 2 + 1, 1));
				fixture.Tick();
			}

			var timer = new Stopwatch();
			var before = GC.GetAllocatedBytesForCurrentThread();
			timer.Start();
			for (var tick = 0; tick < 1000; tick++)
			{
				fixture.Update(0, At(tick % 2 + 1, 1));
				fixture.Tick();
			}

			timer.Stop();
			var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
			TestContext.WriteLine($"ActorMap warmed movement: {bytes} bytes/1000 ticks, {timer.Elapsed.TotalMilliseconds:F3} ms/1000 ticks; wall time is informational.");
			Assert.That(bytes, Is.LessThan(1024), "Reuse membership lists and affected-bin scratch storage for moving actors.");
		}
	}
}
