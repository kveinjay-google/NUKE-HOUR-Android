using System;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class MenuIconLabelLayoutTest
	{
		[Test]
		public void DesktopShellAndControlsShareResponsiveLayout()
		{
			var directory = new System.IO.DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !System.IO.Directory.Exists(System.IO.Path.Combine(directory.FullName, "OpenRA.Mods.RA2")))
				directory = directory.Parent;
			Assert.That(directory, Is.Not.Null);
			var source = System.IO.File.ReadAllText(System.IO.Path.Combine(directory.FullName,
				"engine/OpenRA.Mods.Common/Widgets/Logic/MainMenuLogic.cs"));
			StringAssert.DoesNotContain("if (!Platform.UsesMobileLayout || !iosMainMenuLayoutEnabled)", source);
			StringAssert.Contains("IosMenuLayoutPolicy.Create(Platform.UsesMobileLayout, snapshot)", source);
		}
		[TestCase(IosMainMenuShellProfile.Standard, 0, 1672, 941, 122, 269, 690, 300, 427, 410)]
		[TestCase(IosMainMenuShellProfile.Standard, 1, 1672, 941, 122, 444, 690, 454, 578, 575)]
		[TestCase(IosMainMenuShellProfile.Standard, 2, 1672, 941, 122, 600, 690, 605, 734, 735)]
		[TestCase(IosMainMenuShellProfile.Tablet, 0, 1448, 1086, 105, 367, 588, 392, 503, 490)]
		[TestCase(IosMainMenuShellProfile.Tablet, 1, 1448, 1086, 105, 516, 588, 530, 638, 633)]
		[TestCase(IosMainMenuShellProfile.Tablet, 2, 1448, 1086, 105, 660, 588, 663, 775, 780)]
		[TestCase(IosMainMenuShellProfile.Ultrawide, 0, 1844, 853, 109, 230, 597, 256, 367, 350)]
		[TestCase(IosMainMenuShellProfile.Ultrawide, 1, 1844, 853, 106, 380, 597, 389, 502, 498)]
		[TestCase(IosMainMenuShellProfile.Ultrawide, 2, 1844, 853, 105, 516, 597, 522, 639, 639)]
		public void RouteArtworkFollowsItsOwnGeneratedPanelPlane(IosMainMenuShellProfile profile, int row,
			int width, int height, int left, int topLeft, int right, int topRight, int bottomRight, int bottomLeft)
		{
			var method = typeof(MenuIconLabelWidget).GetMethod("ProjectRoutePoint", BindingFlags.Public | BindingFlags.Static);
			Assert.That(method, Is.Not.Null, "Route artwork must follow the plate, not the horizontal hit rectangle.");
			var tablet = profile == IosMainMenuShellProfile.Tablet;
			var bx = width * (tablet ? .054 : .059);
			var by = height * (tablet ? new[] { .336, .474, .607 } : new[] { .286, .446, .627 })[row];
			var size = new Size((int)Math.Round(width * (tablet ? .36 : .355)), (int)Math.Round(height * (tablet ? .122 : .145)));
			float2 Point(double u, double v) => (float2)method!.Invoke(null, new object[] { size, profile, row, u, v });
			var points = new[] { Point(0, 0), Point(1, 0), Point(1, 1), Point(0, 1) };
			var expected = new[] { new float2(left, topLeft), new float2(right, topRight), new float2(right, bottomRight), new float2(left, bottomLeft) };
			for (var i = 0; i < 4; i++)
			{
				Assert.That(points[i].X + bx, Is.EqualTo(expected[i].X).Within(1.2));
				Assert.That(points[i].Y + by, Is.EqualTo(expected[i].Y).Within(1.2));
			}
			// Icon and lettering share one centerline and the same convergence.
			foreach (var u in new[] { .15, .4, .7 })
				Assert.That(Point(u, .5).Y, Is.EqualTo((Point(u, 0).Y + Point(u, 1).Y) / 2).Within(.01));
			var slope = (topRight + bottomRight) - (topLeft + bottomLeft);
			Assert.That(Math.Sign(Point(.7, .5).Y - Point(.15, .5).Y), Is.EqualTo(Math.Sign(slope)));
			Assert.That(Point(.7, 1).Y - Point(.7, 0).Y, Is.LessThan(Point(.15, 1).Y - Point(.15, 0).Y));
		}

		[Test]
		public void DifferentIconSilhouettesShareTheSameLabelColumn()
		{
			var a = MenuIconLabelWidget.CalculateLayout(new Size(511, 130), new Size(400, 260), new Size(440, 120), true);
			var b = MenuIconLabelWidget.CalculateLayout(new Size(511, 130), new Size(330, 400), new Size(440, 120), true);
			Assert.That(a[1].Left, Is.EqualTo(b[1].Left));
		}

		[TestCase(511, 130, true)]
		[TestCase(425, 100, true)]
		[TestCase(331, 74, false)]
		[TestCase(230, 48, false)]
		public void VisibleEdgesKeepCompactGapAndPreserveAspect(int width, int height, bool route)
		{
			var type = typeof(AspectImageWidget).Assembly.GetType("OpenRA.Mods.RA2.Widgets.MenuIconLabelWidget");
			Assert.That(type, Is.Not.Null, "Home icon/label pairs need shared visible-edge layout.");
			var method = type!.GetMethod("CalculateLayout", BindingFlags.Public | BindingFlags.Static);
			foreach (var icon in new[] { new Size(400, 260), new Size(380, 400), new Size(400, 400) })
			foreach (var label in new[] { new Size(440, 120), new Size(200, 120), new Size(460, 60) })
			{
				var rects = (Rectangle[])method!.Invoke(null, new object[] { new Size(width, height), icon, label, route });
				var gap = rects[1].Left - rects[0].Right;
				Assert.That(gap, Is.InRange(2, (int)Math.Ceiling(height * .14)));
				Assert.That(rects[0].Left, Is.GreaterThanOrEqualTo(0));
				Assert.That(rects[1].Right, Is.LessThanOrEqualTo(width));
				for (var i = 0; i < 2; i++)
				{
					var source = i == 0 ? icon : label;
					Assert.That(rects[i].Height, Is.GreaterThan(0));
					Assert.That(Math.Abs(rects[i].Width - rects[i].Height * (double)source.Width / source.Height), Is.LessThan(6));
					Assert.That(Math.Abs(rects[i].Top + rects[i].Height / 2.0 - height / 2.0), Is.LessThanOrEqualTo(1));
				}
			}
		}
	}
}
