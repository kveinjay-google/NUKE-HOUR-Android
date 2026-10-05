#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class AdaptiveRouteButtonGeometryTest
	{
		[TestCase(2868, 1320, 0)]
		[TestCase(2868, 1320, 1)]
		[TestCase(2868, 1320, 2)]
		[TestCase(1558, 720, 0)]
		public void PhoneIlluminationUsesArtworkCoordinatesNotSafeAreaHitBounds(int width, int height, int row)
		{
			var method = typeof(AdaptiveRouteButtonWidget).GetMethod("CalculateArtworkBounds");
			Assert.That(method, Is.Not.Null, "Safe-area hit bounds must not resize the illumination artwork.");
			var ys = new[] { .286, .446, .627 };
			var expected = new Rectangle((int)Math.Round(width * .059), (int)Math.Round(height * ys[row]),
				(int)Math.Round(width * .414) - (int)Math.Round(width * .059), (int)Math.Round(height * .145));
			foreach (var inset in new[] { 0, 120, 258 })
			{
				var hit = new Rectangle(expected.X + inset, expected.Y, expected.Width - inset, expected.Height);
				Assert.That(method!.Invoke(null, new object[] { hit, new Size(width, height), true, row }), Is.EqualTo(expected));
				Assert.That(method.Invoke(null, new object[] { hit, new Size(width, height), false, row }), Is.EqualTo(hit),
					"Tablet and desktop geometry must remain unchanged.");
			}
		}

		[TestCase(1920, 1080, .059, .355, .145, .286)]
		[TestCase(1536, 1152, .054, .360, .122, .336)]
		[TestCase(2048, 947, .059, .355, .145, .286)]
		public void IlluminationBoundsReverseTheAuthoredAtlasCrop(
			int width, int height, double left, double routeWidth, double routeHeight, double top)
		{
			var sourceRoute = Rectangle.FromLTRB(
				(int)Math.Round(width * left), (int)Math.Round(height * top),
				(int)Math.Round(width * (left + routeWidth)), (int)Math.Round(height * (top + routeHeight)));
			var method = typeof(AdaptiveRouteButtonWidget).GetMethod("CalculateIlluminationBounds", BindingFlags.Public | BindingFlags.Static);
			Assert.That(method, Is.Not.Null, "The full illumination sprite must be mapped back to its authored crop.");
			var expanded = (Rectangle)method!.Invoke(null, new object[] { sourceRoute });
			Assert.That(expanded.Left, Is.EqualTo(sourceRoute.Left));
			Assert.That(expanded.Right, Is.EqualTo(sourceRoute.Right));
			Assert.That(expanded.Bottom, Is.EqualTo(sourceRoute.Bottom + (int)Math.Round(sourceRoute.Height * .18)));
			Assert.That(expanded.Top, Is.EqualTo(sourceRoute.Top - (int)Math.Round(sourceRoute.Height * .38)));
			// The authored atlas is 1024x384. Its entire width must map to the plate,
			// even when the destination is smaller than the source sprite.
			foreach (var scale in new[] { .5, .75, 1.0, 1.5 })
			{
				var target = new Rectangle(17, 31, (int)Math.Round(sourceRoute.Width * scale), (int)Math.Round(sourceRoute.Height * scale));
				var result = (Rectangle)method.Invoke(null, new object[] { target });
				Assert.That(result.Width, Is.EqualTo(target.Width));
				Assert.That(result.Bottom, Is.EqualTo(target.Bottom + (int)Math.Round(target.Height * .18)));
				Assert.That(Math.Abs(result.Height - expanded.Height * scale), Is.LessThanOrEqualTo(2));
			}
		}

		[Test]
		public void RuntimeDrawsTheCompleteIlluminationSpriteInsteadOfTilingIt()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (root != null && !Directory.Exists(Path.Combine(root, "OpenRA.Mods.RA2")))
				root = Directory.GetParent(root)?.FullName;
			Assert.That(root, Is.Not.Null);
			var source = File.ReadAllText(Path.Combine(root!, "OpenRA.Mods.RA2", "Widgets", "AdaptiveRouteButtonWidget.cs"));
			Assert.That(source, Does.Contain("WidgetUtils.DrawSprite("));
			Assert.That(source, Does.Contain("CalculateArtworkBounds(rect, resolution, phone, ArtworkRow)"));
			Assert.That(source, Does.Contain("CalculateIlluminationBounds(artworkRect)"));
			Assert.That(source, Does.Contain("WidgetUtils.GetStatefulImageName("));
			Assert.That(source, Does.Not.Contain("ButtonWidget.DrawBackground("));
			Assert.That(source, Does.Not.Contain("WidgetUtils.DrawPanel("));
		}
	}
}
