using System;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;
using OpenRA.Support;

namespace OpenRA.Test
{
	[TestFixture]
	public class IosResourceLifetimeTest
	{
		sealed class CountingTexture : ITexture
		{
			public int Disposals;
			public Size Size => new(16, 16);
			public TextureScaleFilter ScaleFilter { get; set; }
			public void Dispose() => Disposals++;
			public byte[] GetData() => new byte[16 * 16 * 4];
			public void SetData(byte[] colors, int width, int height) { }
			public void SetFloatData(float[] data, int width, int height) { }
			public void SetDataFromReadBuffer(Rectangle rect) { }
		}

		static FieldInfo VideoField(string name) => typeof(MenuVideoBackgroundWidget)
			.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

		[Test]
		public void VisibilityPredicateChangeReleasesVideoEvenWhenDrawIsSkipped()
		{
			var video = new MenuVideoBackgroundWidget();
			var texture = new CountingTexture();
			VideoField("sheet").SetValue(video, new Sheet(SheetType.BGRA, texture));
			VideoField("upload").SetValue(video, new byte[1024]);
			VideoField("displayedPixels").SetValue(video, new byte[1024]);
			video.IsVisible = () => false;
			video.DrawOuter();
			Assert.That(texture.Disposals, Is.EqualTo(1));
			Assert.That(VideoField("upload").GetValue(video), Is.Null);
			Assert.That(VideoField("displayedPixels").GetValue(video), Is.Null);
		}

		[Test]
		public void HiddenHomeReleasesVideoBuffersAndCanInitializeAgain()
		{
			var video = new MenuVideoBackgroundWidget();
			var texture = new CountingTexture();
			VideoField("sheet").SetValue(video, new Sheet(SheetType.BGRA, texture));
			VideoField("upload").SetValue(video, new byte[1024]);
			VideoField("displayedPixels").SetValue(video, new byte[1024]);
			VideoField("attempted").SetValue(video, true);

			video.Hidden();

			Assert.That(texture.Disposals, Is.EqualTo(1));
			Assert.That(VideoField("sheet").GetValue(video), Is.Null);
			Assert.That(VideoField("upload").GetValue(video), Is.Null);
			Assert.That(VideoField("displayedPixels").GetValue(video), Is.Null);
			Assert.That(VideoField("attempted").GetValue(video), Is.False);
			video.Hidden();
			video.Removed();
			Assert.That(texture.Disposals, Is.EqualTo(1));
		}

		[Test]
		public void RemovedHomeDoesNotRetainPixelsOrDisposeTextureTwice()
		{
			var video = new MenuVideoBackgroundWidget();
			var texture = new CountingTexture();
			VideoField("sheet").SetValue(video, new Sheet(SheetType.BGRA, texture));
			VideoField("upload").SetValue(video, new byte[1024]);
			VideoField("displayedPixels").SetValue(video, new byte[1024]);
			video.Removed();
			video.Removed();
			Assert.That(texture.Disposals, Is.EqualTo(1));
			Assert.That(VideoField("upload").GetValue(video), Is.Null);
			Assert.That(VideoField("displayedPixels").GetValue(video), Is.Null);
		}

		static byte[] PlaybackData(PcmPlayback playback) => (byte[])typeof(PcmPlayback)
			.GetField("Data", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback);

		[TestCase(false, 0)]
		[TestCase(true, 0)]
		[TestCase(true, 1)]
		[TestCase(true, 3)]
		public void EmptyPcmCompletesWithoutThrowingOrRemainingActive(bool loop, int bytes)
		{
			var mixer = new PcmAudioMixer(48000, 2);
			var sound = mixer.Play(new byte[bytes], 2, 16, 48000, loop, 1f);
			var output = new float[8];
			Assert.DoesNotThrow(() => mixer.Mix(output));
			Assert.That(output, Is.All.Zero);
			Assert.That(sound.Complete, Is.True);
			Assert.That(mixer.ActivePlaybackCount, Is.Zero);
		}

		[TestCase("stop")]
		[TestCase("stop-all")]
		[TestCase("finish")]
		[TestCase("handle-stop")]
		public void FinishedPcmHandleReleasesItsLargeBuffer(string completion)
		{
			var mixer = new PcmAudioMixer(48000, 2);
			var sound = mixer.Play(new byte[1024 * 1024], 2, 16, 48000, false, 1f);
			switch (completion)
			{
				case "stop": mixer.Stop(sound); break;
				case "stop-all": mixer.StopAll(); break;
				case "finish": mixer.Mix(new float[1024 * 1024]); break;
				default: sound.Stop(); mixer.Mix(new float[2]); break;
			}

			Assert.That(sound.Complete, Is.True);
			Assert.That(mixer.ActivePlaybackCount, Is.Zero);
			Assert.That(PlaybackData(sound).Length, Is.Zero, "Callers may keep finished ISound handles alive.");
		}

		[Test]
		public void ReleasingOneHandlePreservesSharedSoundDataAndActivePlayback()
		{
			var mixer = new PcmAudioMixer(48000, 2);
			var data = new byte[] { 0xff, 0x7f };
			var first = mixer.Play(data, 1, 16, 48000, true, 1f);
			var second = mixer.Play(data, 1, 16, 48000, true, 1f);
			mixer.Stop(first);
			var output = new float[8];
			mixer.Mix(output);
			Assert.That(output[0], Is.EqualTo(1f).Within(.001));
			Assert.That(second.Complete, Is.False);
			Assert.That(PlaybackData(second), Is.SameAs(data));
			Assert.That(mixer.ActivePlaybackCount, Is.EqualTo(1));
		}
	}
}
