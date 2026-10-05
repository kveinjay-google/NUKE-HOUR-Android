#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OpenRA.FileSystem;
using OpenRA.GameRules;
using OpenRA.Primitives;

namespace OpenRA
{
	public interface ISoundLoader
	{
		bool TryParseSound(Stream stream, out ISoundFormat sound);
	}

	public interface ISoundFormat : IDisposable
	{
		int Channels { get; }
		int SampleBits { get; }
		int SampleRate { get; }
		float LengthInSeconds { get; }
		Stream GetPCMInputStream();
	}

	public enum SoundType { World, UI }

	public sealed class Sound : IDisposable
	{
		readonly ISoundEngine soundEngine;
		ISoundLoader[] loaders;
		IReadOnlyFileSystem fileSystem;
		Cache<string, ISoundSource> sounds;
		ISoundSource videoSource;
		ISound music;
		ISound video;
		readonly Dictionary<uint, ISound> currentSounds = new();
		readonly Dictionary<string, ISound> currentNotifications = new();
		WPos listenerPosition;
		bool muted;
		public bool DummyEngine { get; }

		public Sound(IPlatform platform, SoundSettings soundSettings)
		{
			soundEngine = platform.CreateSound(soundSettings.Device);
			DummyEngine = soundEngine.Dummy;

			if (soundSettings.Mute)
				MuteAudio();
		}

		T LoadSound<T>(string filename, Func<ISoundFormat, T> loadFormat)
			=> LoadSound(filename, (format, _) => loadFormat(format));

		T LoadSound<T>(string filename, Func<ISoundFormat, Stream, T> loadFormat)
		{
			if (!fileSystem.Exists(filename))
			{
				Log.Write("sound", $"LoadSound, file does not exist: {filename}");
				return default;
			}

			try
			{
				using (var stream = fileSystem.Open(filename))
				{
					foreach (var loader in loaders)
					{
						stream.Position = 0;
						if (loader.TryParseSound(stream, out var soundFormat))
						{
							using (soundFormat)
							{
								ValidatePcmFormat(soundFormat.Channels, soundFormat.SampleBits, soundFormat.SampleRate);
								return loadFormat(soundFormat, stream);
							}
						}
					}
				}

				throw new InvalidDataException(filename + " is not a valid sound file!");
			}
			catch (Exception e) when (e is InvalidDataException || e is IOException || e is NotSupportedException)
			{
				Log.Write("sound", $"Failed to load sound '{filename}': {e}");
				return default;
			}
		}

		static void ValidatePcmFormat(int channels, int sampleBits, int sampleRate)
		{
			if (channels is < 1 or > 2 || sampleBits is not (8 or 16) || sampleRate <= 0)
				throw new NotSupportedException(
					$"Only mono/stereo 8/16-bit PCM is supported (channels={channels}, bits={sampleBits}, rate={sampleRate}).");
		}

		// Resolve archive access on the game thread. Background decoders only
		// open independent disk segments or immutable copies of encoded data.
		static Func<Stream> IndependentMusicSource(Stream input)
		{
			var length = input.Length;
			if (length > int.MaxValue)
				throw new InvalidDataException("Music source exceeds the supported size.");
			var offset = 0L;
			var parent = input;
			while (parent is SegmentStream segment)
			{
				offset += segment.BaseOffset;
				parent = segment.BaseStream;
			}

			if (parent.GetType() == typeof(FileStream))
			{
				var path = ((FileStream)parent).Name;
				return () => new SegmentStream(File.OpenRead(path), offset, length);
			}

			input.Position = 0;
			var encoded = input.ReadBytes((int)length);
			return () => new MemoryStream(encoded, false);
		}

		public void Initialize(ISoundLoader[] loaders, IReadOnlyFileSystem fileSystem)
		{
			StopMusic();
			soundEngine.StopAllSounds();

			if (sounds != null)
				foreach (var soundSource in sounds.Values)
					soundSource?.Dispose();

			this.loaders = loaders;
			this.fileSystem = fileSystem;
			ISoundSource LoadIntoMemory(ISoundFormat soundFormat) => soundEngine.AddSoundSourceFromMemory(
				soundFormat.GetPCMInputStream().ReadAllBytes(), soundFormat.Channels, soundFormat.SampleBits, soundFormat.SampleRate);
			sounds = new Cache<string, ISoundSource>(filename => LoadSound(filename, LoadIntoMemory));
			currentSounds.Clear();
			currentNotifications.Clear();
			video = null;
		}

		public SoundDevice[] AvailableDevices()
		{
			return soundEngine.AvailableDevices();
		}

		public void SetListenerPosition(WPos position)
		{
			listenerPosition = position + new WVec(0, 0, 2133);
			soundEngine.SetListenerPosition(position);
		}

		public float WorldHapticStrength(WPos position, float volumeModifier = 1f)
		{
			if (muted || DisableAllSounds || DisableWorldSounds)
				return 0f;

			return HapticStrengthPolicy.Resolve(InternalSoundVolume * volumeModifier, listenerPosition, position);
		}

		ISound Play(SoundType type, Player player, string name, bool headRelative, WPos pos, float volumeModifier = 1f, bool loop = false)
		{
			if (string.IsNullOrEmpty(name) || DisableAllSounds || (DisableWorldSounds && type == SoundType.World))
				return null;

			if (player != null && player != player.World.LocalPlayer)
				return null;

			var source = sounds[name];
			if (source == null)
				return null;

			return soundEngine.Play2D(source,
				loop, headRelative, pos,
				InternalSoundVolume * volumeModifier, true);
		}

		public void StopAudio()
		{
			soundEngine.StopAllSounds();
		}

		public void SetLooped(ISound sound, bool looped)
		{
			soundEngine.SetSoundLooping(looped, sound);
		}

		public void SetPosition(ISound sound, WPos position)
		{
			soundEngine.SetSoundPosition(sound, position);
		}

		public void MuteAudio()
		{
			muted = true;
			soundEngine.Volume = 0f;
		}

		public void UnmuteAudio()
		{
			muted = false;
			soundEngine.Volume = 1f;
		}

		public void SetMusicLooped(bool loop)
		{
			Game.Settings.Sound.Repeat = loop;
			soundEngine.SetSoundLooping(loop, music);
		}

		public bool DisableAllSounds { get; set; }
		public bool DisableWorldSounds { get; set; }
		public ISound Play(SoundType type, string name) { return Play(type, null, name, true, WPos.Zero, 1f); }
		public ISound Play(SoundType type, string name, WPos pos) { return Play(type, null, name, false, pos, 1f); }
		public ISound Play(SoundType type, string name, float volumeModifier) { return Play(type, null, name, true, WPos.Zero, volumeModifier); }
		public ISound Play(SoundType type, string name, WPos pos, float volumeModifier) { return Play(type, null, name, false, pos, volumeModifier); }
		public ISound PlayToPlayer(SoundType type, Player player, string name) { return Play(type, player, name, true, WPos.Zero, 1f); }
		public ISound PlayToPlayer(SoundType type, Player player, string name, WPos pos) { return Play(type, player, name, false, pos, 1f); }
		public ISound PlayLooped(SoundType type, string name) { return Play(type, null, name, true, WPos.Zero, 1f, true); }
		public ISound PlayLooped(SoundType type, string name, WPos pos) { return Play(type, null, name, false, pos, 1f, true); }

		public ISound Play(SoundType type, string[] names, World world, Player player = null, float volumeModifier = 1f)
		{
			return Play(type, player, names.Random(world.LocalRandom), true, WPos.Zero, volumeModifier);
		}

		public ISound Play(SoundType type, string[] names, World world, WPos pos, Player player = null, float volumeModifier = 1f)
		{
			return Play(type, player, names.Random(world.LocalRandom), false, pos, volumeModifier);
		}

		public ISound Play(ISoundFormat soundFormat) => Play(soundFormat, MusicVolume);

		public ISound Play(ISoundFormat soundFormat, float volume)
		{
			try
			{
				ValidatePcmFormat(soundFormat.Channels, soundFormat.SampleBits, soundFormat.SampleRate);
				return soundEngine.Play2DStream(soundFormat.GetPCMInputStream(), soundFormat.Channels,
					soundFormat.SampleBits, soundFormat.SampleRate, false, true, WPos.Zero, volume);
			}
			catch (Exception e) when (e is InvalidDataException || e is IOException || e is NotSupportedException)
			{
				Log.Write("sound", $"Failed to play decoded sound: {e}");
				return null;
			}
		}

		public void PlayVideo(byte[] raw, int channels, int sampleBits, int sampleRate)
		{
			StopVideo();
			videoSource = soundEngine.AddSoundSourceFromMemory(raw, channels, sampleBits, sampleRate);
			video = soundEngine.Play2D(videoSource, false, true, WPos.Zero, InternalSoundVolume, false);
		}

		public void PlayVideo()
		{
			if (video != null)
				soundEngine.PauseSound(video, false);
		}

		public void PauseVideo()
		{
			if (video != null)
				soundEngine.PauseSound(video, true);
		}

		public void StopVideo()
		{
			if (video != null)
			{
				soundEngine.StopSound(video);
				videoSource.Dispose();
				videoSource = null;
				video = null;
			}
		}

		public void Tick()
		{
#if DEBUG
			if (Platform.IsAndroid && Game.RunTime - lastMusicHeartbeat >= 2000)
			{
				lastMusicHeartbeat = Game.RunTime;
				Log.Write("debug", $"music heartbeat: playing={MusicPlaying} current={CurrentMusic?.Filename ?? "null"} seek={music?.SeekPosition ?? -1}");
			}
#endif
			// Song finished
			if (MusicPlaying && music.Complete)
			{
				StopMusic();
				onMusicComplete();
			}
		}

		long lastMusicHeartbeat;

		Action onMusicComplete;
		public bool MusicPlaying { get; private set; }
		public MusicInfo CurrentMusic { get; private set; }

		public void PlayMusicThen(MusicInfo m, Action then)
		{
			if (m == null || !m.Exists)
				return;

			onMusicComplete = then;

			if (m == CurrentMusic && music != null)
			{
				soundEngine.PauseSound(music, false);
				MusicPlaying = true;
				return;
			}

			PlayMusic(m, Game.Settings.Sound.Repeat);
		}

		public void PlayMusic(MusicInfo m, bool looped = false)
		{
			if (!Platform.IsAndroid)
			{
				PlayMusicImpl(m, looped);
				return;
			}

			// Android: RA2 music decodes for ~10s per track. Decode off the main
			// thread AND serialize everything so only one track can ever play:
			// a new request supersedes the current one, stopping it cleanly.
			lock (musicSync)
			{
				if (androidDisposed)
					return;

				musicRequestVersion++;
				androidQueuedMusic = m;
				androidQueuedLooped = looped;
				if (!androidBusy)
				{
					androidBusy = true;
					var worker = Task.Run(AndroidMusicLoop);
				}
			}
		}

		readonly object musicSync = new();
		int musicRequestVersion;
		MusicInfo androidQueuedMusic;
		bool androidQueuedLooped;
		bool androidBusy;
		bool androidDisposed;

		void AndroidMusicLoop()
		{
			while (true)
			{
				lock (musicSync)
				{
					if (androidDisposed)
					{
						androidBusy = false;
						return;
					}
				}

				MusicInfo target;
				bool looped;
				lock (musicSync)
				{
					if (androidQueuedMusic == null)
					{
						androidBusy = false;
						return;
					}

					target = androidQueuedMusic;
					looped = androidQueuedLooped;
					androidQueuedMusic = null;
				}

				int version;
				lock (musicSync)
					version = musicRequestVersion;

				try
				{
					PlayMusicImpl(target, looped);
				}
				catch (Exception e)
				{
					Log.Write("debug", "android async music failed: " + e);
				}

				// A stop or a newer request arrived while decoding: discard what
				// we just started (without clearing the queue) so nothing ever
				// plays in parallel; the loop then picks up the newer request.
				lock (musicSync)
				{
					if (version != musicRequestVersion)
						StopMusicCore();
				}
			}
		}

		void PlayMusicImpl(MusicInfo m, bool looped)
		{
			if (m == null || !m.Exists)
				return;

			IosLoadStageJournal.Record("music-stop-begin", $"file={m.Filename}");
			StopMusicCore();
			IosLoadStageJournal.Record("music-stop-end", $"file={m.Filename}");

			ISound Stream(ISoundFormat soundFormat, Stream encoded)
			{
				IosLoadStageJournal.Record("music-stream-begin", $"file={m.Filename}");
				ISound sound;
				if (soundEngine is IStreamingSoundEngine streaming)
				{
					var source = IndependentMusicSource(encoded);
					var soundLoaders = loaders;
					var channels = soundFormat.Channels;
					var bits = soundFormat.SampleBits;
					var rate = soundFormat.SampleRate;
					Stream Open()
					{
						using var input = source();
						foreach (var loader in soundLoaders)
						{
							input.Position = 0;
							if (!loader.TryParseSound(input, out var format)) continue;
							using (format)
							{
								if (format.Channels != channels || format.SampleBits != bits || format.SampleRate != rate)
									throw new InvalidDataException("Music PCM format changed during playback.");
								return format.GetPCMInputStream();
							}
						}

						throw new InvalidDataException("Music source is no longer a valid sound file.");
					}
					sound = streaming.Play2DStream(Open, channels, bits, rate, looped, true, WPos.Zero, MusicVolume * m.VolumeModifier);
				}
				else
					sound = soundEngine.Play2DStream(
						soundFormat.GetPCMInputStream(), soundFormat.Channels, soundFormat.SampleBits, soundFormat.SampleRate,
						looped, true, WPos.Zero, MusicVolume * m.VolumeModifier);
				IosLoadStageJournal.Record("music-stream-end", $"file={m.Filename}");
				return sound;
			}

			IosLoadStageJournal.Record("music-load-begin", $"file={m.Filename}");
#if DEBUG
			var loadWatch = System.Diagnostics.Stopwatch.StartNew();
#endif
			music = LoadSound(m.Filename, Stream);
#if DEBUG
			loadWatch.Stop();
			Log.Write("debug", $"android music load: file={m.Filename} ms={loadWatch.ElapsedMilliseconds} loaded={music != null}");
#endif
			IosLoadStageJournal.Record("music-load-end", $"file={m.Filename} loaded={music != null}");
			if (music == null)
			{
				onMusicComplete = null;
				return;
			}

			CurrentMusic = m;
			MusicPlaying = true;

			// The volume may have been changed while the track was still
			// decoding; apply the live value to the newly started sound too.
			if (music != null)
				music.Volume = Game.Settings.Sound.MusicVolume;
		}

		public void PlayMusic()
		{
			if (music == null)
				return;

			MusicPlaying = true;
			soundEngine.PauseSound(music, false);
		}

		public void StopSound(ISound sound)
		{
			if (sound != null)
				soundEngine.StopSound(sound);
		}

		public void StopMusic()
		{
			lock (musicSync)
			{
				musicRequestVersion++;
				androidQueuedMusic = null;
			}

			StopMusicCore();
		}

		void StopMusicCore()
		{
			if (music != null)
			{
				soundEngine.StopSound(music);
				music = null;
			}

			CurrentMusic = null;
			MusicPlaying = false;
		}

		public void PauseMusic()
		{
			lock (musicSync)
				musicRequestVersion++;

			if (music == null)
				return;

			MusicPlaying = false;
			soundEngine.PauseSound(music, true);
		}

		float soundVolumeModifier = 1.0f;
		public float SoundVolumeModifier
		{
			get => soundVolumeModifier;

			set
			{
				soundVolumeModifier = value;
				soundEngine.SetSoundVolume(InternalSoundVolume, music, video);
			}
		}

		float InternalSoundVolume => SoundVolume * soundVolumeModifier;

		public float SoundVolume
		{
			get => Game.Settings.Sound.SoundVolume;

			set
			{
				Game.Settings.Sound.SoundVolume = value;
				soundEngine.SetSoundVolume(InternalSoundVolume, music, video);
			}
		}

		public float MusicVolume
		{
			get => Game.Settings.Sound.MusicVolume;

			set
			{
				Game.Settings.Sound.MusicVolume = value;
				if (music != null)
					music.Volume = value;
			}
		}

		public float VideoVolume
		{
			get => Game.Settings.Sound.VideoVolume;

			set
			{
				Game.Settings.Sound.VideoVolume = value;
				if (video != null)
					video.Volume = value;
			}
		}

		public float MusicSeekPosition => music?.SeekPosition ?? 0;

		public float VideoSeekPosition => video?.SeekPosition ?? 0;

		// Returns true if played successfully
		public bool PlayPredefined(SoundType soundType, Ruleset ruleset, Player player, Actor voicedActor, string type, string definition, string variant,
			bool relative, WPos pos, float volumeModifier, bool attenuateVolume)
		{
			if (ruleset == null)
				throw new ArgumentNullException(nameof(ruleset));

			if (definition == null || DisableAllSounds || (DisableWorldSounds && soundType == SoundType.World))
				return false;

			if (ruleset.Voices == null || ruleset.Notifications == null)
				return false;

			var rules = voicedActor != null ? ruleset.Voices[type] : ruleset.Notifications[type];
			if (rules == null)
				return false;

			var id = voicedActor?.ActorID ?? 0;

			SoundPool pool;
			var suffix = rules.DefaultVariant;
			var prefix = rules.DefaultPrefix;

			if (voicedActor != null)
			{
				if (!rules.VoicePools.Value.TryGetValue(definition, out var p))
					throw new InvalidOperationException($"Can't find {definition} in voice pool.");

				pool = p;
			}
			else
			{
				if (!rules.NotificationsPools.Value.TryGetValue(definition, out var p))
					throw new InvalidOperationException($"Can't find {definition} in notification pool.");

				pool = p;
			}

			var clip = pool.GetNext();
			if (string.IsNullOrEmpty(clip))
				return false;

			if (variant != null)
			{
				if (rules.Variants.TryGetValue(variant, out var v) && !rules.DisableVariants.Contains(definition))
					suffix = v[id % v.Length];
				if (rules.Prefixes.TryGetValue(variant, out var p) && !rules.DisablePrefixes.Contains(definition))
					prefix = p[id % p.Length];
			}

			var name = prefix + clip + suffix;
			var actorId = voicedActor != null && voicedActor.World.Selection.Contains(voicedActor) ? 0 : id;

			if (!string.IsNullOrEmpty(name) && (player == null || player == player.World.LocalPlayer))
			{
				ISound PlaySound()
				{
					var source = sounds[name];
					if (source == null)
						return null;

					var volume = InternalSoundVolume * volumeModifier * pool.VolumeModifier;
					return soundEngine.Play2D(source, false, relative, pos, volume, attenuateVolume);
				}

				if (pool.Type == SoundPool.InterruptType.Overlap)
				{
					if (PlaySound() == null)
						return false;
				}
				else if (voicedActor == null)
				{
					if (currentNotifications.TryGetValue(name, out var currentNotification) && !currentNotification.Complete)
					{
						if (pool.Type == SoundPool.InterruptType.Interrupt)
							soundEngine.StopSound(currentNotification);
						else if (pool.Type == SoundPool.InterruptType.DoNotPlay)
							return false;
					}

					var sound = PlaySound();
					if (sound == null)
						return false;
					else
						currentNotifications[name] = sound;
				}
				else
				{
					if (currentSounds.TryGetValue(actorId, out var currentSound) && !currentSound.Complete)
					{
						if (pool.Type == SoundPool.InterruptType.Interrupt)
							soundEngine.StopSound(currentSound);
						else if (pool.Type == SoundPool.InterruptType.DoNotPlay)
							return false;
					}

					var sound = PlaySound();
					if (sound == null)
						return false;
					else
						currentSounds[actorId] = sound;
				}
			}

			return true;
		}

		public bool PlayNotification(Ruleset rules, Player player, string type, string notification, string variant)
		{
			if (rules == null)
				throw new ArgumentNullException(nameof(rules));

			if (type == null || notification == null)
				return false;

			return PlayPredefined(SoundType.UI, rules, player, null, type.ToLowerInvariant(), notification, variant, true, WPos.Zero, 1f, false);
		}

		public void Dispose()
		{
			if (Platform.IsAndroid)
				lock (musicSync)
				{
					androidDisposed = true;
					androidQueuedMusic = null;
				}

			StopAudio();
			if (sounds != null)
				foreach (var soundSource in sounds.Values)
					soundSource?.Dispose();

			soundEngine.Dispose();
		}
	}
}
