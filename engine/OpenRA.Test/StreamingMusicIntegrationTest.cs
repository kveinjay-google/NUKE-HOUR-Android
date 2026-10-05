using System;
using System.IO;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using OpenRA.FileSystem;
using OpenRA.GameRules;
using OpenRA.Mods.Common.AudioLoaders;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	[NonParallelizable]
	public class StreamingMusicIntegrationTest
	{
		sealed class Files : IReadOnlyFileSystem
		{
			readonly byte[] wav;
			public bool Closed;
			public Files(byte[] wav) { this.wav = wav; }
			public Stream Open(string filename)
			{
				if (Closed) throw new ObjectDisposedException(nameof(Files));
				return new MemoryStream(wav, false);
			}
			public bool TryOpen(string filename, out Stream stream) { stream = Open(filename); return true; }
			public bool Exists(string filename) => true;
			public bool IsExternalFile(string filename) => false;
			public bool TryGetPackageContaining(string path, out IReadOnlyPackage package, out string filename)
			{ package = null; filename = null; return false; }
		}

		sealed class PlatformStub : IPlatform
		{
			readonly ISoundEngine engine;
			public PlatformStub(ISoundEngine engine) { this.engine = engine; }
			public ISoundEngine CreateSound(string device) => engine;
			public IPlatformWindow CreateWindow(Size size, WindowMode mode, float scale, int vertex, int index, int display, GLProfile profile) => throw new NotSupportedException();
			public IHapticEngine CreateHaptics() => throw new NotSupportedException();
			public IFont CreateFont(byte[] data) => throw new NotSupportedException();
		}

		class LegacyEngine : ISoundEngine
		{
			public int LegacyCalls;
			public byte[] LegacyPcm;
			public bool Dummy => false;
			public float Volume { get; set; }
			public SoundDevice[] AvailableDevices() => Array.Empty<SoundDevice>();
			public ISoundSource AddSoundSourceFromMemory(byte[] data, int channels, int bits, int rate) => null;
			public ISound Play2D(ISoundSource source, bool loop, bool relative, WPos pos, float volume, bool attenuate) => null;
			public ISound Play2DStream(Stream stream, int channels, int bits, int rate, bool loop, bool relative, WPos pos, float volume)
			{
				LegacyCalls++;
				LegacyPcm = stream.ReadAllBytes();
				return new Handle();
			}
			public void PauseSound(ISound sound, bool paused) { }
			public void StopSound(ISound sound) { }
			public void SetAllSoundsPaused(bool paused) { }
			public void StopAllSounds() { }
			public void SetListenerPosition(WPos position) { }
			public void SetSoundVolume(float volume, ISound music, ISound video) { }
			public void SetSoundLooping(bool looping, ISound sound) { }
			public void SetSoundPosition(ISound sound, WPos position) { }
			public void Dispose() { }
		}

		sealed class StreamingEngine : LegacyEngine, IStreamingSoundEngine
		{
			public Func<Stream> Open;
			public ISound Play2DStream(Func<Stream> open, int channels, int bits, int rate, bool loop, bool relative, WPos pos, float volume)
			{ Open = open; return new Handle(); }
		}

		sealed class Handle : ISound
		{
			public float Volume { get; set; }
			public float SeekPosition => 0;
			public bool Complete => false;
			public void SetPosition(WPos position) { }
		}

		static byte[] Wav(byte[] pcm, bool adpcm = false)
		{
			using var output = new MemoryStream();
			using var writer = new BinaryWriter(output);
			writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + pcm.Length + (adpcm ? 12 : 0));
			writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
			writer.Write((short)(adpcm ? 17 : 1)); writer.Write((short)1); writer.Write(48000); writer.Write(96000);
			writer.Write((short)(adpcm ? 8 : 2)); writer.Write((short)(adpcm ? 4 : 16));
			if (adpcm)
			{
				writer.Write(System.Text.Encoding.ASCII.GetBytes("fact")); writer.Write(4); writer.Write(9);
			}
			writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(pcm.Length); writer.Write(pcm);
			return output.ToArray();
		}

		static MusicInfo Music()
		{
			var music = new MusicInfo("test", new MiniYaml("Synthetic", new[] { new MiniYamlNode("Extension", "wav") }));
			typeof(MusicInfo).GetProperty("Exists").SetValue(music, true);
			return music;
		}

		[TestCase(false)]
		[TestCase(true)]
		public void StreamingMusicUsesIndependentDecoderFactoriesAfterOriginalLoadScopeCloses(bool adpcm)
		{
			var oldSettings = Game.Settings;
			var engine = new StreamingEngine();
			var pcm = adpcm ? new byte[] { 0, 64, 0, 64, 0, 64, 0, 64, 0, 64, 0, 64, 0, 64, 0, 64, 0, 64 } : new byte[] { 0, 64, 0, 192 };
			var files = new Files(Wav(adpcm ? new byte[] { 0, 64, 0, 0, 0, 0, 0, 0 } : pcm, adpcm));
			try
			{
				Game.Settings = new Settings(null, new Arguments());
				Log.AddChannel("sound", null);
				using var sound = new Sound(new PlatformStub(engine), new SoundSettings());
				sound.Initialize(new ISoundLoader[] { new WavLoader() }, files);
				sound.PlayMusic(Music(), true);
				Assert.That(engine.Open, Is.Not.Null);
				Assert.That(engine.LegacyCalls, Is.Zero, "iOS music must bypass whole-recording preload.");
				Assert.That(sound.MusicPlaying, Is.True);
				files.Closed = true;
				using var first = engine.Open();
				using var second = engine.Open();
				Assert.That(first.ReadByte(), Is.Zero);
				Assert.That(second.ReadAllBytes(), Is.EqualTo(pcm));
				Assert.That(first.ReadByte(), Is.EqualTo(64), "Reopening must not share a cursor.");
			}
			finally { Game.Settings = oldSettings; }
		}

		[Test]
		public void EnginesWithoutStreamingCapabilityKeepTheLegacyMusicPath()
		{
			var oldSettings = Game.Settings;
			var engine = new LegacyEngine();
			var pcm = new byte[] { 0, 64, 0, 192 };
			try
			{
				Game.Settings = new Settings(null, new Arguments());
				Log.AddChannel("sound", null);
				using var sound = new Sound(new PlatformStub(engine), new SoundSettings());
				sound.Initialize(new ISoundLoader[] { new WavLoader() }, new Files(Wav(pcm)));
				sound.PlayMusic(Music(), true);
				Assert.That(engine.LegacyCalls, Is.EqualTo(1));
				Assert.That(engine.LegacyPcm, Is.EqualTo(pcm));
			}
			finally { Game.Settings = oldSettings; }
		}

		[Test]
		public void NestedDiskSegmentsReopenTheExactMusicRangeAfterTheArchiveCloses()
		{
			var path = Path.GetTempFileName();
			try
			{
				File.WriteAllBytes(path, new byte[] { 99, 98, 97, 96, 95, 94, 0, 64, 0, 192, 93 });
				Func<Stream> open;
				using (var file = File.OpenRead(path))
				using (var outer = new SegmentStream(file, 4, 6))
				using (var inner = new SegmentStream(outer, 2, 4))
				{
					var before = file.Position;
					open = (Func<Stream>)typeof(Sound).GetMethod("IndependentMusicSource", BindingFlags.Static | BindingFlags.NonPublic)
						.Invoke(null, new object[] { inner });
					Assert.That(file.Position, Is.EqualTo(before), "Disk music setup must not read the entire archive.");
				}

				using var first = open();
				using var second = open();
				Assert.That(first, Is.TypeOf<SegmentStream>());
				Assert.That(first.ReadByte(), Is.Zero);
				Assert.That(second.ReadAllBytes(), Is.EqualTo(new byte[] { 0, 64, 0, 192 }));
				Assert.That(first.ReadByte(), Is.EqualTo(64));
			}
			finally { File.Delete(path); }
		}
	}
}
