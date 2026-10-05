#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosMapChooserLayoutTest
	{
		[TestCase("2 Players", "Heartland", "2 Players  Heartland")]
		[TestCase("4 名玩家", "冰天雪地", "4 名玩家  冰天雪地")]
		[TestCase("8 Players", "", "8 Players")]
		public void MapCardCaptionPlacesTheMapNameAfterThePlayerCount(
			string playerCount, string mapTitle, string expected)
		{
			var method = typeof(MapChooserLogic).GetMethod("MapCardCaption",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null);
			Assert.That(method!.Invoke(null, new object[] { playerCount, mapTitle }), Is.EqualTo(expected));
		}

		[Test]
		public void UserMapTabTakesListLeftEdgeWhenSystemMapsAreHidden()
		{
			var snapshot = Snapshot(1440, 900, 1440, 900, 0, 0, 0, 0);
			var layout = IosMapChooserLayout.Create(snapshot, true);
			var method = typeof(IosMapChooserLayout).GetMethod("UserTabFor", BindingFlags.Public | BindingFlags.Instance);
			Assert.That(method, Is.Not.Null);
			var alone = (Rectangle)method!.Invoke(layout, new object[] { false })!;
			var withSystem = (Rectangle)method.Invoke(layout, new object[] { true })!;
			Assert.That(alone.Left, Is.EqualTo(layout.MapPane.Left));
			Assert.That(alone, Is.EqualTo(layout.SystemTab));
			Assert.That(withSystem, Is.EqualTo(layout.UserTab));
		}

		static readonly object[] Devices =
		{
			new object[] { 1558, 720, 844, 390, 47d, 0d, 47d, 21d },
			new object[] { 1560, 720, 932, 430, 59d, 0d, 59d, 21d },
			new object[] { 1180, 820, 1180, 820, 0d, 0d, 0d, 20d },
			new object[] { 1366, 1024, 1366, 1024, 0d, 0d, 0d, 20d }
		};

		[TestCase(812, 375, 812, 375, 44, 0, 44, 21)]
		[TestCase(1558, 720, 844, 390, 47, 0, 47, 21)]
		[TestCase(1560, 720, 932, 430, 59, 0, 59, 21)]
		[TestCase(1024, 768, 1024, 768, 0, 0, 0, 20)]
		[TestCase(1280, 720, 1280, 720, 0, 0, 0, 0)]
		[TestCase(1920, 1080, 1920, 1080, 0, 0, 0, 0)]
		public void SovietMapChooserKeepsListPreviewFiltersAndActionsSeparated(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var layout = IosMapChooserLayout.Create(snapshot, true);
			Assert.That(snapshot.SafeBounds.Contains(layout.Panel), Is.True);
			var regions = new[] { layout.Title, layout.TabBar, layout.MapPane, layout.SelectedMap, layout.FilterRow, layout.Footer };
			for (var i = 0; i < regions.Length; i++)
			{
				Assert.That(layout.LocalPanel.Contains(regions[i]), Is.True);
				Assert.That(regions[i].Height, Is.GreaterThan(0));
				for (var j = 0; j < i; j++)
					Assert.That(regions[i].IntersectsWith(regions[j]), Is.False);
			}

			var previewPanel = new Rectangle(0, 0, layout.SelectedMap.Width, layout.SelectedMap.Height);
			foreach (var bounds in new[] { layout.SelectedPreview, layout.SelectedTitle, layout.SelectedDetails, layout.SelectedAuthor })
				Assert.That(previewPanel.Contains(bounds), Is.True);
			Assert.That(layout.SelectedPreview.Height / snapshot.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			foreach (var control in new[] { layout.SystemTab, layout.UserTab, layout.FilterInput, layout.GameModeFilter, layout.OrderBy })
				AssertPhysicalTarget(control, snapshot.LogicalPerPoint);
			for (var i = 0; i < 5; i++)
				AssertPhysicalTarget(layout.FooterButtonBounds(i), snapshot.LogicalPerPoint);

			var row = new ContainerWidget();
			foreach (var id in new[] { "PREVIEW", "TITLE", "DETAILS", "AUTHOR", "SIZE" })
				row.AddChild(BareLabel(id));
			var apply = typeof(MapChooserLogic).GetMethod("LayoutMapCard", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(apply, Is.Not.Null);
			apply!.Invoke(null, new object[] { row, layout });
			Assert.That(row.Bounds.Height / snapshot.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			var rowBounds = new Rectangle(0, 0, row.Bounds.Width, row.Bounds.Height);
			foreach (var id in new[] { "PREVIEW", "DETAILS" })
				Assert.That(rowBounds.Contains(row.Get(id).Bounds.ToRectangle()), Is.True);
			Assert.That(row.Get("PREVIEW").Bounds.Bottom, Is.LessThanOrEqualTo(row.Get("DETAILS").Bounds.Top));
			Assert.That(row.Get<LabelWidget>("DETAILS").Align, Is.EqualTo(TextAlign.Center));
		}

		static LabelWidget BareLabel(string id)
		{
#pragma warning disable SYSLIB0050
			var label = (LabelWidget)FormatterServices.GetUninitializedObject(typeof(LabelWidget));
#pragma warning restore SYSLIB0050
			typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(label, new List<Widget>());
			label.Id = id;
			return label;
		}

		[TestCase(1440, 900)]
		[TestCase(1672, 941)]
		[TestCase(1024, 768)]
		[TestCase(1920, 1080)]
		public void RoomyMapChooserFitsTheArtworkInteriorIncludingDiagonalFooterCorners(int width, int height)
		{
			var snapshot = Snapshot(width, height, width, height, 0, 0, 0, 0);
			var layout = IosMapChooserLayout.Create(snapshot, true);
			Assert.That(layout.Panel.Left, Is.GreaterThanOrEqualTo(width * .08));
			Assert.That(layout.Panel.Top, Is.GreaterThanOrEqualTo(height * .10));
			Assert.That(layout.Panel.Right, Is.LessThanOrEqualTo(width * .92));
			Assert.That(layout.Panel.Bottom, Is.LessThanOrEqualTo(height * .89));
			for (var i = 0; i < 5; i++)
			{
				var local = layout.FooterButtonBounds(i);
				var onArtwork = new Rectangle(local.X + layout.Panel.X, local.Y + layout.Panel.Y, local.Width, local.Height);
				Assert.That(layout.Panel.Contains(onArtwork), Is.True);
				Assert.That(onArtwork.Bottom, Is.LessThanOrEqualTo(height * .89));
				AssertPhysicalTarget(local, snapshot.LogicalPerPoint);
			}

			if (width == 1672 && height == 941)
				Assert.That(Rectangle.FromLTRB(134, 94, 1538, 837).Contains(layout.Panel), Is.True,
					"The generated bitmap's measured quiet rectangle must contain the complete UI.");
		}

		[Test]
		public void PhoneMapChooserKeepsItsCompactHeaderSpace()
		{
			var snapshot = Snapshot(812, 375, 812, 375, 44, 0, 44, 21);
			var shared = MultiplayerScreenLayout.ContentBounds(snapshot);
			var layout = IosMapChooserLayout.Create(snapshot, true);
			Assert.That(layout.Panel.Top, Is.EqualTo(shared.Top));
			Assert.That(layout.Panel.Bottom, Is.EqualTo(shared.Bottom));
		}

		[TestCase(1440, 900, 1440, 900, 3)]
		[TestCase(1672, 941, 1672, 941, 3)]
		[TestCase(1180, 820, 1180, 820, 3)]
		[TestCase(812, 375, 812, 375, 3)]
		public void CommandCenterMapCardsFormScrollableThumbnailGrid(
			int width, int height, int nativeWidth, int nativeHeight, int columns)
		{
			var snapshot = Snapshot(width, height, nativeWidth, nativeHeight, 0, 0, 0, 0);
			var layout = IosMapChooserLayout.Create(snapshot, true);
			Assert.That(layout.GridColumns, Is.EqualTo(columns));
			Assert.That(columns * layout.CardWidth + (columns + 1) * layout.Gap,
				Is.LessThanOrEqualTo(layout.MapPane.Width));
			Assert.That(layout.CardHeight / snapshot.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
			var card = new Rectangle(0, 0, layout.CardWidth, layout.CardHeight);
			Assert.That(card.Contains(layout.CardPreview), Is.True);
			Assert.That(card.Contains(layout.CardPlayerCount), Is.True);
			Assert.That(layout.CardPreview.IntersectsWith(layout.CardPlayerCount), Is.False);
			Assert.That(layout.CardPreview.Width, Is.GreaterThanOrEqualTo(layout.CardWidth * .9));
		}

		[TestCaseSource(nameof(Devices))]
		public void MapChooserUsesMostOfTheSafeAreaWithoutOverlappingRows(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var layout = IosMapChooserLayout.Create(snapshot);

			Assert.That(snapshot.SafeBounds.Contains(layout.Panel), Is.True);
			Assert.That(layout.Panel.Width / (double)snapshot.SafeBounds.Width, Is.GreaterThanOrEqualTo(0.94));
			Assert.That(layout.Panel.Height / (double)snapshot.SafeBounds.Height, Is.GreaterThanOrEqualTo(0.90));
			Assert.That(layout.LocalPanel.Contains(layout.Title), Is.True);
			Assert.That(layout.LocalPanel.Contains(layout.MapPane), Is.True);
			Assert.That(layout.Title.Bottom, Is.LessThanOrEqualTo(layout.TabBar.Top));
			Assert.That(layout.TabBar.Bottom, Is.LessThanOrEqualTo(layout.MapPane.Top));
			Assert.That(layout.MapPane.Bottom, Is.LessThanOrEqualTo(layout.FilterRow.Top));
			Assert.That(layout.FilterRow.Bottom, Is.LessThanOrEqualTo(layout.Footer.Top));
			Assert.That(layout.MapPane.Height / snapshot.LogicalPerPoint, Is.GreaterThanOrEqualTo(132));
		}

		[TestCaseSource(nameof(Devices))]
		public void MapChooserInteractiveControlsMeetFortyEightPointTouchMinimum(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			var snapshot = Snapshot(effectiveWidth, effectiveHeight, nativeWidth, nativeHeight,
				safeLeft, safeTop, safeRight, safeBottom);
			var layout = IosMapChooserLayout.Create(snapshot);

			AssertPhysicalTarget(layout.SystemTab, snapshot.LogicalPerPoint);
			AssertPhysicalTarget(layout.UserTab, snapshot.LogicalPerPoint);
			AssertPhysicalTarget(layout.FilterInput, snapshot.LogicalPerPoint);
			AssertPhysicalTarget(layout.GameModeFilter, snapshot.LogicalPerPoint);
			AssertPhysicalTarget(layout.OrderBy, snapshot.LogicalPerPoint);
			for (var i = 0; i < 5; i++)
				AssertPhysicalTarget(layout.FooterButtonBounds(i), snapshot.LogicalPerPoint);

			Assert.That(layout.ScrollbarWidth / snapshot.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
		}

		[Test]
		public void MapChooserEnablesTouchDraggingWhileRetainingDesktopYamlGeometry()
		{
			var root = RepositoryRoot();
			var source = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "MapChooserLogic.cs"));
			var yaml = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "chrome", "map-chooser.yaml"));

			Assert.That(source, Does.Contain("tabScrollpanel.EnableContentDragging = true"));
			Assert.That(source, Does.Contain("tabScrollpanel.ScrollbarWidth = Math.Max"));
			Assert.That(source, Does.Contain("IosMapChooserLayout.Create"));
			Assert.That(yaml, Does.Contain("Width: 900"));
			Assert.That(yaml, Does.Contain("Height: 600"));
		}

		static IosScreenSnapshot Snapshot(
			int effectiveWidth, int effectiveHeight, int nativeWidth, int nativeHeight,
			double safeLeft, double safeTop, double safeRight, double safeBottom)
		{
			return new IosScreenSnapshot(
				new Size(effectiveWidth, effectiveHeight),
				new Size(nativeWidth, nativeHeight),
				new IosSafeAreaInsets(safeLeft, safeTop, safeRight, safeBottom));
		}

		static void AssertPhysicalTarget(Rectangle bounds, double logicalPerPoint)
		{
			Assert.That(bounds.Width / logicalPerPoint, Is.GreaterThanOrEqualTo(48));
			Assert.That(bounds.Height / logicalPerPoint, Is.GreaterThanOrEqualTo(48));
		}

		static string RepositoryRoot()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (root != null && !Directory.Exists(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets")))
				root = Directory.GetParent(root)?.FullName;

			Assert.That(root, Is.Not.Null, "Could not locate repository root.");
			return root!;
		}
	}
}
