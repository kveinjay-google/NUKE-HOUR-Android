using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace OpenRA.Mods.RA2.Widgets
{
	// One serial worker owns the AVAssetReader. No decode or native waits on the UI thread.
	sealed class MenuVideoDecoder : IDisposable
	{
		internal sealed class Frame
		{
			public int Width, Height;
			public double Fps, Duration;
			public byte[] Pixels;
		}

		[DllImport("nukehour_menu_video", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr nh_video_open([MarshalAs(UnmanagedType.LPUTF8Str)] string path,
			out int width, out int height, out double fps, out double duration);
		[DllImport("nukehour_menu_video", CallingConvention = CallingConvention.Cdecl)]
		static extern int nh_video_read(IntPtr session, [Out] byte[] pixels, int capacity);
		[DllImport("nukehour_menu_video", CallingConvention = CallingConvention.Cdecl)]
		static extern int nh_video_rewind(IntPtr session);
		[DllImport("nukehour_menu_video", CallingConvention = CallingConvention.Cdecl)]
		static extern void nh_video_close(IntPtr session);

		static class IosNative
		{
			[DllImport("__Internal", EntryPoint = "nh_video_open", CallingConvention = CallingConvention.Cdecl)]
			internal static extern IntPtr Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path,
				out int width, out int height, out double fps, out double duration);
			[DllImport("__Internal", EntryPoint = "nh_video_read", CallingConvention = CallingConvention.Cdecl)]
			internal static extern int Read(IntPtr session, [Out] byte[] pixels, int capacity);
			[DllImport("__Internal", EntryPoint = "nh_video_rewind", CallingConvention = CallingConvention.Cdecl)]
			internal static extern int Rewind(IntPtr session);
			[DllImport("__Internal", EntryPoint = "nh_video_close", CallingConvention = CallingConvention.Cdecl)]
			internal static extern void Close(IntPtr session);
		}

		IntPtr session;
		IMenuVideoSource androidSource;
		Frame frame;
		Task<Frame> pending;
		int position = -1;
		int requested = -1;
		volatile bool disposed;
		public bool Failed { get; private set; }

		public MenuVideoDecoder(string path)
		{
			pending = Task.Run(() =>
			{
				try
				{
					int width, height;
					double fps, duration;
					if (Platform.IsAndroid)
					{
						androidSource = MenuVideoSourceFactory.AndroidOpen?.Invoke(path) ??
							throw new InvalidOperationException("Android menu video source is unavailable");
						width = androidSource.Width;
						height = androidSource.Height;
						fps = androidSource.Fps;
						duration = androidSource.Duration;
					}
					else
						session = Platform.IsIOS ? IosNative.Open(path, out width, out height, out fps, out duration) :
							nh_video_open(path, out width, out height, out fps, out duration);
					if ((session == IntPtr.Zero && androidSource == null) || width <= 0 || height <= 0 || width > 1920 || height > 1080 ||
						!double.IsFinite(fps) || !double.IsFinite(duration) || fps <= 0 || duration <= 0)
						throw new InvalidDataException("Unsupported menu video");
					frame = new Frame { Width = width, Height = height, Fps = fps, Duration = duration,
						Pixels = new byte[checked(width * height * 4)] };
					return Decode(0);
				}
				catch (Exception e)
				{
					Log.Write("debug", "Menu video fallback: " + e.Message);
					Close();
					return null;
				}
			});
		}

		Frame Decode(int target)
		{
			try
			{
				if (androidSource != null)
				{
					if (!androidSource.ReadFrame(target, frame.Pixels))
						throw new InvalidDataException("Could not decode Android menu video frame");
					position = target;
					return frame;
				}
				if (target < position)
				{
					if ((Platform.IsIOS ? IosNative.Rewind(session) : nh_video_rewind(session)) != 1)
						throw new InvalidDataException("Could not loop menu video");
					position = -1;
				}
				while (position < target && !disposed)
				{
					if ((Platform.IsIOS ? IosNative.Read(session, frame.Pixels, frame.Pixels.Length) :
						nh_video_read(session, frame.Pixels, frame.Pixels.Length)) != 1)
						throw new InvalidDataException("Could not decode menu video frame");
					position++;
				}
				return disposed ? null : frame;
			}
			catch (Exception e)
			{
				Log.Write("debug", "Menu video fallback: " + e.Message);
				Close();
				return null;
			}
		}

		public Frame TakeReadyFrame()
		{
			if (pending == null || !pending.IsCompleted || disposed)
				return null;
			var result = pending.GetAwaiter().GetResult();
			pending = null;
			Failed = result == null;
			return result;
		}

		public void Request(int index)
		{
			if (disposed || Failed || pending != null || requested == index)
				return;
			requested = index;
			pending = Task.Run(() => Decode(index));
		}

		void Close()
		{
			androidSource?.Dispose();
			androidSource = null;
			if (session != IntPtr.Zero)
			{
				if (Platform.IsIOS)
					IosNative.Close(session);
				else
					nh_video_close(session);
				session = IntPtr.Zero;
			}
			frame = null;
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;
			if (pending != null)
				_ = pending.ContinueWith(_ => Close(), TaskScheduler.Default);
			else
				_ = Task.Run(Close);
		}
	}
}
