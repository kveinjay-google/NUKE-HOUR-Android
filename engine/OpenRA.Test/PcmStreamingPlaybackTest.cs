using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenRA.Support;

namespace OpenRA.Test
{
	[TestFixture]
	public class PcmStreamingPlaybackTest
	{
		sealed class GeneratedPcm : Stream
		{
			readonly byte[] pattern;
			readonly long length;
			readonly int shortRead;
			long readBytes;
			public long ReadBytes => Interlocked.Read(ref readBytes);
			int disposed;
			public bool Disposed => Volatile.Read(ref disposed) != 0;
			public int ReadThread;
			public GeneratedPcm(byte[] pattern, long length, int shortRead = int.MaxValue)
			{
				this.pattern = pattern;
				this.length = length;
				this.shortRead = shortRead;
			}

			public override bool CanRead => !Disposed;
			public override bool CanSeek => false;
			public override bool CanWrite => false;
			public override long Length => throw new NotSupportedException();
			public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
			public override int Read(byte[] buffer, int offset, int count)
			{
				ReadThread = Environment.CurrentManagedThreadId;
				count = (int)Math.Min(Math.Min(count, shortRead), length - ReadBytes);
				for (var i = 0; i < count; i++)
					buffer[offset + i] = pattern[(ReadBytes + i) % pattern.Length];
				Interlocked.Add(ref readBytes, count);
				return count;
			}

			protected override void Dispose(bool disposing) { Volatile.Write(ref disposed, 1); base.Dispose(disposing); }
			public override void Flush() => throw new NotSupportedException();
			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
			public override void SetLength(long value) => throw new NotSupportedException();
			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		}

		static PcmPlayback Play(PcmAudioMixer mixer, Func<Stream> open, int channels = 2,
			int bits = 16, int rate = 48000, bool loop = false, float volume = 1f)
		{
			var method = typeof(PcmAudioMixer).GetMethod("PlayStream", new[]
			{
				typeof(Func<Stream>), typeof(int), typeof(int), typeof(int), typeof(bool),
				typeof(float), typeof(bool), typeof(WPos)
			});
			Assert.That(method, Is.Not.Null, "Streaming PCM must not preload the entire recording.");
			return (PcmPlayback)method.Invoke(mixer, new object[] { open, channels, bits, rate, loop, volume, true, WPos.Zero });
		}

		static object Queue(PcmPlayback playback) => typeof(PcmPlayback)
			.GetField("stream", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(playback);
		static Task Completion(PcmPlayback playback) => (Task)Queue(playback).GetType().GetProperty("Completion").GetValue(Queue(playback));
		static int Buffered(PcmPlayback playback) => (int)Queue(playback).GetType().GetProperty("BufferedBytes").GetValue(Queue(playback));
		static void Until(Func<bool> condition) => Assert.That(SpinWait.SpinUntil(condition, 5000), Is.True, "Background decoder made no progress.");

		[Test]
		public void LongRecordingReadsAheadOnlyOneFixedQueueAndNeverOnTheCaller()
		{
			var stream = new GeneratedPcm(new byte[] { 0, 64, 0, 64 }, 300L * 48000 * 4);
			var mixer = new PcmAudioMixer(48000, 2);
			var caller = Environment.CurrentManagedThreadId;
			var sound = Play(mixer, () => stream);
			try
			{
				Until(() => Buffered(sound) == 65536);
				Assert.That(stream.ReadBytes, Is.EqualTo(65536));
				Assert.That(stream.ReadThread, Is.Not.EqualTo(caller));
				TestContext.WriteLine($"fiveMinuteDecodedBytes={300L * 48000 * 4}, queuedDecodedBytes={Buffered(sound)}");
			}
			finally { mixer.Stop(sound); }
			Assert.That(Completion(sound).Wait(5000), Is.True);
			Assert.That(stream.Disposed, Is.True);
			Assert.That(Buffered(sound), Is.Zero);
		}

		[TestCase(1, 8, 22050)]
		[TestCase(2, 8, 48000)]
		[TestCase(1, 16, 22050)]
		[TestCase(2, 16, 48000)]
		[TestCase(2, 16, 96000)]
		public void StreamedShortReadsMatchExistingMemorySamplesAndResampling(int channels, int bits, int rate)
		{
			var data = new byte[16384 + 128];
			for (var i = 0; i < data.Length; i++) data[i] = (byte)(i % 251);
			var stream = new GeneratedPcm(data, data.Length, 7);
			var mixer = new PcmAudioMixer(48000, 2);
			var reference = new PcmAudioMixer(48000, 2);
			var sound = Play(mixer, () => stream, channels, bits, rate, volume: .4f);
			try
			{
				Until(() => stream.Disposed);
				reference.Play(data, channels, bits, rate, false, .4f);
				var actual = new float[100000];
				var expected = new float[actual.Length];
				mixer.Mix(actual);
				reference.Mix(expected);
				Assert.That(actual, Is.EqualTo(expected));
				Assert.That(sound.Complete, Is.True);
				Assert.That(mixer.ActivePlaybackCount, Is.Zero);
			}
			finally { mixer.Stop(sound); }
		}

		[Test]
		public void NonSeekableRepeatReopensAndStopsAtNextEndWhenRepeatIsDisabled()
		{
			var opens = 0;
			var disposals = 0;
			var mixer = new PcmAudioMixer(48000, 2);
			var sound = Play(mixer, () =>
			{
				Interlocked.Increment(ref opens);
				return new TrackingStream(new byte[] { 0, 64, 0, 192 }, () => Interlocked.Increment(ref disposals));
			}, channels: 1, loop: true);
			try
			{
				Until(() => Volatile.Read(ref opens) >= 4);
				var output = new float[12];
				mixer.Mix(output);
				Assert.That(output, Is.EqualTo(new[] { .5f, .5f, -.5f, -.5f, .5f, .5f, -.5f, -.5f, .5f, .5f, -.5f, -.5f }));
				sound.Loop = false;
				mixer.Mix(new float[12]);
				Assert.That(sound.Complete, Is.True);
			}
			finally { mixer.Stop(sound); }
			Assert.That(Completion(sound).Wait(5000), Is.True);
			Assert.That(disposals, Is.EqualTo(opens));
		}

		[TestCase(22050)]
		[TestCase(32000)]
		[TestCase(96000)]
		public void RepeatedUnequalRatePlaybackMatchesMemoryAtRecordingBoundaries(int rate)
		{
			var data = new byte[] { 0, 64, 0, 192 };
			var mixer = new PcmAudioMixer(48000, 2);
			var reference = new PcmAudioMixer(48000, 2);
			var sound = Play(mixer, () => new MemoryStream(data), channels: 1, rate: rate, loop: true);
			var referenceSound = reference.Play(data, 1, 16, rate, true, 1f);
			try
			{
				for (var block = 0; block < 30; block++)
				{
					Until(() => Buffered(sound) >= 12);
					var actual = new float[6];
					var expected = new float[6];
					mixer.Mix(actual);
					reference.Mix(expected);
					Assert.That(actual, Is.EqualTo(expected), $"block={block}, rate={rate}");
					Assert.That(sound.SeekPosition, Is.EqualTo(referenceSound.SeekPosition));
				}
			}
			finally { mixer.Stop(sound); reference.StopAll(); Completion(sound).Wait(5000); }
		}

		[Test]
		public void RepeatCanBeEnabledAfterDecoderReachesEndButBeforePlaybackDoes()
		{
			var opens = 0;
			var mixer = new PcmAudioMixer(48000, 2);
			var sound = Play(mixer, () => { Interlocked.Increment(ref opens); return new MemoryStream(new byte[] { 0, 64 }); }, channels: 1);
			try
			{
				Until(() => Buffered(sound) == 2);
				sound.Loop = true;
				Until(() => Volatile.Read(ref opens) >= 4);
				var output = new float[6];
				mixer.Mix(output);
				Assert.That(output, Is.All.EqualTo(.5f));
				Assert.That(sound.Complete, Is.False);
			}
			finally { mixer.Stop(sound); }
		}

		[Test]
		public void PauseKeepsTimeAndBufferConsumptionFixedThenResumes()
		{
			var stream = new GeneratedPcm(new byte[] { 0, 64, 0, 64 }, 1000000);
			var mixer = new PcmAudioMixer(48000, 2);
			var sound = Play(mixer, () => stream);
			try
			{
				Until(() => Buffered(sound) == 65536);
				mixer.Mix(new float[100]);
				sound.Paused = true;
				Until(() => stream.ReadBytes == 65536);
				var time = sound.SeekPosition;
				var bytes = Buffered(sound);
				var output = new float[100];
				mixer.Mix(output);
				Assert.That(output, Is.All.Zero);
				Assert.That(sound.SeekPosition, Is.EqualTo(time));
				Assert.That(Buffered(sound), Is.EqualTo(bytes));
				sound.Paused = false;
				mixer.Mix(output);
				Assert.That(output, Is.All.EqualTo(.5f));
			}
			finally { mixer.Stop(sound); }
		}

		sealed class TrackingStream : MemoryStream
		{
			readonly Action disposed;
			public TrackingStream(byte[] data, Action disposed) : base(data) { this.disposed = disposed; }
			public override bool CanSeek => false;
			protected override void Dispose(bool disposing) { if (disposing) disposed(); base.Dispose(disposing); }
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(3)]
		public void EmptyOrPartialFirstFrameTerminatesWithoutInfiniteReopen(int bytes)
		{
			var opens = 0;
			var mixer = new PcmAudioMixer(48000, 2);
			var sound = Play(mixer, () => { Interlocked.Increment(ref opens); return new MemoryStream(new byte[bytes]); }, loop: true);
			try
			{
				Until(() => Completion(sound).IsCompleted);
				Assert.DoesNotThrow(() => mixer.Mix(new float[32]));
				Assert.That(sound.Complete, Is.True);
				Assert.That(opens, Is.EqualTo(1));
			}
			finally { mixer.Stop(sound); }
		}

		[Test]
		public void StopDuringBlockedReadDoesNotWaitOrCloseOnTheAudioConsumer()
		{
			using var entered = new ManualResetEventSlim();
			using var release = new ManualResetEventSlim();
			var stream = new BlockingStream(entered, release);
			var mixer = new PcmAudioMixer(48000, 2);
			var sound = Play(mixer, () => stream);
			try
			{
				Assert.That(entered.Wait(5000), Is.True);
				var stop = Task.Run(() => mixer.Stop(sound));
				Assert.That(stop.Wait(1000), Is.True, "Stop must not join a decoder doing IO.");
				Assert.That(stream.Disposed, Is.False, "Only the read owner can close the stream.");
				Assert.That(sound.Complete, Is.True);
				Assert.That(Buffered(sound), Is.Zero);
			}
			finally { release.Set(); mixer.Stop(sound); Completion(sound).Wait(5000); }
			Assert.That(stream.Disposed, Is.True);
		}

		[Test]
		public void UnderrunReturnsSilenceWithoutWaitingOrAdvancingThenRecovers()
		{
			using var entered = new ManualResetEventSlim();
			using var release = new ManualResetEventSlim();
			var stream = new BlockingStream(entered, release, new byte[] { 0, 64, 0, 64 });
			var mixer = new PcmAudioMixer(48000, 2);
			var sound = Play(mixer, () => stream);
			try
			{
				Assert.That(entered.Wait(5000), Is.True);
				var output = new float[2];
				Assert.That(Task.Run(() => mixer.Mix(output)).Wait(1000), Is.True);
				Assert.That(output, Is.All.Zero);
				Assert.That(sound.SeekPosition, Is.Zero);
				Assert.That(sound.Complete, Is.False);
				release.Set();
				Until(() => Buffered(sound) == 4);
				mixer.Mix(output);
				Assert.That(output, Is.All.EqualTo(.5f));
				Assert.That(sound.SeekPosition, Is.EqualTo(1f / 48000));
			}
			finally { release.Set(); mixer.Stop(sound); Completion(sound).Wait(5000); }
		}

		[Test]
		public void StopDuringDecoderOpenClosesTheLateInputWithoutReadingIt()
		{
			using var entered = new ManualResetEventSlim();
			using var release = new ManualResetEventSlim();
			var stream = new GeneratedPcm(new byte[] { 0, 64, 0, 64 }, 1000000);
			var mixer = new PcmAudioMixer(48000, 2);
			var sound = Play(mixer, () => { entered.Set(); release.Wait(); return stream; });
			try
			{
				Assert.That(entered.Wait(5000), Is.True);
				Assert.That(Task.Run(() => mixer.Stop(sound)).Wait(1000), Is.True);
				Assert.That(stream.Disposed, Is.False);
			}
			finally { release.Set(); mixer.Stop(sound); Completion(sound).Wait(5000); }
			Assert.That(stream.Disposed, Is.True);
			Assert.That(stream.ReadBytes, Is.Zero);
		}

		[TestCase("open")]
		[TestCase("read")]
		[TestCase("close")]
		public void DecoderFaultsCompleteWithoutEscapingOrLeakingTheInput(string failure)
		{
			Log.AddChannel("sound", null);
			var stream = new FaultStream(failure);
			var mixer = new PcmAudioMixer(48000, 2);
			var sound = Play(mixer, () => failure == "open" ? throw new IOException("Synthetic open error") : stream);
			try
			{
				Assert.That(Completion(sound).Wait(5000), Is.True);
				Assert.That(Completion(sound).IsFaulted, Is.False);
				var output = new float[16];
				Assert.DoesNotThrow(() => mixer.Mix(output));
				Assert.That(output, Is.All.Zero);
				Assert.That(sound.Complete, Is.True);
				Assert.That(Buffered(sound), Is.Zero);
				if (failure != "open") Assert.That(stream.CanRead, Is.False);
			}
			finally { mixer.Stop(sound); stream.DisposeQuietly(); }
		}

		sealed class FaultStream : MemoryStream
		{
			readonly string failure;
			public FaultStream(string failure) : base(new byte[4]) { this.failure = failure; }
			public override int Read(byte[] buffer, int offset, int count) => failure == "read" ? throw new IOException("Synthetic read error") : base.Read(buffer, offset, count);
			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing);
				if (failure == "close") throw new IOException("Synthetic close error");
			}
			public void DisposeQuietly() { try { Dispose(); } catch (IOException) { } }
		}

		[Test]
		public void RepeatedSwitchAndStopAllReleaseEveryProducerAndQueue()
		{
			var mixer = new PcmAudioMixer(48000, 2);
			for (var cycle = 0; cycle < 50; cycle++)
			{
				var first = new GeneratedPcm(new byte[] { 0, 64, 0, 64 }, 1000000);
				var second = new GeneratedPcm(new byte[] { 0, 64, 0, 64 }, 1000000);
				var a = Play(mixer, () => first);
				var b = Play(mixer, () => second);
				try { Until(() => Buffered(a) == 65536 && Buffered(b) == 65536); }
				finally { mixer.StopAll(); }
				Assert.That(Task.WaitAll(new[] { Completion(a), Completion(b) }, 5000), Is.True);
				Assert.That(first.Disposed && second.Disposed, Is.True);
				Assert.That(Buffered(a) + Buffered(b), Is.Zero);
				Assert.That(mixer.ActivePlaybackCount, Is.Zero);
			}
		}

		[Test]
		public void MixingBufferedMusicDoesNotAllocateOnTheAudioConsumer()
		{
			var mixer = new PcmAudioMixer(48000, 2);
			var stream = new GeneratedPcm(new byte[] { 0, 64, 0, 64 }, 1000000);
			var sound = Play(mixer, () => stream);
			try
			{
				Until(() => Buffered(sound) == 65536);
				mixer.Mix(new float[2]);
				var output = new float[16384];
				var started = System.Diagnostics.Stopwatch.GetTimestamp();
				var before = GC.GetAllocatedBytesForCurrentThread();
				mixer.Mix(output);
				var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
				var elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000000.0 / System.Diagnostics.Stopwatch.Frequency;
				Assert.That(output, Is.All.EqualTo(.5f));
				Assert.That(allocated, Is.Zero, "Streaming must not allocate sample objects on every mix callback.");
				TestContext.WriteLine($"mixedFrames={output.Length / 2}, mixElapsedMicroseconds={elapsed:F0}, consumerAllocatedBytes={allocated}");
			}
			finally { mixer.Stop(sound); Completion(sound).Wait(5000); }
		}

		sealed class BlockingStream : MemoryStream
		{
			readonly ManualResetEventSlim entered, release;
			public bool Disposed;
			public BlockingStream(ManualResetEventSlim entered, ManualResetEventSlim release, byte[] data = null) : base(data ?? new byte[4])
			{ this.entered = entered; this.release = release; }
			public override int Read(byte[] buffer, int offset, int count) { entered.Set(); release.Wait(); return base.Read(buffer, offset, count); }
			protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
		}
	}
}
