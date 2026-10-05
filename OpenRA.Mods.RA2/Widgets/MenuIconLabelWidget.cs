using System;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	/// <summary>One visible-edge layout for the generated home icon and lettering.</summary>
	public sealed class MenuIconLabelWidget : ImageWidget
	{
		public string IconName;
		public bool Route;
		// Kept compatible with the ordinary wordmark configuration.
		public bool AlignLeft = true;
		Sprite cachedIcon, cachedLabel;
		Sprite[] iconStrips, labelStrips;

		// Same inset corners as the generated hover plates (TL, TR, BR, BL).
		static readonly int[,] StandardFaces = { { 122, 269, 690, 300, 427, 410 }, { 122, 444, 690, 454, 578, 575 }, { 122, 600, 690, 605, 734, 735 } };
		static readonly int[,] TabletFaces = { { 105, 367, 588, 392, 503, 490 }, { 105, 516, 588, 530, 638, 633 }, { 105, 660, 588, 663, 775, 780 } };
		static readonly int[,] UltrawideFaces = { { 109, 230, 597, 256, 367, 350 }, { 106, 380, 597, 389, 502, 498 }, { 105, 516, 597, 522, 639, 639 } };

		public MenuIconLabelWidget() { }

		MenuIconLabelWidget(MenuIconLabelWidget other) : base(other)
		{
			IconName = other.IconName;
			Route = other.Route;
			AlignLeft = other.AlignLeft;
		}

		public static Rectangle[] CalculateLayout(Size button, Size icon, Size label, bool route)
		{
			var h = Math.Max(1, button.Height);
			var left = button.Width * (route ? .12 : .10);
			var right = button.Width * (route ? .85 : .94);
			var slotWidth = h * (route ? .74 : .60);
			var iconScale = Math.Min(slotWidth / Math.Max(1, icon.Width),
				h * (route ? .58 : .56) / Math.Max(1, icon.Height));
			var labelScale = h * (route ? .42 : .38) / Math.Max(1, label.Height);
			var gap = Math.Max(2, h * (route ? .12 : .10));
			var width = slotWidth + gap + label.Width * labelScale;
			var fit = Math.Min(1, Math.Max(1, right - left) / Math.Max(1, width));
			iconScale *= fit;
			labelScale *= fit;
			slotWidth *= fit;
			gap = Math.Max(2, gap * fit);
			var iw = Math.Max(1, (int)Math.Round(icon.Width * iconScale));
			var ih = Math.Max(1, (int)Math.Round(icon.Height * iconScale));
			var lw = Math.Max(1, (int)Math.Round(label.Width * labelScale));
			var lh = Math.Max(1, (int)Math.Round(label.Height * labelScale));
			var edge = (int)Math.Round(left + slotWidth);
			return new[]
			{
				new Rectangle(edge - iw, (h - ih) / 2, iw, ih),
				new Rectangle(edge + (int)Math.Round(gap), (h - lh) / 2, lw, lh)
			};
		}

		public override void Draw()
		{
			var collection = GetImageCollection().Replace("cc-soviet-wordmarks-", "cc-soviet-compact-wordmarks-");
			var label = ChromeProvider.GetImage(collection, GetImageName());
			var iconCollection = IconName == "thanks" ? "cc-soviet-thanks-icon" :
				IconName == "about" || IconName == "quit" ? "cc-soviet-system-icons" : "cc-soviet-compact-icons";
			var icon = ChromeProvider.GetImage(iconCollection, IconName);
			var owner = ArtworkBounds();
			var rectangles = CalculateLayout(owner.Size,
				new Size((int)icon.Size.X, (int)icon.Size.Y),
				new Size((int)label.Size.X, (int)label.Size.Y), Route);
			if (Route && Parent is AdaptiveRouteButtonWidget route)
			{
				var resolution = Game.Renderer.Resolution;
				var profile = IosMainMenuLayout.ProfileFor(resolution.Width, resolution.Height);
				if (cachedIcon != icon)
				{
					cachedIcon = icon;
					iconStrips = CreateStrips(icon);
				}

				if (cachedLabel != label)
				{
					cachedLabel = label;
					labelStrips = CreateStrips(label);
				}

				DrawOnRoute(icon, iconStrips, rectangles[0], owner, profile, route.ArtworkRow);
				DrawOnRoute(label, labelStrips, rectangles[1], owner, profile, route.ArtworkRow);
				return;
			}

			WidgetUtils.DrawSprite(icon, new float2(owner.X + rectangles[0].X, owner.Y + rectangles[0].Y), rectangles[0].Size);
			WidgetUtils.DrawSprite(label, new float2(owner.X + rectangles[1].X, owner.Y + rectangles[1].Y), rectangles[1].Size);
		}

		Rectangle ArtworkBounds()
		{
			var owner = Parent.RenderBounds;
			var resolution = Game.Renderer.Resolution;
			var policy = IosMenuLayoutPolicy.Create(Platform.UsesMobileLayout,
				IosScreenMetrics.SnapshotFor(resolution));
			if (Parent is AdaptiveRouteButtonWidget route)
				return AdaptiveRouteButtonWidget.CalculateArtworkBounds(owner, resolution, policy.IsPhone, route.ArtworkRow);
			if (Parent is MacHomeUtilityButtonWidget)
				return MacHomeUtilityButtonWidget.CalculateArtworkBounds(owner, policy, Parent.Id);
			return owner;
		}

		public override void Tick()
		{
			if (!Route || Parent is not AdaptiveRouteButtonWidget route)
				return;

			var chevron = Parent.GetOrNull<LabelWidget>("CHEVRON");
			if (chevron == null)
				return;

			var resolution = Game.Renderer.Resolution;
			var profile = IosMainMenuLayout.ProfileFor(resolution.Width, resolution.Height);
			var owner = ArtworkBounds();
			var center = ProjectRoutePoint(owner.Size, profile, route.ArtworkRow, .93, .5);
			chevron.Bounds.X = (int)Math.Round(owner.X - Parent.RenderBounds.X + center.X - chevron.Bounds.Width / 2f);
			chevron.Bounds.Y = (int)Math.Round(owner.Y - Parent.RenderBounds.Y + center.Y - chevron.Bounds.Height / 2f);
		}

		public static float2 ProjectRoutePoint(Size button, IosMainMenuShellProfile profile, int row, double u, double v)
		{
			row = Math.Clamp(row, 0, 2);
			var tablet = profile == IosMainMenuShellProfile.Tablet;
			var ultra = profile == IosMainMenuShellProfile.Ultrawide;
			var face = tablet ? TabletFaces : ultra ? UltrawideFaces : StandardFaces;
			var referenceWidth = tablet ? 1448 : ultra ? 1844 : 1672;
			var referenceHeight = tablet ? 1086 : ultra ? 853 : 941;
			var routeY = tablet ? (row == 0 ? .336 : row == 1 ? .474 : .607) : (row == 0 ? .286 : row == 1 ? .446 : .627);
			// Homography for a plate with vertical left/right edges. The divisor
			// creates actual distance scaling, not just a rotation or shear.
			var leftHeight = face[row, 5] - face[row, 1];
			var rightHeight = face[row, 4] - face[row, 3];
			var g = (double)leftHeight / rightHeight - 1;
			var denominator = 1 + g * u;
			var x = ((face[row, 2] * (g + 1) - face[row, 0]) * u + face[row, 0]) / denominator;
			var y = ((face[row, 3] * (g + 1) - face[row, 1]) * u + leftHeight * v + face[row, 1]) / denominator;
			return new float2(
				(float)((x - referenceWidth * (tablet ? .054 : .059)) * button.Width / (referenceWidth * (tablet ? .36 : .355))),
				(float)((y - referenceHeight * routeY) * button.Height / (referenceHeight * (tablet ? .122 : .145))));
		}

		static Sprite[] CreateStrips(Sprite source)
		{
			// Small cached quads avoid the diagonal distortion of one affine quad.
			var count = Math.Min(16, source.Bounds.Width);
			var strips = new Sprite[count];
			for (var i = 0; i < count; i++)
			{
				var left = source.Bounds.Left + source.Bounds.Width * i / count;
				var right = source.Bounds.Left + source.Bounds.Width * (i + 1) / count;
				strips[i] = new Sprite(source.Sheet, Rectangle.FromLTRB(left, source.Bounds.Top, right, source.Bounds.Bottom),
					source.Channel);
			}

			return strips;
		}

		static void DrawOnRoute(Sprite source, Sprite[] strips, Rectangle placement, Rectangle owner, IosMainMenuShellProfile profile, int row)
		{
			float3 Point(double x, double y)
			{
				var p = ProjectRoutePoint(owner.Size, profile, row, x / owner.Width, y / owner.Height);
				return new float3(owner.X + p.X, owner.Y + p.Y, 0);
			}

			foreach (var strip in strips)
			{
				var left = placement.Left + placement.Width * (double)(strip.Bounds.Left - source.Bounds.Left) / source.Bounds.Width;
				var right = placement.Left + placement.Width * (double)(strip.Bounds.Right - source.Bounds.Left) / source.Bounds.Width;
				Game.Renderer.RgbaSpriteRenderer.DrawSprite(strip,
					Point(left, placement.Top), Point(right, placement.Top), Point(right, placement.Bottom), Point(left, placement.Bottom),
					new float3(1, 1, 1), 1);
			}
		}

		public override Widget Clone() => new MenuIconLabelWidget(this);
	}
}
