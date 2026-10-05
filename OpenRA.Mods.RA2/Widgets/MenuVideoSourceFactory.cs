using System;

namespace OpenRA.Mods.RA2.Widgets
{
	public interface IMenuVideoSource : IDisposable
	{
		int Width { get; }
		int Height { get; }
		double Fps { get; }
		double Duration { get; }
		bool ReadFrame(int index, byte[] bgraPixels);
	}

	// Installed by the Android host; all calls occur on the decoder's serial worker.
	public static class MenuVideoSourceFactory
	{
		public static Func<string, IMenuVideoSource> AndroidOpen { get; set; }
	}
}
