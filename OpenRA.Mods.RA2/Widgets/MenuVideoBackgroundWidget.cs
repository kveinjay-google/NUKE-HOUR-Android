using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Mods.RA2.Widgets
{
	// The static shell is always drawn first. Only the measured wallpaper aperture
	// receives video pixels; existing controls/dialogs are later siblings above it.
	public class MenuVideoBackgroundWidget : StretchBackgroundWidget
	{
		public string Video = "ra2|media/menu-background.mp4";
		public string IosVideo = "ra2|media/ios-home-background.mp4";
		public string Profile = "standard";
		MenuVideoDecoder decoder;
		MenuVideoDecoder.Frame frame;
		Sheet sheet;
		Sprite sprite;
		byte[] upload;
		byte[] displayedPixels;
		readonly List<(int Offset, int Length)> clearRanges = new();
		readonly Stopwatch clock = new();
		Size lastSize;
		Rectangle target;
		bool attempted;
		bool loggedPlaybackGate;

		public MenuVideoBackgroundWidget() { }

		protected MenuVideoBackgroundWidget(MenuVideoBackgroundWidget other)
			: base(other)
		{
			Video = other.Video;
			IosVideo = other.IosVideo;
			Profile = other.Profile;
		}

		public override OpenRA.Widgets.Widget Clone() => new MenuVideoBackgroundWidget(this);

		// Subpages retain the shell behind them, but only the home menu animates it.
		public bool ShouldPlayVideo => Parent?.GetOrNull("MAIN_MENU")?.IsVisible() == true;

		public override void DrawOuter()
		{
			// Profile/subpage predicates can hide this widget without calling Hidden.
			// The ordinary DrawOuter then skips Draw, so release before that guard.
			if (!IsVisible())
			{
				ReleaseVideo();
				return;
			}

			base.DrawOuter();
		}

		public override void Draw()
		{
			base.Draw();
			if (!loggedPlaybackGate)
			{
				loggedPlaybackGate = true;
				Log.Write("debug", $"Home video gate: home={ShouldPlayVideo}, focus={Game.Renderer.WindowHasInputFocus}, paused={Game.Settings.Game.PauseShellmap}");
			}
			if (!ShouldPlayVideo || !Game.Renderer.WindowHasInputFocus)
			{
				clock.Stop();
				if (!ShouldPlayVideo)
					ReleaseVideo();
				return;
			}

			if (!attempted)
			{
				attempted = true;
				var video = Platform.UsesMobileLayout ? IosVideo : Video;
				if ((OperatingSystem.IsMacOS() || Platform.UsesMobileLayout) &&
					Game.ModData.DefaultFileSystem.TryGetPackageContaining(video, out var package, out var file) &&
					package is Folder)
					decoder = new MenuVideoDecoder(Platform.ResolvePath(Path.Combine(package.Name, file)));
				else
					Log.Write("debug", "Home video source unavailable: " + video);
			}

			if (decoder == null || decoder.Failed)
				return;
			var ready = decoder.TakeReadyFrame();
			if (ready != null)
			{
				if (frame == null && Platform.UsesMobileLayout)
					{
					Log.Write("debug", $"Home video ready: {ready.Width}x{ready.Height}, {ready.Fps} fps");
					Console.WriteLine($"NUKE HOUR home video ready: {ready.Width}x{ready.Height}, {ready.Fps} fps; silent, home-only.");
				}
				frame = ready;
				displayedPixels ??= new byte[ready.Pixels.Length];
				Buffer.BlockCopy(ready.Pixels, 0, displayedPixels, 0, ready.Pixels.Length);
			}
			if (frame == null)
				return;
			if (Game.Settings.Game.PauseShellmap)
				clock.Stop();
			else
				clock.Start();

			var resized = lastSize != RenderBounds.Size;
			if (sheet == null || resized)
				ConfigureTexture();
			if (ready != null || resized)
			{
				for (var y = 0; y < frame.Height; y++)
					Buffer.BlockCopy(displayedPixels, y * frame.Width * 4, upload, y * sheet.Size.Width * 4, frame.Width * 4);
				foreach (var (offset, length) in clearRanges)
					Array.Clear(upload, offset, length);
				Game.Renderer.Flush();
				sheet.GetTexture().SetData(upload, sheet.Size.Width, sheet.Size.Height);
			}

			WidgetUtils.DrawSprite(sprite, new float2(RenderBounds.X + target.X, RenderBounds.Y + target.Y), target.Size);
			decoder.Request(MenuVideoAperture.FrameAt(clock.Elapsed.TotalSeconds, frame.Fps, frame.Duration));
		}

		void ConfigureTexture()
		{
			lastSize = RenderBounds.Size;
			var left = (int)Math.Round(lastSize.Width * MenuVideoAperture.Left(Profile));
			target = new Rectangle(left, 0, Math.Max(1, lastSize.Width - left),
				Math.Max(1, (int)Math.Round(lastSize.Height * MenuVideoAperture.Bottom(Profile))));
			var crop = CalculateAspectFillCrop(new Rectangle(0, 0, frame.Width, frame.Height), target.Size);
			if (sheet == null)
			{
				sheet = new Sheet(SheetType.BGRA, new Size(Exts.NextPowerOf2(frame.Width), Exts.NextPowerOf2(frame.Height)));
				upload = new byte[4 * sheet.Size.Width * sheet.Size.Height];
				sheet.GetTexture().ScaleFilter = TextureScaleFilter.Linear;
			}
			sprite = new Sprite(sheet, crop, TextureChannel.RGBA);
			clearRanges.Clear();
			for (var y = 0; y < frame.Height; y++)
			{
				var start = -1;
				for (var x = 0; x <= frame.Width; x++)
				{
					var screenX = (target.X + (x + .5 - crop.X) / crop.Width * target.Width) / lastSize.Width;
					var screenY = (y + .5 - crop.Y) / crop.Height * target.Height / lastSize.Height;
					var clear = x < frame.Width && !MenuVideoAperture.Contains(Profile, screenX, screenY);
					if (clear && start < 0)
						start = x;
					else if (!clear && start >= 0)
					{
						clearRanges.Add(((y * sheet.Size.Width + start) * 4, (x - start) * 4));
						start = -1;
					}
				}
			}
		}

		public override void Hidden()
		{
			ReleaseVideo();
			base.Hidden();
		}

		void ReleaseVideo()
		{
			clock.Reset();
			decoder?.Dispose();
			decoder = null;
			if (sheet != null)
				Game.Renderer?.Flush();
			sheet?.Dispose();
			sheet = null;
			sprite = null;
			frame = null;
			upload = null;
			displayedPixels = null;
			clearRanges.Clear();
			attempted = false;
		}

		public override void Removed()
		{
			ReleaseVideo();
			base.Removed();
		}
	}
}
