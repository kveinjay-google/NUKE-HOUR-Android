using System;
using OpenRA.Primitives;

namespace OpenRA.Platforms.Default
{
	/// <summary>
	/// Android platform implementation (Phase 6). Window/GL come from the shared
	/// SDL sources, sound is the SDL audio engine, fonts are FreeType. Impact feedback uses the native Android vibrator.
	/// </summary>
	public sealed class AndroidPlatform : IPlatform
	{
		public static Action<float> RequestRenderFrameRate = _ => { };

		public IPlatformWindow CreateWindow(
			Size size, WindowMode windowMode, float scaleModifier, int vertexBatchSize, int indexBatchSize, int videoDisplay, GLProfile profile)
		{
			var window = new Sdl2PlatformWindow(size, windowMode, scaleModifier, vertexBatchSize, indexBatchSize, videoDisplay, profile);
			RequestRenderFrameRate(Game.Settings.Graphics.CapFramerate ? Game.Settings.Graphics.MaxFramerate : 0);
			return window;
		}

		public ISoundEngine CreateSound(string device)
		{
			try
			{
				return new Sdl2SoundEngine();
			}
			catch (Exception e)
			{
				Log.Write("sound", $"SDL Android audio unavailable; continuing without audio: {e}");
				return new DummySoundEngine();
			}
		}

		public IHapticEngine CreateHaptics()
		{
			return new AndroidHapticEngine();
		}

		public IFont CreateFont(byte[] data)
		{
			return new FreeTypeFont(data);
		}
	}
}
