using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.FileSystem;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public class UnitPerformanceStage1Test
	{
		const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
		static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
		static void Set(object target, string name, object value, Type declaring = null) =>
			(declaring ?? target.GetType()).GetField(name, Fields).SetValue(target, value);
		delegate Target Choose(Actor self, AttackBase attack, PlayerRelationship relationships, WDist range, bool move, bool turn);

		sealed class Position : IOccupySpace
		{
			public WPos CenterPosition { get; set; }
			public CPos TopLeft => CPos.Zero;
			public (CPos Cell, SubCell SubCell)[] OccupiedCells() => Array.Empty<(CPos, SubCell)>();
		}

		sealed class Visible : IDefaultVisibility
		{
			public bool IsVisible(Actor self, Player player) => true;
		}

		sealed class StubTargetable : ITargetable
		{
			public BitSet<TargetableType> TargetTypes { get; set; }
			public bool RequiresForceFire => false;
			public bool TargetableBy(Actor self, Actor viewer) => true;
		}

		// Only the broad-phase box query is used by the production ChooseTarget scan.
		sealed class Candidates : IActorMap
		{
			public Actor[] Actors = Array.Empty<Actor>();
			public IEnumerable<Actor> ActorsInBox(WPos a, WPos b) => Actors;
			public IEnumerable<Actor> AllActors() => Actors;
			public IEnumerable<Actor> GetActorsAt(CPos a) => throw new NotSupportedException();
			public IEnumerable<Actor> GetActorsAt(CPos a, SubCell sub) => throw new NotSupportedException();
			public bool HasFreeSubCell(CPos a, bool checkTransient = true) => throw new NotSupportedException();
			public SubCell FreeSubCell(CPos a, SubCell preferredSubCell = SubCell.Any, bool checkTransient = true) => throw new NotSupportedException();
			public SubCell FreeSubCell(CPos a, SubCell sub, Func<Actor, bool> check) => throw new NotSupportedException();
			public bool AnyActorsAt(CPos a) => throw new NotSupportedException();
			public bool AnyActorsAt(CPos a, SubCell sub, bool checkTransient = true) => throw new NotSupportedException();
			public bool AnyActorsAt(CPos a, SubCell sub, Func<Actor, bool> check) => throw new NotSupportedException();
			public void AddInfluence(Actor a, IOccupySpace space) => throw new NotSupportedException();
			public void RemoveInfluence(Actor a, IOccupySpace space) => throw new NotSupportedException();
			public int AddCellTrigger(CPos[] cells, Action<Actor> enter, Action<Actor> exit) => throw new NotSupportedException();
			public IEnumerable<CPos> TriggerPositions() => throw new NotSupportedException();
			public void RemoveCellTrigger(int id) => throw new NotSupportedException();
			public int AddProximityTrigger(WPos p, WDist r, WDist v, Action<Actor> enter, Action<Actor> exit) => throw new NotSupportedException();
			public void RemoveProximityTrigger(int id) => throw new NotSupportedException();
			public void UpdateProximityTrigger(int id, WPos p, WDist r, WDist v) => throw new NotSupportedException();
			public void AddPosition(Actor a, IOccupySpace space) => throw new NotSupportedException();
			public void RemovePosition(Actor a, IOccupySpace space) => throw new NotSupportedException();
			public void UpdatePosition(Actor a, IOccupySpace space) => throw new NotSupportedException();
			public WDist LargestActorRadius => WDist.Zero;
			public WDist LargestBlockingActorRadius => WDist.Zero;
			public void UpdateOccupiedCells(IOccupySpace space) => throw new NotSupportedException();
			public event Action<CPos> CellUpdated { add { } remove { } }
		}

		sealed class Scan
		{
			public readonly World World = Empty<World>();
			public readonly Candidates Map = new Candidates();
			public readonly Player Owner = Empty<Player>();
			public readonly AutoTarget Auto = Empty<AutoTarget>();
			public readonly Actor Self;
			public readonly AttackOmni Attack;
			public readonly Choose Select;
			uint nextId;

			public Scan(params AutoTargetPriorityInfo[] priorities)
			{
				Set(World, "ActorMap", Map);
				Set(World, "SharedRandom", new MersenneTwister(123));
				var traits = typeof(World).GetField("TraitDict", Fields);
				traits.SetValue(World, Activator.CreateInstance(traits.FieldType, true));
				Self = Actor(0);
				Attack = new AttackOmni(Self, new AttackOmniInfo());
				var weapon = new WeaponInfo();
				Set(weapon, "Range", new WDist(100000));
				var info = new ArmamentInfo();
				Set(info, "<WeaponInfo>k__BackingField", weapon);
				Set(info, "TargetRelationships", PlayerRelationship.Ally);
				var arm = new Armament(Self, info);
				Set(arm, "rangeModifiers", Array.Empty<int>());
				Set(Attack, "getArmaments", new Func<IEnumerable<Armament>>(() => new[] { arm }), typeof(AttackBase));
				Set(Auto, "activeTargetPriorities", priorities);
				Select = (Choose)typeof(AutoTarget).GetMethod("ChooseTarget", Fields).CreateDelegate(typeof(Choose), Auto);
			}

			public Actor Actor(int distance, string type = "Ground")
			{
				var actor = Empty<Actor>();
				Set(actor, "World", World);
				Set(actor, "ActorID", ++nextId);
				Set(actor, "<Owner>k__BackingField", Owner);
				Set(actor, "Info", new ActorInfo("unit"));
				Set(actor, "<IsInWorld>k__BackingField", true);
				Set(actor, "<OccupiesSpace>k__BackingField", new Position { CenterPosition = new WPos(distance, 0, 0) });
				Set(actor, "<Targetables>k__BackingField", new ITargetable[] { new StubTargetable { TargetTypes = new BitSet<TargetableType>(type) } });
				Set(actor, "<EnabledTargetablePositions>k__BackingField", Array.Empty<ITargetablePositions>());
				Set(actor, "visibilityModifiers", Array.Empty<IVisibilityModifier>());
				Set(actor, "defaultVisibility", new Visible());
				return actor;
			}

			public Target Run() => Select(Self, Attack, PlayerRelationship.Ally, new WDist(100000), false, true);
		}

		static AutoTargetPriorityInfo Priority(int value, string valid = "Ground", string invalid = null)
		{
			var info = new AutoTargetPriorityInfo();
			Set(info, "Priority", value);
			Set(info, "ValidTargets", new BitSet<TargetableType>(valid));
			if (invalid != null)
				Set(info, "InvalidTargets", new BitSet<TargetableType>(invalid));
			return info;
		}

		[Test]
		public void ScanPreservesPriorityNearestAndFirstEqualDistanceRules()
		{
			var scan = new Scan(Priority(10, "Water"), Priority(2));
			var near = scan.Actor(10);
			var far = scan.Actor(100);
			var first = scan.Actor(80, "Water");
			var tied = scan.Actor(80, "Water");
			var closer = scan.Actor(60, "Water");
			scan.Map.Actors = new[] { far, near, first, tied };
			Assert.That(scan.Run().Actor, Is.SameAs(first));
			scan.Map.Actors = new[] { far, near, first, tied, closer };
			Assert.That(scan.Run().Actor, Is.SameAs(closer));
		}

		[Test]
		public void ScanFiltersBeforeWeaponEvaluationAndResetsPrioritiesForEachCandidate()
		{
			var priority = Priority(1);
			var scan = new Scan(priority);
			var first = scan.Actor(100);
			var second = scan.Actor(10);
			var calls = 0;
			var original = (Func<IEnumerable<Armament>>)typeof(AttackBase).GetField("getArmaments", Fields).GetValue(scan.Attack);
			Set(scan.Attack, "getArmaments", new Func<IEnumerable<Armament>>(() =>
			{
				calls++;
				// The current candidate must already have been eagerly filtered.
				Set(priority, "InvalidTargets", new BitSet<TargetableType>("Ground"));
				return original();
			}), typeof(AttackBase));
			scan.Map.Actors = new[] { first, second };
			Assert.That(scan.Run().Actor, Is.SameAs(first));
			Assert.That(calls, Is.EqualTo(1));
		}

		[Test]
		public void NestedScanCannotOverwriteTheOuterCandidatePriorities()
		{
			var scan = new Scan(Priority(10), Priority(1, "Water"));
			var outer = scan.Actor(100);
			var lowerPriority = scan.Actor(10, "Water");
			var inner = scan.Actor(20, "Water");
			scan.Map.Actors = new[] { outer, lowerPriority };
			var entered = false;
			var original = (Func<IEnumerable<Armament>>)typeof(AttackBase).GetField("getArmaments", Fields).GetValue(scan.Attack);
			Set(scan.Attack, "getArmaments", new Func<IEnumerable<Armament>>(() =>
			{
				if (!entered)
				{
					entered = true;
					var previous = scan.Map.Actors;
					scan.Map.Actors = new[] { inner };
					Assert.That(scan.Run().Actor, Is.SameAs(inner));
					scan.Map.Actors = previous;
				}

				return original();
			}), typeof(AttackBase));
			Assert.That(scan.Run().Actor, Is.SameAs(outer));
		}

		[Test]
		public void PrioritiesAreMaterializedOncePerScanAndRandomSequenceIsUntouched()
		{
			var scan = new Scan();
			var enumerations = 0;
			IEnumerable<AutoTargetPriorityInfo> Active()
			{
				enumerations++;
				yield return Priority(1);
			}

			Set(scan.Auto, "activeTargetPriorities", Active());
			scan.Map.Actors = new[] { scan.Actor(50), scan.Actor(20) };
			Assert.That(scan.Run().Actor, Is.SameAs(scan.Map.Actors[1]));
			Assert.That(enumerations, Is.EqualTo(1));
			Assert.That(scan.World.SharedRandom.Next(), Is.EqualTo(new MersenneTwister(123).Next()));
			scan.Run();
			Assert.That(enumerations, Is.EqualTo(2));
		}

		[Test]
		public void ScanIntervalConsumesExactlyOneRandomDrawAndForcedScanConsumesNone()
		{
			var scan = new Scan(Priority(1));
			Set(scan.Auto, "Info", new AutoTargetInfo(), typeof(ConditionalTrait<AutoTargetInfo>));
			Set(scan.Auto, "ActiveAttackBases", new[] { scan.Attack });
			Set(scan.Auto, "overrideAutoTarget", Array.Empty<IOverrideAutoTarget>());
			scan.Map.Actors = new[] { scan.Actor(50), scan.Actor(20) };
			var expectedRandom = new MersenneTwister(123);
			var expectedInterval = expectedRandom.Next(3, 8);
			Assert.That(scan.Auto.ScanForTarget(scan.Self, false, true).Actor, Is.SameAs(scan.Map.Actors[1]));
			Assert.That(typeof(AutoTarget).GetField("nextScanTime", Fields).GetValue(scan.Auto), Is.EqualTo(expectedInterval));
			Assert.That(scan.World.SharedRandom.TotalCount, Is.EqualTo(expectedRandom.TotalCount));
			Assert.That(scan.Auto.ScanForTarget(scan.Self, false, true).Type, Is.EqualTo(TargetType.Invalid));
			Assert.That(scan.Auto.ScanForTarget(scan.Self, false, true, true).Actor, Is.SameAs(scan.Map.Actors[1]));
			Assert.That(scan.World.SharedRandom.Next(), Is.EqualTo(expectedRandom.Next()));
		}

		[TestCase(100)]
		[TestCase(300)]
		[TestCase(512)]
		[TestCase(600)]
		[TestCase(1000)]
		public void RejectedCandidatesDoNotAllocateASeparatePriorityList(int count)
		{
			var scan = new Scan(Priority(1));
			scan.Map.Actors = Enumerable.Range(1, count).Select(i => scan.Actor(i, "Air")).ToArray();
			for (var i = 0; i < 20; i++)
				scan.Run();
			var start = GC.GetAllocatedBytesForCurrentThread();
			var timer = Stopwatch.StartNew();
			for (var i = 0; i < 100; i++)
				AssertTargetInvalid(scan.Run());
			timer.Stop();
			var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
			TestContext.WriteLine($"AutoTarget rejected candidates: {bytes / 100} bytes/scan, {timer.Elapsed.TotalMilliseconds:F3} ms/100 scans, candidates={count}");
			// Existing trait enumerators and the armament-range target capture still allocate about
			// 160 bytes/candidate. This budget detects the removed priority filtering allocations
			// (baseline: 376 bytes/candidate) without requiring unrelated hot-path changes.
			Assert.That(bytes / 100, Is.LessThan(count * 200 + 1024));
		}

		static void AssertTargetInvalid(Target target)
		{
			if (target.Type != TargetType.Invalid)
				throw new InvalidOperationException("Unexpected target");
		}

		sealed class Files : IReadOnlyFileSystem
		{
			public Stream Open(string filename) => new MemoryStream();
			public bool TryOpen(string filename, out Stream stream) { stream = filename == "missing" ? null : Open(filename); return stream != null; }
			public bool Exists(string filename) => filename != "missing";
			public bool IsExternalFile(string filename) => false;
			public bool TryGetPackageContaining(string path, out IReadOnlyPackage package, out string filename) { package = null; filename = path; return false; }
		}

		sealed class Frame : ISpriteFrame
		{
			public SpriteFrameType Type => SpriteFrameType.Indexed8;
			public Size Size => new Size(0, 0);
			public Size FrameSize => new Size(0, 0);
			public float2 Offset => float2.Zero;
			public byte[] Data => Array.Empty<byte>();
			public bool DisableExportPadding => false;
		}

		sealed class Loader : ISpriteLoader
		{
			public bool TryParseSprite(Stream s, string filename, out ISpriteFrame[] frames, out TypeDictionary metadata)
			{ frames = new ISpriteFrame[] { new Frame() }; metadata = null; return true; }
		}

		static SpriteCache Cache() => new SpriteCache(new Files(), new ISpriteLoader[] { new Loader() }, 16, 16);
		static readonly MiniYamlNode.SourceLocation Location = new MiniYamlNode.SourceLocation("stage1-test.yaml", 7);

		[Test]
		public void SpriteReservationsResolveOnceAndAllowLaterBatches()
		{
			using var cache = Cache();
			var first = cache.ReserveSprites("unit", null, Location);
			var second = cache.ReserveSprites("unit", new[] { 0 }, Location);
			cache.LoadReservations(Empty<ModData>());
			var firstSprites = cache.ResolveSprites(first);
			var later = cache.ReserveSprites("later", null, Location);
			cache.LoadReservations(Empty<ModData>());
			Assert.That(cache.ResolveSprites(second)[0], Is.SameAs(firstSprites[0]));
			Assert.That(cache.ResolveSprites(later).Length, Is.EqualTo(1));
			Assert.Throws<InvalidOperationException>(() => cache.ResolveSprites(first));
			Assert.Throws<InvalidOperationException>(() => cache.ResolveSprites(9999));
		}

		[Test]
		public void MissingSpriteKeepsFilenameAndLocationAndConsumesReservation()
		{
			using var cache = Cache();
			var token = cache.ReserveSprites("missing", null, Location);
			cache.LoadReservations(Empty<ModData>());
			var error = Assert.Throws<FileNotFoundException>(() => cache.ResolveSprites(token));
			Assert.That(error.FileName, Is.EqualTo("missing"));
			StringAssert.Contains("stage1-test.yaml:7", error.Message);
			Assert.That(cache.MissingFiles.Single().Filename, Is.EqualTo("missing"));
			Assert.Throws<InvalidOperationException>(() => cache.ResolveSprites(token));
		}

		[Test]
		public void ResolvingSpriteBatchDoesNotAllocateDictionaryRebuilds()
		{
			using var cache = Cache();
			const int count = 4096;
			var tokens = Enumerable.Range(0, count).Select(_ => cache.ReserveSprites("unit", null, Location)).ToArray();
			cache.LoadReservations(Empty<ModData>());
			var resolved = (Dictionary<int, Sprite[]>)typeof(SpriteCache).GetField("resolvedSprites", Fields).GetValue(cache);
			var initialCapacity = resolved.EnsureCapacity(0);
			// Warm up the resolver without disturbing the measured batch.
			cache.ResolveSprites(tokens[0]);
			var start = GC.GetAllocatedBytesForCurrentThread();
			var timer = Stopwatch.StartNew();
			for (var i = 1; i < tokens.Length - 1; i++)
				cache.ResolveSprites(tokens[i]);
			var capacityBeforeLastToken = resolved.EnsureCapacity(0);
			cache.ResolveSprites(tokens[tokens.Length - 1]);
			timer.Stop();
			var bytes = GC.GetAllocatedBytesForCurrentThread() - start;
			TestContext.WriteLine($"SpriteCache resolve: {bytes} bytes/batch, {timer.Elapsed.TotalMilliseconds:F3} ms/batch, tokens={count - 1}");
			Assert.That(bytes, Is.LessThan(1024));
			Assert.That(capacityBeforeLastToken, Is.EqualTo(initialCapacity), "Pending tokens must retain their existing capacity.");
			Assert.That(resolved.Count, Is.Zero);
			Assert.That(resolved.EnsureCapacity(0), Is.LessThan(initialCapacity), "The final token releases batch capacity.");
		}
	}
}
