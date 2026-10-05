using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace OpenRA.Support
{
	public sealed class PcmAudioMixer
	{
		const int ListenerHeight = 2133;
		const int ReferenceDistance = 6826;
		const int MaximumDistance = 136533;
		const int RolloffFactor = 1;

		readonly object sync = new();
		readonly List<PcmPlayback> playbacks = new();
		readonly int outputRate;
		readonly int outputChannels;
		WPos listenerPosition;

		public float Volume { get; set; } = 1f;

		public int ActivePlaybackCount
		{
			get
			{
				lock (sync)
					return playbacks.Count;
			}
		}

		public PcmAudioMixer(int outputRate, int outputChannels)
		{
			this.outputRate = outputRate;
			this.outputChannels = outputChannels;
		}

		public PcmPlayback Play(byte[] data, int channels, int sampleBits, int sampleRate, bool loop, float volume)
		{
			return Play(data, channels, sampleBits, sampleRate, loop, volume, true, WPos.Zero);
		}

		public PcmPlayback Play(byte[] data, int channels, int sampleBits, int sampleRate, bool loop, float volume,
			bool relative, WPos position)
		{
			if (channels is < 1 or > 2 || sampleBits is not (8 or 16) || sampleRate <= 0)
				throw new ArgumentOutOfRangeException(nameof(channels), "Only mono/stereo 8/16-bit PCM is supported.");

			var playback = new PcmPlayback(data, channels, sampleBits, sampleRate, loop, volume, relative, position);
			lock (sync)
				playbacks.Add(playback);

			return playback;
		}

		public void SetListenerPosition(WPos position)
		{
			lock (sync)
				listenerPosition = position + new WVec(0, 0, ListenerHeight);
		}

		public PcmPlayback PlayStream(Func<Stream> open, int channels, int sampleBits, int sampleRate,
			bool loop, float volume, bool relative, WPos position)
		{
			ValidateStreamFormat(channels, sampleBits, sampleRate);
			var playback = new PcmPlayback(Array.Empty<byte>(), channels, sampleBits, sampleRate, loop, volume, relative, position);
			playback.stream = new PcmStreamBuffer(open, channels, sampleBits, loop);
			lock (sync)
				playbacks.Add(playback);
			return playback;
		}

		static void ValidateStreamFormat(int channels, int sampleBits, int sampleRate)
		{
			if (channels is < 1 or > 2 || sampleBits is not (8 or 16) || sampleRate <= 0)
				throw new ArgumentOutOfRangeException(nameof(channels), "Only mono/stereo 8/16-bit PCM is supported.");
		}

		public void SetPlaybackPosition(PcmPlayback playback, WPos position)
		{
			if (playback == null)
				return;

			lock (sync)
				playback.Position = position;
		}

		public void Stop(PcmPlayback playback)
		{
			if (playback == null)
				return;

			lock (sync)
			{
				playback.Stopped = true;
				playback.Complete = true;
				playback.Data = Array.Empty<byte>();
				playback.stream?.Dispose();
				playbacks.Remove(playback);
			}
		}

		public void StopAll()
		{
			lock (sync)
			{
				foreach (var playback in playbacks)
				{
					playback.Stopped = true;
					playback.Complete = true;
					playback.Data = Array.Empty<byte>();
					playback.stream?.Dispose();
				}

				playbacks.Clear();
			}
		}

		public void Mix(float[] output)
		{
			Array.Clear(output, 0, output.Length);
			lock (sync)
			{
				for (var i = playbacks.Count - 1; i >= 0; i--)
				{
					var sound = playbacks[i];
					if (sound.Stopped)
					{
						sound.Complete = true;
						sound.Data = Array.Empty<byte>();
						sound.stream?.Dispose();
						playbacks.RemoveAt(i);
						continue;
					}

					if (sound.Paused)
						continue;

					MixSound(sound, output);
					if (sound.Complete)
					{
						sound.Data = Array.Empty<byte>();
						sound.stream?.Dispose();
						playbacks.RemoveAt(i);
					}
				}
			}
		}

		void MixSound(PcmPlayback sound, float[] output)
		{
			if (sound.stream != null)
			{
				MixStream(sound, output);
				return;
			}

			var outputFrames = output.Length / outputChannels;
			var sourceFrames = sound.Data.Length / (sound.Channels * sound.BytesPerSample);
			if (sourceFrames == 0)
			{
				sound.Complete = true;
				return;
			}

			var distanceGain = DistanceGain(sound);
			for (var frame = 0; frame < outputFrames; frame++)
			{
				var sourceFrame = (int)sound.FramePosition;
				if (sourceFrame >= sourceFrames)
				{
					if (!sound.Loop)
					{
						sound.Complete = true;
						break;
					}

					sound.FramePosition %= sourceFrames;
					sourceFrame = (int)sound.FramePosition;
				}

				for (var channel = 0; channel < outputChannels; channel++)
				{
					var sourceChannel = sound.Channels == 1 ? 0 : Math.Min(channel, sound.Channels - 1);
					var sample = ReadSample(sound, sourceFrame, sourceChannel) * sound.Volume * Volume * distanceGain;
					var index = frame * outputChannels + channel;
					output[index] = Math.Clamp(output[index] + sample, -1f, 1f);
				}

				sound.FramePosition += (double)sound.SampleRate / outputRate;
			}
		}

		void MixStream(PcmPlayback sound, float[] output)
		{
			var gain = sound.Volume * Volume * DistanceGain(sound);
			for (var frame = 0; frame < output.Length / outputChannels; frame++)
			{
				var target = (long)sound.FramePosition;
				while (sound.StreamFrame < target)
				{
					var result = sound.stream.TryReadFrame(out var left, out var right, out var recordingFrame);
					if (result == 2)
					{
						// Match the memory mixer at each recording boundary rather
						// than accumulating rounding error across an entire session.
						sound.FramePosition %= recordingFrame;
						sound.StreamFrame = -1;
						target = (long)sound.FramePosition;
						continue;
					}
					if (result <= 0)
					{
						if (result < 0) sound.Complete = true;
						return;
					}

					sound.StreamLeft = left;
					sound.StreamRight = right;
					sound.StreamFrame++;
				}

				for (var channel = 0; channel < outputChannels; channel++)
				{
					var sample = (channel == 0 ? sound.StreamLeft : sound.StreamRight) * gain;
					var index = frame * outputChannels + channel;
					output[index] = Math.Clamp(output[index] + sample, -1f, 1f);
				}

				sound.FramePosition += (double)sound.SampleRate / outputRate;
			}
		}

		float DistanceGain(PcmPlayback sound)
		{
			if (sound.Relative)
				return 1f;

			var distance = Math.Clamp((sound.Position - listenerPosition).Length, ReferenceDistance, MaximumDistance);
			return ReferenceDistance /
				(float)(ReferenceDistance + RolloffFactor * (distance - ReferenceDistance));
		}

		static float ReadSample(PcmPlayback sound, int frame, int channel)
		{
			var offset = (frame * sound.Channels + channel) * sound.BytesPerSample;
			if (sound.SampleBits == 16)
				return (short)(sound.Data[offset] | sound.Data[offset + 1] << 8) / 32768f;

			return (sound.Data[offset] - 128) / 128f;
		}
	}

	public sealed class PcmPlayback
	{
		internal byte[] Data;
		internal PcmStreamBuffer stream;
		internal long StreamFrame = -1;
		internal float StreamLeft, StreamRight;
		internal readonly int Channels;
		internal readonly int SampleBits;
		internal readonly int SampleRate;
		internal readonly int BytesPerSample;
		internal double FramePosition;
		internal volatile bool Stopped;
		internal readonly bool Relative;
		internal WPos Position;

		bool loop;
		public bool Loop
		{
			get => Volatile.Read(ref loop);
			set { Volatile.Write(ref loop, value); if (stream != null) stream.Loop = value; }
		}
		public bool Paused { get; set; }
		public bool Complete { get; internal set; }
		public float Volume { get; set; }
		public float SeekPosition => (float)(FramePosition / SampleRate);

		internal PcmPlayback(byte[] data, int channels, int sampleBits, int sampleRate, bool loop, float volume,
			bool relative, WPos position)
		{
			Data = data;
			Channels = channels;
			SampleBits = sampleBits;
			SampleRate = sampleRate;
			BytesPerSample = sampleBits / 8;
			Loop = loop;
			Volume = volume;
			Relative = relative;
			Position = position;
		}

		public void Stop() => Stopped = true;
	}
}
