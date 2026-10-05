using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OpenRA.Support
{
	// One producer owns IO. The mixer never waits for a decoder or disposes a
	// stream being read. Metadata preserves recording ends across read-ahead.
	public sealed class PcmStreamBuffer : IDisposable
	{
		public const int CapacityBytes = 64 * 1024;
		const int ChunkBytes = 16 * 1024;
		const int ChunkCount = CapacityBytes / ChunkBytes;
		sealed class Chunk
		{
			public readonly byte[] Data = new byte[ChunkBytes];
			public int Length, Offset;
			public long FirstFrame;
			public bool End;
		}

		readonly object sync = new();
		readonly int channels, sampleBits, frameBytes;
		Func<Stream> open;
		Chunk[] chunks = new Chunk[ChunkCount];
		int reader, writer, count;
		bool loop;
		volatile bool stopped;
		public Task Completion { get; }

		public int BufferedBytes
		{
			get
			{
				lock (sync)
				{
					var bytes = 0;
					for (var i = 0; i < count; i++)
					{
						var chunk = chunks[(reader + i) % ChunkCount];
						bytes += chunk.Length - chunk.Offset;
					}

					return bytes;
				}
			}
		}

		public bool Loop
		{
			get { lock (sync) return loop; }
			set { lock (sync) { loop = value; Monitor.PulseAll(sync); } }
		}

		public PcmStreamBuffer(Func<Stream> open, int channels, int sampleBits, bool loop)
		{
			if (channels is < 1 or > 2 || sampleBits is not (8 or 16))
				throw new ArgumentOutOfRangeException(nameof(channels));
			this.open = open ?? throw new ArgumentNullException(nameof(open));
			this.channels = channels;
			this.sampleBits = sampleBits;
			frameBytes = channels * sampleBits / 8;
			this.loop = loop;
			for (var i = 0; i < chunks.Length; i++) chunks[i] = new Chunk();
			Completion = Task.Run(Produce);
		}

		void Produce()
		{
			Stream stream = null;
			var frame = 0L;
			try
			{
				while (!stopped)
				{
					if (stream == null)
						stream = open?.Invoke() ?? throw new InvalidDataException("PCM decoder returned no stream.");
					Chunk chunk;
					lock (sync)
					{
						while (!stopped && count == ChunkCount) Monitor.Wait(sync);
						if (stopped) return;
						chunk = chunks[writer];
					}

					var bytes = 0;
					var end = false;
					while (!stopped && bytes < ChunkBytes)
					{
						var read = stream.Read(chunk.Data, bytes, ChunkBytes - bytes);
						if (read < 0 || read > ChunkBytes - bytes)
							throw new InvalidDataException("PCM decoder returned an invalid byte count.");
						if (read == 0) { end = true; break; }
						bytes += read;
					}

					chunk.Offset = 0;
					chunk.Length = bytes - bytes % frameBytes;
					chunk.FirstFrame = frame;
					chunk.End = end;
					frame += chunk.Length / frameBytes;
					lock (sync)
					{
						if (stopped) return;
						if (end && frame == 0) { StopUnderLock(); return; }
						writer = (writer + 1) % ChunkCount;
						count++;
					}

					if (!end) continue;
					stream.Dispose();
					stream = null;

					lock (sync)
					{
						while (!stopped && !loop) Monitor.Wait(sync);
						if (stopped) return;
					}

					frame = 0;
				}
			}
			catch (Exception error)
			{
				Dispose();
				try { Log.Write("sound", "PCM streaming stopped: " + error.Message); } catch { }
			}
			finally
			{
				try { stream?.Dispose(); } catch { }
				open = null;
			}
		}

		// 2 = loop boundary (recordingFrame is its length), 1 = sample,
		// 0 = underrun, -1 = end/failure. Reading a frame may
		// consume an end marker, but never performs IO or waits for the producer.
		public int TryReadFrame(out float left, out float right, out long recordingFrame)
		{
			left = right = 0;
			recordingFrame = 0;
			lock (sync)
			{
				while (!stopped && count > 0)
				{
					var chunk = chunks[reader];
					if (chunk.Offset == chunk.Length)
					{
						reader = (reader + 1) % ChunkCount;
						count--;
						Monitor.PulseAll(sync);
						if (chunk.End && !loop) { StopUnderLock(); return -1; }
						if (chunk.End)
						{
							recordingFrame = chunk.FirstFrame + chunk.Length / frameBytes;
							return 2;
						}
						continue;
					}

					recordingFrame = chunk.FirstFrame + chunk.Offset / frameBytes;
					left = ReadSample(chunk.Data, chunk.Offset);
					right = channels == 1 ? left : ReadSample(chunk.Data, chunk.Offset + sampleBits / 8);
					chunk.Offset += frameBytes;
					return 1;
				}

				return stopped ? -1 : 0;
			}
		}

		float ReadSample(byte[] data, int offset) => sampleBits == 16 ?
			(short)(data[offset] | data[offset + 1] << 8) / 32768f : (data[offset] - 128) / 128f;

		void StopUnderLock()
		{
			stopped = true;
			count = 0;
			chunks = Array.Empty<Chunk>();
			Monitor.PulseAll(sync);
		}

		public void Dispose() { lock (sync) StopUnderLock(); }
	}
}
