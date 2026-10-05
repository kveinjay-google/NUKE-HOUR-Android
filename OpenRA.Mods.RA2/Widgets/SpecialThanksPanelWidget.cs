using System;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	// Controls and artwork share the same aspect-fill transform.
	public sealed class SpecialThanksPanelWidget : StretchBackgroundWidget
	{
		public SpecialThanksPanelWidget() { }
		SpecialThanksPanelWidget(SpecialThanksPanelWidget other) : base(other) { }
		public override Widget Clone() => new SpecialThanksPanelWidget(this);

		public static Rectangle MapRegion(Size source, Size target, double x, double y, double w, double h)
		{
			var crop = CalculateAspectFillCrop(new Rectangle(0, 0, source.Width, source.Height), target);
			return new Rectangle((int)Math.Round((x * source.Width - crop.X) * target.Width / crop.Width),
				(int)Math.Round((y * source.Height - crop.Y) * target.Height / crop.Height),
				(int)Math.Round(w * source.Width * target.Width / crop.Width),
				(int)Math.Round(h * source.Height * target.Height / crop.Height));
		}

		public static Size SourceSizeFor(string profile) => profile switch
		{
			"phone" => new Size(1844, 853),
			"tablet" => new Size(1448, 1086),
			_ => new Size(1672, 941),
		};

		public static Rectangle SupporterRollBoundsFor(string profile, Size target)
		{
			var (y, height) = profile switch
			{
				"phone" => (.285, .26),
				"tablet" => (.30, .235),
				_ => (.295, .245),
			};
			return MapRegion(SourceSizeFor(profile), target, .31, y, .38, height);
		}

		public static Rectangle CurtainSafeBoundsFor(string profile, Size target)
		{
			var (y, height) = profile switch
			{
				"phone" => (.205, .35),
				"tablet" => (.235, .305),
				_ => (.23, .32),
			};
			return MapRegion(SourceSizeFor(profile), target, .29, y, .42, height);
		}

		public static Rectangle TitleBoundsFor(string profile, Size target)
		{
			var (y, height) = profile switch
			{
				"phone" => (.085, .075),
				"tablet" => (.14, .07),
				_ => (.13, .075),
			};
			return MapRegion(SourceSizeFor(profile), target, .37, y, .26, height);
		}

		public static Rectangle TitleFrameSafeBoundsFor(string profile, Size target)
		{
			var (y, height) = profile switch
			{
				"phone" => (.07, .105),
				"tablet" => (.125, .10),
				_ => (.115, .11),
			};
			return MapRegion(SourceSizeFor(profile), target, .35, y, .30, height);
		}

		static double ActionButtonCenterX(string profile, int index)
		{
			if ((uint)index >= 3)
				throw new ArgumentOutOfRangeException(nameof(index));

			return profile switch
			{
				"phone" => index == 0 ? .267 : index == 1 ? .5 : .733,
				"tablet" => index == 0 ? .26 : index == 1 ? .5 : .74,
				_ => index == 0 ? .265 : index == 1 ? .5 : .735,
			};
		}

		public static Rectangle ActionButtonBoundsFor(string profile, Size target, int index)
		{
			var centerX = ActionButtonCenterX(profile, index);
			var (centerY, height) = profile switch
			{
				"phone" => (.858, .06),
				"tablet" => (.808, .055),
				_ => (.843, .055),
			};
			return MapRegion(SourceSizeFor(profile), target, centerX - .09, centerY - height / 2, .18, height);
		}

		public static Rectangle ActionButtonSafeBoundsFor(string profile, Size target, int index)
		{
			var centerX = ActionButtonCenterX(profile, index);
			var (y, height) = profile switch
			{
				"phone" => (.815, .085),
				"tablet" => (.765, .085),
				_ => (.798, .09),
			};
			return MapRegion(SourceSizeFor(profile), target, centerX - .11, y, .22, height);
		}

		public static string ProfileFor(bool isIos, IosScreenSnapshot snapshot)
		{
			if (!isIos)
				return "desktop";

			return IosMenuLayoutPolicy.Create(true, snapshot).IsPhone ? "phone" : "tablet";
		}

		void UpdateLayout()
		{
			var size = Bounds.ToRectangle().Size;
			var profile = ProfileFor(Platform.UsesMobileLayout, IosScreenMetrics.SnapshotFor(size));
			var tablet = profile == "tablet";
			var source = SourceSizeFor(profile);
			Background = "thanks-" + profile + "-v3";
			WideBackground = "";
			PreserveAspectRatio = true;
			void SetBounds(string id, Rectangle rect)
			{
				var child = GetOrNull(id);
				if (child != null)
					child.Bounds = new WidgetBounds(rect.X, rect.Y, rect.Width, rect.Height);
			}

			void Place(string id, double x, double y, double w, double h)
			{
				SetBounds(id, MapRegion(source, size, x, y, w, h));
			}

			SetBounds("TITLE", TitleBoundsFor(profile, size));
			var title = GetOrNull("TITLE");
			if (title != null && title.Bounds.Height < 56)
				title.Bounds = new WidgetBounds(title.Bounds.X,
					title.Bounds.Y - (56 - title.Bounds.Height) / 2, title.Bounds.Width, 56);
			Place("INTRO", .31, .225, .38, .065);
			SetBounds("SUPPORTER_ROLL", SupporterRollBoundsFor(profile, size));
			Place("PRIVACY_NOTE", .29, tablet ? .58 : .61, .42, .05);
			var ids = new[] { "SUPPORT_BUTTON", "MORE_BUTTON", "BACK_BUTTON" };
			for (var i = 0; i < ids.Length; i++)
				SetBounds(ids[i], ActionButtonBoundsFor(profile, size, i));
		}

		public override void Tick() { UpdateLayout(); base.Tick(); }
		public override void Draw() { UpdateLayout(); base.Draw(); }
	}
}
