#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using OpenRA.Graphics;
using OpenRA.Mods.Common.LoadScreens;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.RA2.Content;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Mods.RA2.LoadScreens
{
	// Shared desktop/iOS full-bleed cinematic splash.
	public sealed class BrandSplashLoadScreen : SheetLoadScreen
	{
		[FluentReference]
		const string Loading = "loadscreen-loading";
		const string BrandTitle = "NUKE HOUR";

		static readonly Color BrandTitleColor = Color.FromArgb(246, 214, 121);
		static readonly Color BrandTitleShadow = Color.FromArgb(192, 0, 0, 0);

		Size wallpaperSize;
		Sprite splash;
		Sprite croppedSplash;
		Sheet lastSheet;
		int lastDensity;
		Size lastResolution;
		Rectangle bounds;
		string importStage;
		int completed, total;
		string[] messages = Array.Empty<string>();

		public override void Init(ModData modData, Dictionary<string, string> info)
		{
			var selectedInfo = new Dictionary<string, string>(info);
			if (info.TryGetValue("Images", out var pool))
			{
				var images = pool.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0 && modData.DefaultFileSystem.Exists(x)).ToArray();
				if (images.Length > 0)
				{
					var statePath = Path.Combine(Platform.SupportDir, "nukehour-startup-wallpaper.txt");
					var previous = -1;
					try
					{
						if (File.Exists(statePath)) previous = Array.IndexOf(images, File.ReadAllText(statePath).Trim());
					}
					catch (IOException) { }
					catch (UnauthorizedAccessException) { }
					var selected = SplashWallpaperSelection.SelectIndex(images.Length, previous, Game.CosmeticRandom.Next);
					selectedInfo["Image"] = images[selected];
					selectedInfo.Remove("Image2x");
					selectedInfo.Remove("Image3x");
					if (info.TryGetValue("ImageSizes", out var sizeList))
					{
						var allImages = pool.Split(',').Select(x => x.Trim()).ToArray();
						var sizes = sizeList.Split(',');
						var dimensions = sizes[Array.IndexOf(allImages, images[selected])].Trim().Split('x');
						wallpaperSize = new Size(int.Parse(dimensions[0], CultureInfo.InvariantCulture), int.Parse(dimensions[1], CultureInfo.InvariantCulture));
					}
					try
					{
						Directory.CreateDirectory(Platform.SupportDir);
						File.WriteAllText(statePath, images[selected]);
					}
					catch (IOException) { }
					catch (UnauthorizedAccessException) { }
				}
			}

			base.Init(modData, selectedInfo);
			messages = FluentProvider.GetMessage(Loading).Split(',').Select(x => x.Trim()).ToArray();
		}

		public override bool BeforeLoad()
		{
			if (!base.BeforeLoad())
				return false;

			try
			{
				RetailMapInstaller.EnsureInstalled(ModData, (done, count, _) => UpdateImportProgress("loadscreen-preparing-maps", done, count));
				RetailCampaignInstaller.EnsureInstalled(ModData, (done, count, _) => UpdateImportProgress("loadscreen-preparing-campaign", done, count));
			}
			finally { importStage = null; }
			Display();
			return true;
		}

		void UpdateImportProgress(string stage, int done, int count)
		{
			importStage = FluentProvider.GetMessage(stage);
			completed = done;
			total = count;
			Display();
		}

		public override void DisplayInner(Renderer r, Sheet s, int density)
		{
			if (s != lastSheet || density != lastDensity)
			{
				lastSheet = s;
				lastDensity = density;
				splash = s == null ? null : new Sprite(s,
					new Rectangle(0, 0, wallpaperSize.Width > 0 ? wallpaperSize.Width : s.Size.Width,
						wallpaperSize.Height > 0 ? wallpaperSize.Height : s.Size.Height),
					TextureChannel.RGBA, 1f / density);
				croppedSplash = null;
			}

			if (r.Resolution != lastResolution)
			{
				lastResolution = r.Resolution;
				bounds = new Rectangle(0, 0, lastResolution.Width, lastResolution.Height);
				croppedSplash = null;
			}

			if (splash != null && croppedSplash == null)
			{
				var crop = StretchBackgroundWidget.CalculateAspectFillCrop(splash.Bounds, bounds.Size);
				var scale = splash.Size.X / splash.Bounds.Width;
				croppedSplash = new Sprite(splash.Sheet, crop, splash.ZRamp, splash.Offset,
					splash.Channel, splash.BlendMode, scale);
			}

			if (croppedSplash != null)
				WidgetUtils.DrawSprite(croppedSplash, new float2(bounds.X, bounds.Y), bounds.Size);

			// First-start conversion must still show progress before fonts are ready.
			if (importStage != null)
			{
				var width = Math.Max(1, r.Resolution.Width * 3 / 5);
				var x = (r.Resolution.Width - width) / 2;
				var y = r.Resolution.Height * 4 / 5;
				WidgetUtils.FillRectWithColor(new Rectangle(x, y, width, 12), Color.FromArgb(220, 20, 20, 20));
				var fraction = total > 0 ? completed / (double)total : 0;
				WidgetUtils.FillRectWithColor(new Rectangle(x + 2, y + 2, Math.Max(0, (int)((width - 4) * fraction)), 8), BrandTitleColor);
			}

			if (r.Fonts != null)
			{
				var titleFont = r.Fonts["Title"];
				var titleSize = titleFont.Measure(BrandTitle);
				var titlePosition = new float2(
					(r.Resolution.Width - titleSize.X) / 2,
					Math.Max(24, r.Resolution.Height / 12));
				titleFont.DrawTextWithShadow(BrandTitle, titlePosition,
					BrandTitleColor, BrandTitleShadow, 2);

				if (importStage != null)
				{
					var width = Math.Max(1, r.Resolution.Width * 3 / 5);
					var x = (r.Resolution.Width - width) / 2;
					var y = r.Resolution.Height * 4 / 5;
					var text = $"{importStage}  {completed}/{total}";
					var font = r.Fonts["Bold"];
					font.DrawTextWithShadow(text, new float2(x, y - font.Measure(text).Y - 12), Color.White, Color.Black, 1);
				}
				else if (messages.Length > 0)
				{
					var text = messages.Random(Game.CosmeticRandom);
					var textSize = r.Fonts["Bold"].Measure(text);
					r.Fonts["Bold"].DrawTextWithShadow(text,
						new float2(r.Resolution.Width - textSize.X - 20, r.Resolution.Height - textSize.Y - 20),
						Color.White, Color.Black, 1);
				}
			}
		}
	}
}
