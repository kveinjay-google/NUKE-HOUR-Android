using System.Linq;
using NUnit.Framework;
using OpenRA.MobileUi;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class MobileUiTests
	{
		[TestCase(800, 360, MobileLayoutProfile.CompactPhoneLandscape)]
		[TestCase(891, 411, MobileLayoutProfile.PhoneLandscape)]
		[TestCase(700, 411, MobileLayoutProfile.PhoneLandscape)]
		[TestCase(960, 432, MobileLayoutProfile.FoldableLandscape)]
		[TestCase(700, 540, MobileLayoutProfile.LargePhoneLandscape)]
		[TestCase(960, 540, MobileLayoutProfile.FoldableLandscape)]
		[TestCase(1280, 800, MobileLayoutProfile.TabletLandscape)]
		[TestCase(1536, 1024, MobileLayoutProfile.TabletLandscape)]
		public void ResolverClassifiesCommonSurfaces(int w, int h, MobileLayoutProfile expected)
		{
			Assert.That(MobileLayoutProfileResolver.Resolve(w, h), Is.EqualTo(expected));
		}

		[Test]
		public void ResolverUsesUsableSizeNotBrand()
		{
			Assert.That(MobileLayoutProfileResolver.Resolve(1400, 380), Is.EqualTo(MobileLayoutProfile.FoldableLandscape));
			Assert.That(MobileLayoutProfileResolver.Resolve(891, 411), Is.EqualTo(MobileLayoutProfile.PhoneLandscape));
		}

		[Test]
		public void PhoneTokensUseTwoProductionColumnsAndSafeTouchTargets()
		{
			var tokens = MobileUiTokensFactory.BaseTokens(MobileLayoutProfile.PhoneLandscape);
			Assert.That(tokens.ProductionColumns, Is.EqualTo(2));
			Assert.That(tokens.MinimumTouchTargetDp, Is.GreaterThanOrEqualTo(48));
			Assert.That(tokens.PrimaryButtonHeightDp, Is.GreaterThanOrEqualTo(56));
		}

		[Test]
		public void TabletTokensKeepThreeColumns()
		{
			var tokens = MobileUiTokensFactory.BaseTokens(MobileLayoutProfile.TabletLandscape);
			Assert.That(tokens.ProductionColumns, Is.EqualTo(3));
			Assert.That(tokens.ProductionPanelWidthDp, Is.GreaterThan(200));
		}

		[Test]
		public void LargerUserSizeScalesDpTokensAndFonts()
		{
			var standard = MobileUiTokensFactory.ApplyUserSize(
				MobileUiTokensFactory.BaseTokens(MobileLayoutProfile.PhoneLandscape),
				MobileUiSizePreference.Standard100, 1f);
			var xl = MobileUiTokensFactory.ApplyUserSize(
				MobileUiTokensFactory.BaseTokens(MobileLayoutProfile.PhoneLandscape),
				MobileUiSizePreference.ExtraLarge145, 1f);

			Assert.That(xl.PrimaryButtonHeightDp, Is.GreaterThan(standard.PrimaryButtonHeightDp));
			Assert.That(xl.ButtonFontSp, Is.GreaterThan(standard.ButtonFontSp));
		}

		[Test]
		public void SystemFontScaleGrowsSpNotDp()
		{
			var normal = MobileUiTokensFactory.ApplyUserSize(
				MobileUiTokensFactory.BaseTokens(MobileLayoutProfile.PhoneLandscape),
				MobileUiSizePreference.Standard100, 1f);
			var bigFont = MobileUiTokensFactory.ApplyUserSize(
				MobileUiTokensFactory.BaseTokens(MobileLayoutProfile.PhoneLandscape),
				MobileUiSizePreference.Standard100, 2f);

			Assert.That(bigFont.ButtonFontSp, Is.EqualTo(normal.ButtonFontSp * 2));
			Assert.That(bigFont.MinimumTouchTargetDp, Is.EqualTo(normal.MinimumTouchTargetDp));
		}

		[Test]
		public void DpConversionRoundTrips()
		{
			var ui = MobileUiMath.DpToUi(56f, 3.5f, 1.6f);
			Assert.That(ui, Is.EqualTo(122.5f).Within(0.001f));
			var dp = ui * 1.6f / 3.5f;
			Assert.That(dp, Is.EqualTo(56f).Within(0.001f));
		}

		[Test]
		public void SpConversionIncludesFontScale()
		{
			var normal = MobileUiMath.SpToUi(16f, 3.5f, 1f, 1.6f);
			var large = MobileUiMath.SpToUi(16f, 3.5f, 1.3f, 1.6f);
			Assert.That(large, Is.EqualTo(normal * 1.3f).Within(0.001f));
		}

		[Test]
		public void ConversionGuardsNonPositiveInputs()
		{
			Assert.That(MobileUiMath.DpToUi(48, 0, 1), Is.EqualTo(0));
			Assert.That(MobileUiMath.DpToUi(48, 3.5f, 0), Is.EqualTo(0));
			Assert.That(MobileUiMath.SpToUi(16, 0, 1, 1), Is.EqualTo(0));
		}

		[Test]
		public void ServiceUpdateRecomputesProfileAndFiresOnce()
		{
			var service = MobileUiService.Instance;
			var fired = 0;
			service.LayoutChanged += () => fired++;

			service.Update(2730, 1440, 2730, 1440, 1706, 900, 3.5f, 1f, 60f, 780f, 411f,
				MobileUiSizePreference.Standard100);

			Assert.That(service.Profile, Is.EqualTo(MobileLayoutProfile.PhoneLandscape));
			Assert.That(service.UiScale, Is.EqualTo(2730f / 1706f).Within(0.001f));
			Assert.That(service.Tokens, Is.Not.Null);
			Assert.That(fired, Is.EqualTo(1));
			Assert.That(service.RoundDpToUi(48), Is.GreaterThan(0));
		}

		[Test]
		public void ServiceCannotClassifyDesktopSurfaceAsMobile()
		{
			var service = MobileUiService.Instance;
			service.Update(1920, 1080, 1920, 1080, 1920, 1080, 1f, 1f, 60f, 300f, 200f,
				MobileUiSizePreference.Standard100);
			Assert.That(service.Profile, Is.EqualTo(MobileLayoutProfile.Desktop));
		}
	}

	sealed class ExpandableTestWidget : OpenRA.Widgets.Widget
	{
	}

	[TestFixture]
	public sealed class MobileUiTouchTests
	{
		[Test]
		public void EventBoundsExpandOnlyWhenRequested()
		{
			var w = new ExpandableTestWidget();
			w.Bounds = new OpenRA.Widgets.WidgetBounds(100, 100, 20, 20);
			Assert.That(w.EventBounds, Is.EqualTo(new OpenRA.Primitives.Rectangle(100, 100, 20, 20)));
			w.EventExpandLeft = w.EventExpandRight = w.EventExpandTop = w.EventExpandBottom = 10;
			Assert.That(w.EventBounds, Is.EqualTo(new OpenRA.Primitives.Rectangle(90, 90, 40, 40)));
		}

		[Test]
		public void ExpandToMinimumTouchUsesMobileMetrics()
		{
			var service = MobileUiService.Instance;
			service.Update(2730, 1440, 2730, 1440, 1706, 900, 3.5f, 1f, 60f, 891f, 411f,
				MobileUiSizePreference.Standard100);

			var parent = new ExpandableTestWidget();
			parent.Bounds = new OpenRA.Widgets.WidgetBounds(0, 0, 900, 600);
			var w = new ExpandableTestWidget();
			w.Bounds = new OpenRA.Widgets.WidgetBounds(100, 100, 20, 20);
			parent.AddChild(w);
			w.ExpandEventBoundsToMinimumTouchTarget();

			Assert.That(w.EventBounds.Width, Is.GreaterThanOrEqualTo(120));
			Assert.That(w.EventBounds.Height, Is.GreaterThanOrEqualTo(120));
			Assert.That(w.Bounds.Width, Is.EqualTo(20), "visual bounds must not change");
		}

		[Test]
		public void ExpansionClampsInsideParent()
		{
			var service = MobileUiService.Instance;
			service.Update(2730, 1440, 2730, 1440, 1706, 900, 3.5f, 1f, 60f, 891f, 411f,
				MobileUiSizePreference.Standard100);

			var parent = new ExpandableTestWidget();
			parent.Bounds = new OpenRA.Widgets.WidgetBounds(100, 100, 600, 400);
			var w = new ExpandableTestWidget();
			w.Bounds = new OpenRA.Widgets.WidgetBounds(160, 150, 20, 20);
			parent.AddChild(w);
			w.ExpandEventBoundsToMinimumTouchTarget();

			Assert.That(w.EventBounds.Left, Is.GreaterThanOrEqualTo(parent.RenderBounds.Left));
			Assert.That(w.EventBounds.Top, Is.GreaterThanOrEqualTo(parent.RenderBounds.Top));
			Assert.That(w.EventBounds.Right, Is.LessThanOrEqualTo(parent.RenderBounds.Right));
			Assert.That(w.EventBounds.Bottom, Is.LessThanOrEqualTo(parent.RenderBounds.Bottom));
		}

		[Test]
		public void DesktopProfileLeavesEventBoundsUntouched()
		{
			var service = MobileUiService.Instance;
			service.Update(1920, 1080, 1920, 1080, 1920, 1080, 1f, 1f, 60f, 300f, 200f,
				MobileUiSizePreference.Standard100);

			var w = new ExpandableTestWidget();
			w.Bounds = new OpenRA.Widgets.WidgetBounds(0, 0, 20, 20);
			w.ExpandEventBoundsToMinimumTouchTarget();
			Assert.That(w.EventExpandLeft, Is.EqualTo(0));
			Assert.That(w.EventExpandRight, Is.EqualTo(0));
		}
	}

	[TestFixture]
	public sealed class MobileUiAuditTests
	{
		[Test]
		public void AuditFlagsTinyAndOverlappingTargets()
		{
			var service = MobileUiService.Instance;
			service.Update(2730, 1440, 2730, 1440, 1706, 900, 3.5f, 1f, 60f, 891f, 411f,
				MobileUiSizePreference.Standard100);

			var root = new ExpandableTestWidget();
			root.Bounds = new OpenRA.Widgets.WidgetBounds(0, 0, 500, 300);

			var tiny = new ExpandableTestWidget { Id = "tiny" };
			tiny.Bounds = new OpenRA.Widgets.WidgetBounds(10, 10, 20, 20);

			var a = new ExpandableTestWidget { Id = "a" };
			a.Bounds = new OpenRA.Widgets.WidgetBounds(100, 100, 30, 30);
			var b = new ExpandableTestWidget { Id = "b" };
			b.Bounds = new OpenRA.Widgets.WidgetBounds(120, 110, 30, 30);

			root.AddChild(tiny);
			root.AddChild(a);
			root.AddChild(b);

			var entries = MobileUiTouchAudit.Audit(root, w => w is ExpandableTestWidget && w.Id != "root");
			Assert.That(entries.Any(e => e.Id == "tiny" && e.Severity == "P0"), Is.True);
			Assert.That(entries.Any(e => e.Type == "Overlap"), Is.True);

			var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mui-audit-" + System.Guid.NewGuid().ToString("N"));
			MobileUiTouchAudit.WriteReports(entries, dir);
			Assert.That(System.IO.File.Exists(System.IO.Path.Combine(dir, "mobile-touch-audit.txt")), Is.True);
			Assert.That(System.IO.File.Exists(System.IO.Path.Combine(dir, "mobile-touch-audit.json")), Is.True);
			var text = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "mobile-touch-audit.txt"));
			StringAssert.Contains("P0", text);
			System.IO.Directory.Delete(dir, true);
		}
	}


	[TestFixture]
	public sealed class MobileSafeAreaTests
	{
		[Test]
		public void LogicalSafeBoundsExcludeInsets()
		{
			var service = MobileUiService.Instance;
			// Right-hand gesture inset 168px (=48dp), top cutout etc.
			service.Update(2730, 1440, 2730, 1440, 1706, 900, 3.5f, 1f, 60f, 732f, 411f,
				MobileUiSizePreference.Standard100, insetRightDp: 48f, insetTopDp: 20f);

			var safe = service.LogicalSafeBounds;
			// 48dp -> 105 logical; 20dp -> 44 logical
			Assert.That(safe.Left, Is.EqualTo(0));
			Assert.That(safe.Top, Is.GreaterThanOrEqualTo(43));
			Assert.That(safe.Top, Is.LessThanOrEqualTo(45));
			Assert.That(safe.Right, Is.EqualTo(safe.Left + safe.Width));
			Assert.That(safe.Height, Is.GreaterThan(0));
			Assert.That(safe.Width, Is.LessThan(service.LogicalWidth));
		}
	}


	[TestFixture]
	public sealed class MobilePhoneInputTests
	{
		[Test]
		public void PhoneHitRectExpandsToMinimumAndStaysInsideOwner()
		{
			var service = MobileUiService.Instance;
			service.Update(2730, 1440, 2730, 1440, 1706, 900, 3.5f, 1f, 60f, 891f, 411f,
				MobileUiSizePreference.Standard100);
			service.IsEnabledMobile = true;

			var owner = new ExpandableTestWidget();
			owner.Bounds = new OpenRA.Widgets.WidgetBounds(100, 100, 400, 400);

			var visual = new OpenRA.Primitives.Rectangle(150, 150, 20, 20);
			var expanded = PhoneInput.ExpandHitRect(visual, owner);

			Assert.That(expanded.Width, Is.GreaterThanOrEqualTo(visual.Width * 2));
			Assert.That(expanded.Width, Is.GreaterThanOrEqualTo(105));
			Assert.That(expanded.Left, Is.GreaterThanOrEqualTo(owner.RenderBounds.Left));
			Assert.That(expanded.Right, Is.LessThanOrEqualTo(owner.RenderBounds.Right));
		}

		[Test]
		public void DesktopHitRectIsUntouched()
		{
			var service = MobileUiService.Instance;
			service.Update(1920, 1080, 1920, 1080, 1920, 1080, 1f, 1f, 60f, 300f, 200f,
				MobileUiSizePreference.Standard100);
			service.IsEnabledMobile = false;

			var owner = new ExpandableTestWidget();
			owner.Bounds = new OpenRA.Widgets.WidgetBounds(0, 0, 500, 500);
			var visual = new OpenRA.Primitives.Rectangle(10, 10, 20, 20);
			var expanded = PhoneInput.ExpandHitRect(visual, owner);
			Assert.That(expanded, Is.EqualTo(visual));
		}
	}


	[TestFixture]
	public sealed class MobileUiMatrixTests
	{
		static readonly (int W, int H, MobileLayoutProfile Profile)[] Surfaces =
		{
			(800, 360, MobileLayoutProfile.CompactPhoneLandscape),
			(891, 411, MobileLayoutProfile.PhoneLandscape),
			(960, 432, MobileLayoutProfile.FoldableLandscape),
			(960, 540, MobileLayoutProfile.FoldableLandscape),
			(700, 540, MobileLayoutProfile.LargePhoneLandscape),
			(960, 560, MobileLayoutProfile.FoldableLandscape),
			(1024, 600, MobileLayoutProfile.TabletLandscape),
			(1280, 800, MobileLayoutProfile.TabletLandscape),
			(1536, 1024, MobileLayoutProfile.TabletLandscape),
			(1180, 885, MobileLayoutProfile.TabletLandscape),
		};

		[Test]
		public void ProfilesResolveAcrossAspectRatios()
		{
			foreach (var (w, h, expected) in Surfaces)
				Assert.That(MobileLayoutProfileResolver.Resolve(w, h), Is.EqualTo(expected), $"{w}x{h}");
		}

		[Test]
		public void PhoneAndFoldableTokensMeetTouchAndFontFloors()
		{
			var profiles = new[]
			{
				MobileLayoutProfile.CompactPhoneLandscape,
				MobileLayoutProfile.PhoneLandscape,
				MobileLayoutProfile.LargePhoneLandscape,
				MobileLayoutProfile.FoldableLandscape,
			};

			foreach (var profile in profiles)
			{
				var tokens = MobileUiTokensFactory.BaseTokens(profile);
				Assert.That(tokens.MinimumTouchTargetDp, Is.GreaterThanOrEqualTo(48), profile.ToString());
				Assert.That(tokens.PrimaryButtonHeightDp, Is.GreaterThanOrEqualTo(56), profile.ToString());
				Assert.That(tokens.CaptionFontSp, Is.GreaterThanOrEqualTo(12), profile.ToString());
			}
		}

		[Test]
		public void TwoColumnCardsFitPhonePanelAndThreeFitTablet()
		{
			var phone = MobileUiTokensFactory.BaseTokens(MobileLayoutProfile.PhoneLandscape);
			var fit = phone.ProductionColumns * phone.ProductionCardWidthDp
				+ (phone.ProductionColumns - 1) * phone.ItemSpacingDp;
			Assert.That(fit, Is.LessThanOrEqualTo(phone.ProductionPanelWidthDp));

			var tablet = MobileUiTokensFactory.BaseTokens(MobileLayoutProfile.TabletLandscape);
			var fitTablet = tablet.ProductionColumns * tablet.ProductionCardWidthDp
				+ (tablet.ProductionColumns - 1) * tablet.ItemSpacingDp;
			Assert.That(fitTablet, Is.LessThanOrEqualTo(tablet.ProductionPanelWidthDp));
		}

		[Test]
		public void UserSizeAndFontScaleCombinationsStayPositiveAndMonotonic()
		{
			var baseTokens = MobileUiTokensFactory.BaseTokens(MobileLayoutProfile.PhoneLandscape);
			var prefs = new[]
			{
				MobileUiSizePreference.Standard100,
				MobileUiSizePreference.Comfortable115,
				MobileUiSizePreference.Large130,
				MobileUiSizePreference.ExtraLarge145,
			};

			var previousHeight = 0;
			foreach (var pref in prefs)
			{
				var heightAtPref = 0;
				foreach (var fontScale in new[] { 1f, 1.3f, 2f })
				{
					var tokens = MobileUiTokensFactory.ApplyUserSize(baseTokens, pref, fontScale);
					Assert.That(tokens.MinimumTouchTargetDp, Is.GreaterThanOrEqualTo(48));
					Assert.That(tokens.ButtonFontSp, Is.GreaterThanOrEqualTo(16));
					heightAtPref = tokens.PrimaryButtonHeightDp;
				}

				Assert.That(heightAtPref, Is.GreaterThan(previousHeight), pref.ToString());
				previousHeight = heightAtPref;
			}
		}

		[Test]
		public void TabletsNeverResolveToCompactPhone()
		{
			foreach (var (w, h, expected) in Surfaces)
				if (expected == MobileLayoutProfile.TabletLandscape)
					Assert.That(MobileLayoutProfileResolver.Resolve(w, h), Is.EqualTo(MobileLayoutProfile.TabletLandscape));
		}
	}

}
