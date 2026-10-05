// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, licensed under the GNU General Public License v3.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Graphics;
using CncUtil = OpenRA.Mods.Cnc.Util;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class UnitRenderingPerformanceTest
	{
		const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		static readonly float[] ReferenceGroundNormal = { 0, 0, 1, 1 };
		static readonly float[] ReferenceZVector = { 0, 0, 1, 1 };

		static void SameBits(float[] expected, float[] actual)
		{
			Assert.That(actual.Length, Is.EqualTo(expected.Length));
			for (var i = 0; i < expected.Length; i++)
				Assert.That(BitConverter.SingleToInt32Bits(actual[i]), Is.EqualTo(BitConverter.SingleToInt32Bits(expected[i])), $"component {i}");
		}

		// Frozen pre-optimization algorithm: selectors, corner order, zero-start sum,
		// projective divisions and min/max (including NaNs) are part of the contract.
		static float[] OriginalAabb(float[] mtx, float[] bounds)
		{
			var ix = new uint[] { 0, 0, 0, 0, 3, 3, 3, 3 };
			var iy = new uint[] { 1, 1, 4, 4, 1, 1, 4, 4 };
			var iz = new uint[] { 2, 5, 2, 5, 2, 5, 2, 5 };
			var ret = new[] { float.MaxValue, float.MaxValue, float.MaxValue, float.MinValue, float.MinValue, float.MinValue };
			for (var i = 0; i < 8; i++)
			{
				var vec = new[] { bounds[ix[i]], bounds[iy[i]], bounds[iz[i]], 1 };
				var tvec = new float[4];
				for (var j = 0; j < 4; j++)
				{
					tvec[j] = 0;
					for (var k = 0; k < 4; k++)
						tvec[j] += mtx[4 * k + j] * vec[k];
				}

				ret[0] = Math.Min(ret[0], tvec[0] / tvec[3]);
				ret[1] = Math.Min(ret[1], tvec[1] / tvec[3]);
				ret[2] = Math.Min(ret[2], tvec[2] / tvec[3]);
				ret[3] = Math.Max(ret[3], tvec[0] / tvec[3]);
				ret[4] = Math.Max(ret[4], tvec[1] / tvec[3]);
				ret[5] = Math.Max(ret[5], tvec[2] / tvec[3]);
			}

			return ret;
		}

		[Test]
		public void AabbPreservesOriginalBitsForAffineProjectiveAndNonFiniteInputs()
		{
			var projective = new[] { 1.2f, -0.7f, 0.1f, 0.25f, 0.05f, 2.1f, -0.4f, -0.125f, -0.3f, 0.2f, 0.9f, 0.5f, 10.5f, -8f, 3.3f, 0.75f };
			var zeroW = CncUtil.IdentityMatrix();
			zeroW[15] = 0;
			var nonFinite = CncUtil.IdentityMatrix();
			nonFinite[0] = float.PositiveInfinity;
			nonFinite[6] = BitConverter.Int32BitsToSingle(unchecked((int)0xffc12345));
			var matrices = new[] { CncUtil.IdentityMatrix(), CncUtil.TranslationMatrix(3, -7, 12),
				CncUtil.MakeFloatMatrix(new WRot(new WAngle(117), new WAngle(289), new WAngle(619)).AsMatrix()),
				CncUtil.ScaleMatrix(-2, 0, 1.25f), projective, zeroW, nonFinite };
			var boxes = new[] { new[] { -3.2f, -7.1f, -11f, 4.9f, 13.7f, 2f }, new float[6],
				new[] { -0f, -0f, -0f, 0f, 0f, 0f }, new[] { float.NegativeInfinity, -1f, float.NaN, float.PositiveInfinity, 2f, 3f } };
			foreach (var matrix in matrices)
				foreach (var box in boxes)
					SameBits(OriginalAabb(matrix, box), CncUtil.MatrixAABBMultiply(matrix, box));
		}

		[Test]
		public void AabbReturnsIndependentResultsAndLeavesInputsUntouched()
		{
			var matrix = CncUtil.TranslationMatrix(3, -7, 12);
			var bounds = new[] { -2f, -3f, -4f, 5f, 6f, 7f };
			var beforeMatrix = (float[])matrix.Clone();
			var beforeBounds = (float[])bounds.Clone();
			var first = CncUtil.MatrixAABBMultiply(matrix, bounds);
			var saved = (float[])first.Clone();
			var second = CncUtil.MatrixAABBMultiply(CncUtil.ScaleMatrix(2, 3, 4), bounds);
			second[0] = 12345;
			Assert.That(first, Is.Not.SameAs(second));
			SameBits(saved, first);
			SameBits(beforeMatrix, matrix);
			SameBits(beforeBounds, bounds);
		}

		[Test]
		public void AabbAllocatesOnlyTheOwnedSixFloatResultAfterWarmup()
		{
			var matrix = CncUtil.IdentityMatrix();
			var bounds = new[] { -2f, -3f, -4f, 5f, 6f, 7f };
			for (var i = 0; i < 1000; i++)
				CncUtil.MatrixAABBMultiply(matrix, bounds);
			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 10000; i++)
				CncUtil.MatrixAABBMultiply(matrix, bounds);
			var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
			TestContext.WriteLine($"AABB warmed bytes/call: {bytes / 10000.0}");
			Assert.That(bytes, Is.LessThanOrEqualTo(640000));
		}

		sealed class Cache
		{
			public readonly object Instance;
			public readonly Func<WRot, float, WRot, WRot, object> Get;
			public readonly Action Clear;
			public Cache()
			{
				var type = typeof(CncUtil).Assembly.GetType("OpenRA.Mods.Cnc.Graphics.FixedModelTransformCache");
				Assert.That(type, Is.Not.Null, "Production fixed-transform cache is missing.");
				Instance = Activator.CreateInstance(type, true);
				Get = (Func<WRot, float, WRot, WRot, object>)type.GetMethod("Get", Members).CreateDelegate(typeof(Func<WRot, float, WRot, WRot, object>), Instance);
				Clear = (Action)type.GetMethod("Clear", Members).CreateDelegate(typeof(Action), Instance);
			}

			public float[] Array(object entry, string field) => (float[])entry.GetType().GetField(field, Members).GetValue(entry);
		}

		static (float[] ScaleTransform, float[] ShadowTransform, float[] InvShadowTransform,
			float[] CameraTransform, float[] InvCameraTransform, float[] ShadowScreenTransform,
			float[] ShadowGroundNormal, float ShadowDirection) OriginalTransforms(WRot camera, float scale, WRot groundOrientation, WRot lightSource)
		{
			var scaleTransform = CncUtil.ScaleMatrix(scale, scale, scale);
			var lightYaw = CncUtil.MakeFloatMatrix(new WRot(WAngle.Zero, WAngle.Zero, -lightSource.Yaw).AsMatrix());
			var lightPitch = CncUtil.MakeFloatMatrix(new WRot(WAngle.Zero, -lightSource.Pitch, WAngle.Zero).AsMatrix());
			var ground = CncUtil.MakeFloatMatrix(groundOrientation.AsMatrix());
			var shadowTransform = CncUtil.MatrixMultiply(CncUtil.MatrixMultiply(lightPitch, lightYaw), CncUtil.MatrixInverse(ground));
			var groundNormal = CncUtil.MatrixVectorMultiply(ground, ReferenceGroundNormal);
			var invShadowTransform = CncUtil.MatrixInverse(shadowTransform);
			var cameraTransform = CncUtil.MakeFloatMatrix(camera.AsMatrix());
			var invCameraTransform = CncUtil.MatrixInverse(cameraTransform);
			var shadowScreenTransform = CncUtil.MatrixMultiply(cameraTransform, invShadowTransform);
			var shadowGroundNormal = CncUtil.MatrixVectorMultiply(shadowTransform, groundNormal);
			var screenLightVector = CncUtil.MatrixVectorMultiply(invShadowTransform, ReferenceZVector);
			screenLightVector = CncUtil.MatrixVectorMultiply(cameraTransform, screenLightVector);
			return (scaleTransform, shadowTransform, invShadowTransform, cameraTransform, invCameraTransform,
				shadowScreenTransform, shadowGroundNormal, -screenLightVector[2] / screenLightVector[1]);
		}

		[Test]
		public void FixedTransformsPreserveEveryOriginalMatrixAndShadowDirectionBit()
		{
			var cache = new Cache();
			foreach (var scale in new[] { 1f, 1.23f, -0f, float.NaN })
			{
				var camera = new WRot(new WAngle(73), new WAngle(221), new WAngle(49));
				var ground = new WRot(new WAngle(17), new WAngle(33), new WAngle(79));
				var light = new WRot(new WAngle(81), new WAngle(127), new WAngle(313));
				var entry = cache.Get(camera, scale, ground, light);
				var old = OriginalTransforms(camera, scale, ground, light);
				var expectedArrays = new Dictionary<string, float[]> { { "ScaleTransform", old.ScaleTransform }, { "ShadowTransform", old.ShadowTransform },
					{ "InvShadowTransform", old.InvShadowTransform }, { "CameraTransform", old.CameraTransform }, { "InvCameraTransform", old.InvCameraTransform },
					{ "ShadowScreenTransform", old.ShadowScreenTransform }, { "ShadowGroundNormal", old.ShadowGroundNormal }, { "ShadowDirection", new[] { old.ShadowDirection } } };
				foreach (var expected in expectedArrays)
				{
					var actual = expected.Key == "ShadowDirection" ? new[] { (float)entry.GetType().GetField(expected.Key, Members).GetValue(entry) } : cache.Array(entry, expected.Key);
					SameBits(expected.Value, actual);
				}
			}
		}

		[Test]
		public void FixedTransformHitsReuseEntriesButMissesNeverOverwriteDeferredArrays()
		{
			var cache = new Cache();
			var a = cache.Get(WRot.None, 1, WRot.None, WRot.None);
			var saved = cache.Array(a, "ScaleTransform").ToArray();
			var b = cache.Get(WRot.None, 2, WRot.None, WRot.None);
			Assert.That(cache.Get(WRot.None, 1, WRot.None, WRot.None), Is.SameAs(a));
			Assert.That(cache.Array(a, "ScaleTransform"), Is.Not.SameAs(cache.Array(b, "ScaleTransform")));
			for (var i = 3; i < 25; i++)
				cache.Get(WRot.None, i, WRot.None, WRot.None);
			SameBits(saved, cache.Array(a, "ScaleTransform"));
			var entries = (Array)cache.Instance.GetType().GetField("entries", Members).GetValue(cache.Instance);
			Assert.That(entries.Length, Is.EqualTo(8));
			Assert.That(entries.Cast<object>().Count(e => e != null), Is.EqualTo(8));
			cache.Clear();
			Assert.That(entries.Cast<object>().All(e => e == null), Is.True);
		}

		[Test]
		public void FixedTransformKeysUseMatrixOrientationScaleBitsAndOnlyUsedLightAngles()
		{
			var cache = new Cache();
			var a = cache.Get(WRot.None, 0f, WRot.None, WRot.None);
			Assert.That(cache.Get(WRot.None, -0f, WRot.None, WRot.None), Is.Not.SameAs(a));
			var nan1 = BitConverter.Int32BitsToSingle(unchecked((int)0x7fc12345));
			var nan2 = BitConverter.Int32BitsToSingle(unchecked((int)0x7fc54321));
			var nanEntry = cache.Get(WRot.None, nan1, WRot.None, WRot.None);
			Assert.That(cache.Get(WRot.None, nan1, WRot.None, WRot.None), Is.SameAs(nanEntry));
			Assert.That(cache.Get(WRot.None, nan2, WRot.None, WRot.None), Is.Not.SameAs(nanEntry));
			Assert.That(cache.Get(WRot.None, 0f, WRot.None, new WRot(new WAngle(93), WAngle.Zero, WAngle.Zero)), Is.SameAs(a));
			Assert.That(cache.Get(WRot.None, 0f, WRot.None, WRot.FromYaw(new WAngle(13))), Is.Not.SameAs(a));
			Assert.That(cache.Get(WRot.None, 0f, WRot.None, new WRot(WAngle.Zero, new WAngle(13), WAngle.Zero)), Is.Not.SameAs(a));
			var quaternion = new WRot(new WVec(724, 724, 0), new WAngle(137));
			var euler = new WRot(quaternion.Roll, quaternion.Pitch, quaternion.Yaw);
			Assert.That(quaternion, Is.EqualTo(euler), "WRot equality compares Euler fields.");
			Assert.That(quaternion.AsMatrix(), Is.Not.EqualTo(euler.AsMatrix()), "Fixture must expose quaternion rounding.");
			Assert.That(cache.Get(quaternion, 1, WRot.None, WRot.None), Is.Not.SameAs(cache.Get(euler, 1, WRot.None, WRot.None)));
			Assert.That(cache.Get(WRot.None, 1, quaternion, WRot.None), Is.Not.SameAs(cache.Get(WRot.None, 1, euler, WRot.None)));
		}

		[Test]
		public void FixedTransformWarmHitsAllocateNothing()
		{
			var cache = new Cache();
			for (var i = 0; i < 1000; i++)
				cache.Get(WRot.None, 1, WRot.None, WRot.None);
			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 10000; i++)
				cache.Get(WRot.None, 1, WRot.None, WRot.None);
			var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
			TestContext.WriteLine($"Fixed transform warm hit bytes/call: {bytes / 10000.0}");
			Assert.That(bytes, Is.EqualTo(0));
			for (var i = 0; i < 1000; i++)
				OriginalTransforms(WRot.None, 1, WRot.None, WRot.None);
			before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 10000; i++)
				OriginalTransforms(WRot.None, 1, WRot.None, WRot.None);
			var original = GC.GetAllocatedBytesForCurrentThread() - before;
			TestContext.WriteLine($"Original fixed-transform formulas bytes/call: {original / 10000.0}");
			Assert.That(original, Is.GreaterThan(bytes));
		}

		sealed class Renderable : IRenderable
		{
			public readonly string Name;
			public WPos Pos { get; }
			public int ZOffset { get; }
			public bool IsDecoration => false;
			public Renderable(string name, int key) { Name = name; Pos = WPos.Zero; ZOffset = key; }
			public IRenderable WithZOffset(int offset) => throw new NotSupportedException();
			public IRenderable OffsetBy(in WVec offset) => throw new NotSupportedException();
			public IRenderable AsDecoration() => throw new NotSupportedException();
			public IFinalizedRenderable PrepareRender(WorldRenderer wr) => throw new NotSupportedException();
		}

		sealed class Sorter
		{
			public readonly object Instance;
			public readonly Action<IReadOnlyList<IRenderable>, Func<IRenderable, int>> Sort;
			public readonly Func<int, IRenderable> At;
			public readonly Func<int> Count;
			public readonly Action Clear;
			public Sorter()
			{
				var generic = typeof(WorldRenderer).Assembly.GetType("OpenRA.Graphics.StableSortBuffer`1");
				Assert.That(generic, Is.Not.Null, "Production reusable stable-sort buffer is missing.");
				var type = generic.MakeGenericType(typeof(IRenderable));
				Instance = Activator.CreateInstance(type, true);
				Sort = (Action<IReadOnlyList<IRenderable>, Func<IRenderable, int>>)type.GetMethod("Sort", Members).CreateDelegate(typeof(Action<IReadOnlyList<IRenderable>, Func<IRenderable, int>>), Instance);
				At = (Func<int, IRenderable>)type.GetProperty("Item", Members).GetMethod.CreateDelegate(typeof(Func<int, IRenderable>), Instance);
				Count = (Func<int>)type.GetProperty("Count", Members).GetMethod.CreateDelegate(typeof(Func<int>), Instance);
				Clear = (Action)type.GetMethod("Clear", Members).CreateDelegate(typeof(Action), Instance);
			}
		}

		static IRenderable[] SortInput() => new IRenderable[] { new Renderable("transparent first", 7), new Renderable("fog", 7),
			new Renderable("minimum", int.MinValue), new Renderable("nonspatial", 7), new Renderable("maximum", int.MaxValue),
			new Renderable("negative", -2), new Renderable("transparent last", 7) };

		[Test]
		public void StableSortPreservesLinqOrderIncludingTiesAndExtremeKeys()
		{
			var input = SortInput();
			var snapshot = input.ToArray();
			var expected = snapshot.OrderBy(WorldRenderer.RenderableZPositionComparisonKey).ToArray();
			var sorter = new Sorter();
			sorter.Sort(input, WorldRenderer.RenderableZPositionComparisonKey);
			Assert.That(Enumerable.Range(0, sorter.Count()).Select(sorter.At), Is.EqualTo(expected));
			Assert.That(input, Is.EqualTo(snapshot));
			sorter.Clear();
			sorter.Sort(Array.Empty<IRenderable>(), WorldRenderer.RenderableZPositionComparisonKey);
			Assert.That(sorter.Count(), Is.Zero);
		}

		[Test]
		public void StableSortEvaluatesEveryKeyOnceInSourceOrderBeforeConsumption()
		{
			var input = SortInput();
			var snapshot = input.ToArray();
			var visited = new List<IRenderable>();
			var sorter = new Sorter();
			sorter.Sort(input, r => { visited.Add(r); return WorldRenderer.RenderableZPositionComparisonKey(r); });
			Assert.That(visited, Is.EqualTo(snapshot));
			for (var i = 0; i < sorter.Count(); i++)
				sorter.At(i);
			Assert.That(visited, Is.EqualTo(snapshot));
			sorter.Clear();
			var items = (IRenderable[])sorter.Instance.GetType().GetField("items", Members).GetValue(sorter.Instance);
			Assert.That(items.Length, Is.GreaterThanOrEqualTo(input.Length), "Clear retains capacity.");
			Assert.That(items.All(item => item == null), Is.True, "Clear releases every retained source reference.");
			Assert.That(sorter.Count(), Is.Zero);
		}

		[Test]
		public void StableSortWarmBufferAllocatesLessThanOriginalOrderBy()
		{
			var input = Enumerable.Range(0, 256).Select(i => (IRenderable)new Renderable(i.ToString(), i % 17)).ToArray();
			var sorter = new Sorter();
			var key = WorldRenderer.RenderableZPositionComparisonKey;
			for (var i = 0; i < 100; i++) { sorter.Sort(input, key); sorter.Clear(); }
			var timer = Stopwatch.StartNew();
			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 1000; i++)
			{
				sorter.Sort(input, key);
				for (var j = 0; j < sorter.Count(); j++)
					GC.KeepAlive(sorter.At(j));
				sorter.Clear();
			}
			var buffered = GC.GetAllocatedBytesForCurrentThread() - before;
			var elapsed = timer.Elapsed.TotalMilliseconds;
			timer.Restart();
			before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 1000; i++)
				foreach (var item in input.OrderBy(key))
					GC.KeepAlive(item);
			var original = GC.GetAllocatedBytesForCurrentThread() - before;
			TestContext.WriteLine($"Stable sort bytes/call: buffered={buffered / 1000.0}, original={original / 1000.0}; ms: buffered={elapsed}, original={timer.Elapsed.TotalMilliseconds}");
			Assert.That(buffered, Is.LessThan(original / 20));
		}

		[Test]
		public void StableSortSnapshotsAllSourceItemsBeforeKeyCallbacksCanMutateSource()
		{
			var originalSource = SortInput().ToList();
			var expected = originalSource.OrderBy(r =>
			{
				originalSource.Clear();
				return WorldRenderer.RenderableZPositionComparisonKey(r);
			}).ToArray();
			var source = SortInput().ToList();
			var snapshot = source.ToArray();
			var sorter = new Sorter();
			var visited = new List<IRenderable>();
			sorter.Sort(source, r =>
			{
				visited.Add(r);
				source.Clear();
				return WorldRenderer.RenderableZPositionComparisonKey(r);
			});
			Assert.That(visited, Is.EqualTo(snapshot));
			Assert.That(Enumerable.Range(0, sorter.Count()).Select(i => ((Renderable)sorter.At(i)).Name),
				Is.EqualTo(expected.Cast<Renderable>().Select(r => r.Name)));
			sorter.Clear();
		}

		[Test]
		public void StableSortRepresentativeDistributionsMatchOriginalWithInformationalTiming()
		{
			foreach (var size in new[] { 100, 300, 600, 1000 })
				foreach (var distribution in new[] { "equal", "mixed", "nearly sorted" })
				{
					var random = new Random(123);
					var input = Enumerable.Range(0, size).Select(i => (IRenderable)new Renderable(i.ToString(),
						distribution == "equal" ? 7 : distribution == "mixed" ? random.Next(-size, size) : i % 31 == 0 ? i + 7 : i)).ToArray();
					var snapshot = input.ToArray();
					var expected = snapshot.OrderBy(WorldRenderer.RenderableZPositionComparisonKey).ToArray();
					var sorter = new Sorter();
					var key = WorldRenderer.RenderableZPositionComparisonKey;
					for (var warm = 0; warm < 100; warm++)
					{
						sorter.Sort(input, key);
						sorter.Clear();
						foreach (var item in input.OrderBy(key))
							GC.KeepAlive(item);
					}

					for (var round = 0; round < 5; round++)
					{
						var timer = Stopwatch.StartNew();
						var before = GC.GetAllocatedBytesForCurrentThread();
						for (var i = 0; i < 100; i++)
						{
							sorter.Sort(input, key);
							for (var j = 0; j < sorter.Count(); j++)
								GC.KeepAlive(sorter.At(j));
							sorter.Clear();
						}

						var bufferedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
						var bufferedMs = timer.Elapsed.TotalMilliseconds;
						timer.Restart();
						before = GC.GetAllocatedBytesForCurrentThread();
						for (var i = 0; i < 100; i++)
							foreach (var item in input.OrderBy(key))
								GC.KeepAlive(item);
						var originalBytes = GC.GetAllocatedBytesForCurrentThread() - before;
						var originalMs = timer.Elapsed.TotalMilliseconds;
						TestContext.WriteLine($"sort size={size} distribution={distribution} round={round}: bytes/call={bufferedBytes / 100.0}/{originalBytes / 100.0}, ms={bufferedMs}/{originalMs}");
						Assert.That(bufferedBytes, Is.Zero);
					}

					sorter.Sort(input, key);
					Assert.That(Enumerable.Range(0, sorter.Count()).Select(sorter.At), Is.EqualTo(expected));
					Assert.That(input, Is.EqualTo(snapshot));
					sorter.Clear();
				}
		}

	}
}
