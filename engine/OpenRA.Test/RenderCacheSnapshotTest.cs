using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Mods.Cnc.Traits;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture, NonParallelizable]
	public sealed class RenderCacheSnapshotTest
	{
		const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		static FieldInfo Field(Type type, string name) => type.GetField(name, Members);
		static T Empty<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

		sealed class ForbiddenTexture : ITexture
		{
			public Size Size => throw new InvalidOperationException("GPU texture queried");
			public TextureScaleFilter ScaleFilter { get => throw new InvalidOperationException(); set => throw new InvalidOperationException(); }
			public void Dispose() => throw new InvalidOperationException("GPU disposed");
			public byte[] GetData() => throw new InvalidOperationException();
			public void SetData(byte[] colors, int width, int height) => throw new InvalidOperationException();
			public void SetFloatData(float[] data, int width, int height) => throw new InvalidOperationException();
			public void SetDataFromReadBuffer(Rectangle rect) => throw new InvalidOperationException();
		}

		sealed class ForbiddenFrameBuffer : IFrameBuffer
		{
			public ITexture Texture => throw new InvalidOperationException("GPU framebuffer queried");
			public void Bind() => throw new InvalidOperationException();
			public void Unbind() => throw new InvalidOperationException();
			public void EnableScissor(Rectangle rect) => throw new InvalidOperationException();
			public void DisableScissor() => throw new InvalidOperationException();
			public void Dispose() => throw new InvalidOperationException();
		}

		static Sheet Sheet(int width, int height)
		{
			var sheet = new Sheet(SheetType.BGRA, new Size(width, height));
			Field(typeof(Sheet), "texture").SetValue(sheet, new ForbiddenTexture());
			Field(typeof(Sheet), "dirty").SetValue(sheet, true);
			Field(typeof(Sheet), "data").SetValue(sheet, new byte[4]);
			return sheet;
		}

		static ITuple Snapshot(Type type, object instance)
		{
			var method = type.GetMethod("GetCacheSnapshot", Members);
			Assert.That(method, Is.Not.Null, "Missing read-only cache snapshot API.");
			Assert.That(method.ReturnType.IsValueType, Is.True, "Snapshot must hold values only.");
			Assert.That(method.ReturnType.GetFields().All(f => f.FieldType == typeof(int) || f.FieldType == typeof(long)), Is.True);
			return (ITuple)method.Invoke(instance, null);
		}

		[Test]
		public void ModelSnapshotCountsLiveCachesWithoutGpuAccessOrMutation()
		{
			var model = Empty<ModelRenderer>();
			var mapped = new Dictionary<Sheet, IFrameBuffer> { { Sheet(64, 64), new ForbiddenFrameBuffer() } };
			var unmapped = new Stack<KeyValuePair<Sheet, IFrameBuffer>>();
			unmapped.Push(new(Sheet(64, 64), new ForbiddenFrameBuffer()));
			unmapped.Push(new(Sheet(64, 64), new ForbiddenFrameBuffer()));
			var queued = new List<(Sheet Sheet, Action Func)> { (mapped.Keys.Single(), () => throw new InvalidOperationException("render invoked")) };
			Field(typeof(ModelRenderer), "mappedBuffers").SetValue(model, mapped);
			Field(typeof(ModelRenderer), "unmappedBuffers").SetValue(model, unmapped);
			Field(typeof(ModelRenderer), "doRender").SetValue(model, queued);
			Field(typeof(ModelRenderer), "sheetSize").SetValue(model, 65536);
			var snapshot = Snapshot(typeof(ModelRenderer), model);
			Assert.That(Enumerable.Range(0, snapshot.Length).Select(i => snapshot[i]), Is.EqualTo(new object[] { 1, 2, 1, 4L * 65536 * 65536 * 3 }));
			Assert.That((mapped.Count, unmapped.Count, queued.Count), Is.EqualTo((1, 2, 1)), "Snapshot leaves owned caches intact.");
			Assert.That(Field(typeof(ModelRenderer), "mappedBuffers").GetValue(model), Is.SameAs(mapped));
			Assert.That(Field(typeof(ModelRenderer), "unmappedBuffers").GetValue(model), Is.SameAs(unmapped));
			Assert.That(Field(typeof(ModelRenderer), "doRender").GetValue(model), Is.SameAs(queued));
			mapped.Clear(); unmapped.Clear(); queued.Clear();
			Assert.That(snapshot[0], Is.EqualTo(1), "Previously returned values remain independent of caches.");
			var empty = Snapshot(typeof(ModelRenderer), model);
			Assert.That(Enumerable.Range(0, empty.Length).Select(i => empty[i]), Is.EqualTo(new object[] { 0, 0, 0, 0L }));
		}

		[Test]
		public void ChromeSnapshotCountsActualSheetPixelsOnceDespiteCollectionAliasesAndHandlesNullState()
		{
			var names = new[] { "cachedSheets", "cachedSprites", "cachedPanelSprites", "cachedCollectionSheets" };
			var fields = names.Select(n => Field(typeof(ChromeProvider), n)).ToArray();
			var saved = fields.Select(f => f.GetValue(null)).ToArray();
			try
			{
				var a = Sheet(64, 32);
				var b = Sheet(16, 8);
				var sprite = new Sprite(a, new Rectangle(0, 0, 8, 8), TextureChannel.RGBA);
				fields[0].SetValue(null, new Dictionary<string, (Sheet, int)> { { "a", (a, 1) }, { "b", (b, 3) } });
				fields[1].SetValue(null, new Dictionary<string, Dictionary<string, Sprite>> { { "x", new() { { "1", sprite }, { "2", sprite } } }, { "y", new() { { "1", sprite } } } });
				fields[2].SetValue(null, new Dictionary<string, Sprite[]> { { "x", new[] { sprite, sprite } }, { "y", new[] { sprite } } });
				fields[3].SetValue(null, new Dictionary<ChromeProvider.Collection, (Sheet, int)> { { new(), (a, 1) }, { new(), (a, 1) }, { new(), (b, 3) } });
				var caches = fields.Select(f => f.GetValue(null)).ToArray();
				var snapshot = Snapshot(typeof(ChromeProvider), null);
				Assert.That(Enumerable.Range(0, snapshot.Length).Select(i => snapshot[i]), Is.EqualTo(new object[] { 2, 3, 3, 3, 4L * (64 * 32 + 16 * 8) }));
				for (var i = 0; i < fields.Length; i++) Assert.That(fields[i].GetValue(null), Is.SameAs(caches[i]));
				Assert.That(Snapshot(typeof(ChromeProvider), null)[0], Is.EqualTo(2), "Snapshot leaves cached sheets intact.");
				foreach (var field in fields) field.SetValue(null, null);
				Assert.That(snapshot[0], Is.EqualTo(2));
				var empty = Snapshot(typeof(ChromeProvider), null);
				Assert.That(Enumerable.Range(0, empty.Length).Select(i => empty[i]), Is.EqualTo(new object[] { 0, 0, 0, 0, 0L }));
			}
			finally
			{
				for (var i = 0; i < fields.Length; i++) fields[i].SetValue(null, saved[i]);
			}
		}
	}
}
