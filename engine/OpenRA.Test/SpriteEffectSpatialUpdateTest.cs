using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Effects;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Effects;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class SpriteEffectSpatialUpdateTest
	{
		const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
		static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Members).SetValue(instance, value);
		static T Get<T>(object instance, string name) => (T)instance.GetType().GetField(name, Members).GetValue(instance);
		static Sprite Image(int width = 16, int height = 16, float scale = 1f, float zRamp = 0f) =>
			new(new Sheet(SheetType.BGRA, new Size(64, 64)), new Rectangle(0, 0, width, height), zRamp, float3.Zero, TextureChannel.RGBA, scale: scale);

		sealed class Sequence : ISpriteSequence
		{
			public readonly List<int> RequestedFrames = new();
			public Sprite Current = Image();
			public string Name => "idle";
			public int Length { get; set; } = 100;
			public int Facings => 1;
			public int Tick => 40;
			public int ZOffset => 0;
			public int ShadowZOffset => 0;
			public Rectangle Bounds => new(0, 0, 16, 16);
			public bool IgnoreWorldTint => true;
			public float Scale => 1;
			public void ResolveSprites(SpriteCache cache) => throw new NotSupportedException();
			public Sprite GetSprite(int frame) { RequestedFrames.Add(frame); return Current; }
			public Sprite GetSprite(int frame, WAngle facing) => GetSprite(frame);
			public (Sprite Sprite, WAngle Rotation) GetSpriteWithRotation(int frame, WAngle facing) => (GetSprite(frame), WAngle.Zero);
			public Sprite GetShadow(int frame, WAngle facing) => null;
			public float GetAlpha(int frame) => 1;
		}

		sealed class Fixture
		{
			public readonly World World = Empty<World>();
			public readonly Sequence Sequence = new();
			public readonly SpatiallyPartitioned<IEffect> Index = new(4096, 4096, 128);
			public WPos Position = new(1024, 2048, 0);
			public int PositionCalls, FacingCalls, Adds, Removes;
			public Fixture()
			{
				var sequences = Empty<SequenceSet>();
				Set(sequences, "images", new Dictionary<string, IReadOnlyDictionary<string, ISpriteSequence>> { { "image", new Dictionary<string, ISpriteSequence> { { "idle", Sequence } } } });
				var map = Empty<Map>();
				Set(map, "<Sequences>k__BackingField", sequences);
				Set(map, "Grid", Empty<MapGrid>());
				Set(World, "Map", map);
				var renderer = Empty<WorldRenderer>();
				Set(renderer, "TileSize", new Size(64, 32));
				Set(renderer, "TileScale", 1024);
				var screenMap = Empty<ScreenMap>();
				Set(screenMap, "worldRenderer", renderer);
				Set(screenMap, "partitionedRenderableEffects", Index);
				Set(World, "ScreenMap", screenMap);
				Set(World, "effects", new List<IEffect>());
				Set(World, "frameEndActions", new Queue<Action<World>>());
				Set(Index, "addItem", (Action<Dictionary<IEffect, Rectangle>, IEffect, Rectangle>)((bin, effect, bounds) => { Adds++; bin.Add(effect, bounds); }));
				Set(Index, "removeItem", (Action<Dictionary<IEffect, Rectangle>, IEffect, Rectangle>)((bin, effect, bounds) => { Removes++; bin.Remove(effect); }));
			}

			public SpriteEffect Effect(int delay = 0) => new(() => { PositionCalls++; return Position; },
				() => { FacingCalls++; return WAngle.Zero; }, World, "image", "idle", "palette", delay: delay);
			public Queue<Action<World>> FrameEnd => Get<Queue<Action<World>>>(World, "frameEndActions");
		}

		[Test]
		public void StationaryEffectTicksAnimationAndCallbacksButSkipsUnchangedBounds()
		{
			var f = new Fixture();
			var effect = f.Effect();
			Assert.That(f.PositionCalls, Is.EqualTo(1));
			effect.Tick(f.World);
			Assert.That((f.Adds, f.Removes, f.PositionCalls, f.FacingCalls), Is.EqualTo((1, 0, 1, 1)));
			f.Sequence.Current = Image(); // New image identity with the same size must not churn bins.
			effect.Tick(f.World);
			effect.Tick(f.World);
			Assert.That(f.Sequence.RequestedFrames, Is.EqualTo(new[] { 0, 1, 2 }));
			Assert.That((f.Adds, f.Removes, f.PositionCalls, f.FacingCalls), Is.EqualTo((1, 0, 3, 3)));
			Assert.That(f.Index.Contains(effect), Is.True);
		}

		[Test]
		public void ExactWorldPositionAndFullFloatSpriteSizeChangesUpdateEvenIfPixelBoundsMatch()
		{
			var f = new Fixture();
			var effect = f.Effect();
			effect.Tick(f.World);
			f.Position += new WVec(1, 0, 0); // Below projected pixel rounding threshold.
			effect.Tick(f.World);
			f.Sequence.Current = Image(scale: 1.01f); // Below integer sprite-width threshold.
			effect.Tick(f.World);
			f.Sequence.Current = Image(scale: 1.01f, zRamp: .25f); // Z changes independently of screen width/height.
			effect.Tick(f.World);
			Assert.That((f.Adds, f.Removes), Is.EqualTo((4, 3)));
			effect.Tick(f.World);
			Assert.That((f.Adds, f.Removes), Is.EqualTo((4, 3)));
		}

		[Test]
		public void ZeroSizeRemovesEntryAndNonzeroSizeAddsItAgain()
		{
			var f = new Fixture();
			var effect = f.Effect();
			effect.Tick(f.World);
			f.Sequence.Current = Image(0, 0);
			effect.Tick(f.World);
			Assert.That(f.Index.Contains(effect), Is.False);
			Assert.That((f.Adds, f.Removes), Is.EqualTo((1, 1)));
			effect.Tick(f.World);
			f.Sequence.Current = Image();
			effect.Tick(f.World);
			Assert.That(f.Index.Contains(effect), Is.True);
			Assert.That((f.Adds, f.Removes), Is.EqualTo((2, 1)));
		}

		[Test]
		public void DelayFirstPositionAndCompletionFrameEndRemovalRemainUnchanged()
		{
			var f = new Fixture();
			f.Sequence.Length = 2;
			var effect = f.Effect(delay: 2);
			f.World.Add(effect);
			f.Position += new WVec(1024, 0, 0);
			effect.Tick(f.World); effect.Tick(f.World);
			Assert.That((f.Adds, f.PositionCalls, f.FacingCalls), Is.EqualTo((0, 1, 0)));
			effect.Tick(f.World);
			Assert.That(f.Index.At(new int2(64, 64)), Does.Contain(effect), "First Add keeps the constructor position.");
			effect.Tick(f.World); effect.Tick(f.World);
			Assert.That(f.Sequence.RequestedFrames, Is.EqualTo(new[] { 0, 1, 1 }));
			Assert.That(f.FrameEnd.Count, Is.EqualTo(1));
			Assert.That(f.World.Effects, Does.Contain(effect));
			f.FrameEnd.Dequeue()(f.World);
			Assert.That(f.World.Effects, Is.Empty);
			Assert.That(f.Index.Contains(effect), Is.False);
			effect.Tick(f.World);
			Assert.That(f.FrameEnd, Is.Empty, "Animation completion enqueues removal only once.");
		}

		[Test]
		public void MixedStationaryMovingEffectsPreserveLegacyOrderedViewportQueries()
		{
			var f = new Fixture();
			var legacy = new Fixture();
			var effects = Enumerable.Range(0, 8).Select(_ => f.Effect()).ToArray();
			foreach (var effect in effects) { effect.Tick(f.World); legacy.World.ScreenMap.Add(effect, legacy.Position, legacy.Sequence.Current); }
			for (var tick = 0; tick < 20; tick++)
			{
				for (var i = 0; i < effects.Length; i++)
				{
					f.Position = new WPos(1024 + (i % 2 == 0 ? 0 : tick * 256), 2048, 0);
					effects[i].Tick(f.World);
					legacy.World.ScreenMap.Update(effects[i], f.Position, f.Sequence.Current);
				}

				foreach (var box in new[] { (new int2(0, 0), new int2(1024, 128)), (new int2(48, 48), new int2(256, 80)), (new int2(128, 0), new int2(512, 128)) })
					Assert.That(f.World.ScreenMap.RenderableEffectsInBox(box.Item1, box.Item2).ToArray(),
						Is.EqualTo(legacy.World.ScreenMap.RenderableEffectsInBox(box.Item1, box.Item2).ToArray()), $"ordered equal-Z source at tick {tick}");
			}
		}
	}
}
