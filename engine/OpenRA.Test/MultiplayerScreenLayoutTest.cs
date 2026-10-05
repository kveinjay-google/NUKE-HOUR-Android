using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class MultiplayerScreenLayoutTest
	{
		[TestCase("MULTIPLAYER_PANEL", true)]
		[TestCase("MULTIPLAYER_CREATESERVER_PANEL", true)]
		[TestCase("DIRECTCONNECT_PANEL", true)]
		[TestCase("LOBBY_SERVERS_BIN", false)]
		[TestCase("SETTINGS_PANEL", false)]
		public void DesktopAdaptationIsLimitedToTheThreeMultiplayerScreens(string id, bool expected)
		{
			Assert.That(MultiplayerScreenLayout.SupportsDesktop(id, true), Is.EqualTo(expected));
			Assert.That(MultiplayerScreenLayout.SupportsDesktop(id, false), Is.False);
		}

		[TestCase(1920, 1080, 1920, 1080, 0, 0)]
		[TestCase(3440, 1440, 3440, 1440, 0, 0)]
		[TestCase(1558, 720, 844, 390, 47, 21)]
		[TestCase(1180, 820, 1180, 820, 0, 20)]
		public void ContentClearsShellDecorationAndDeviceSafeArea(
			int width, int height, int nativeWidth, int nativeHeight, int side, int bottom)
		{
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(nativeWidth, nativeHeight),
				new IosSafeAreaInsets(side, 0, side, bottom));
			var content = MultiplayerScreenLayout.ContentBounds(snapshot);
			Assert.That(content.X, Is.GreaterThanOrEqualTo(Math.Max(snapshot.SafeBounds.X, Math.Ceiling(width * .04))));
			Assert.That(content.Y, Is.GreaterThanOrEqualTo(Math.Ceiling(height * .04)));
			Assert.That(content.Right, Is.LessThanOrEqualTo(Math.Min(snapshot.SafeBounds.Right, width * .96)));
			Assert.That(content.Bottom, Is.LessThanOrEqualTo(Math.Min(snapshot.SafeBounds.Bottom, height * .96)));
		}

		[TestCase(1920, 1080)]
		[TestCase(1180, 820)]
		public void RoomyScreensKeepTheFooterAboveTheSixPercentShellRail(int width, int height)
		{
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(width, height), default);
			var bounds = MultiplayerScreenLayout.ContentBounds(snapshot);
			Assert.That(bounds.Y, Is.EqualTo(Math.Ceiling(height * .06)));
			Assert.That(bounds.Bottom, Is.EqualTo(height - Math.Ceiling(height * .06) - snapshot.LogicalPoints(24)));
			Assert.That(bounds.Height, Is.GreaterThan(0));
		}

		[Test]
		public void RoomyScreenBottomClearanceUsesPhysicalPointsUnderScaledRendering()
		{
			var snapshot = new IosScreenSnapshot(new Size(2360, 1640), new Size(1180, 820), default);
			var bounds = MultiplayerScreenLayout.ContentBounds(snapshot);
			Assert.That(bounds.Y, Is.EqualTo(Math.Ceiling(1640 * .06)));
			Assert.That(bounds.Bottom, Is.EqualTo(1640 - Math.Ceiling(1640 * .06) - 48));
		}

		[Test]
		public void PhonesKeepFourPercentVerticalInsetForAvailableFormHeight()
		{
			var snapshot = new IosScreenSnapshot(new Size(1558, 720), new Size(844, 390), default);
			var bounds = MultiplayerScreenLayout.ContentBounds(snapshot);
			Assert.That(bounds.Y, Is.EqualTo(Math.Ceiling(720 * .04)));
			Assert.That(bounds.Bottom, Is.EqualTo(720 - Math.Ceiling(720 * .04)));
		}

		[TestCase(1920, 1080)]
		[TestCase(3440, 1440)]
		public void DirectConnectionFormStaysCenteredAndReadableOnWideScreens(int width, int height)
		{
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(width, height), default);
			var bounds = MultiplayerScreenLayout.ContentBounds(snapshot, 760, 320);
			Assert.That(bounds.Width, Is.EqualTo(760));
			Assert.That(bounds.Height, Is.EqualTo(320));
			Assert.That(bounds.X * 2 + bounds.Width, Is.EqualTo(width).Within(1));
			Assert.That(bounds.Y * 2 + bounds.Height, Is.EqualTo(height - snapshot.LogicalPoints(24)).Within(1));
		}

		[Test]
		public void DetachedFilterPopupReceivesItsOwnScopedPreparationHook()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (root != null && !Directory.Exists(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets")))
				root = Directory.GetParent(root)?.FullName;
			var source = File.ReadAllText(Path.Combine(root!, "engine", "OpenRA.Mods.Common", "Widgets", "Logic", "IosTouchMenuLogic.cs"));
			Assert.That(source, Does.Contain("filters.PreparePanel = PrepareMultiplayerFilters"));
			Assert.That(source, Does.Contain("filters.GetPopupSafeBounds"));
		}

		[Test]
		public void FilterPopupRowsStayLargeAndScrollableInsidePhoneSafeArea()
		{
			var snapshot = new IosScreenSnapshot(new Size(1558, 720), new Size(844, 390),
				new IosSafeAreaInsets(47, 0, 47, 21));
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var popup = MultiplayerScreenLayout.FilterPanelBounds(snapshot, 200, 5);
			Assert.That(popup.Width, Is.LessThanOrEqualTo(snapshot.SafeBounds.Width));
			Assert.That(popup.Height, Is.LessThanOrEqualTo(snapshot.SafeBounds.Height));
			for (var i = 0; i < 5; i++)
			{
				var row = MultiplayerScreenLayout.FilterRowBounds(popup.Width, i, policy);
				Assert.That(row.Height / snapshot.LogicalPerPoint, Is.GreaterThanOrEqualTo(48));
				Assert.That(row.Right, Is.LessThan(popup.Width));
				if (i > 0)
					Assert.That(row.Y, Is.GreaterThan(MultiplayerScreenLayout.FilterRowBounds(popup.Width, i - 1, policy).Bottom));
			}
		}

		[Test]
		public void PhoneCreationFormScrollsWithoutCompressingFieldsOrNotices()
		{
			var snapshot = new IosScreenSnapshot(new Size(1558, 720), new Size(844, 390),
				new IosSafeAreaInsets(47, 0, 47, 21));
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var content = MultiplayerScreenLayout.ContentBounds(snapshot, 1120, 700);
			var layout = IosServerCreationLayout.Create(content.Width, content.Height, policy);
			Assert.That(layout.FormContentHeight, Is.GreaterThan(layout.Form.Height));
			foreach (var row in new[] { layout.ServerNameRow, layout.PasswordRow, layout.PortRow })
			{
				Assert.That((row.Height - policy.MinimumReadableTextHeight - policy.Gap) / policy.LogicalPerPoint,
					Is.GreaterThanOrEqualTo(48));
				Assert.That(row.Width / policy.LogicalPerPoint, Is.GreaterThan(400));
			}
			Assert.That(layout.ServerNameRow.Bottom, Is.LessThan(layout.PasswordRow.Y));
			Assert.That(layout.PasswordRow.Bottom, Is.LessThan(layout.PortRow.Y));
			Assert.That(layout.NoticesBody.Height / policy.LogicalPerPoint, Is.GreaterThanOrEqualTo(144));
			Assert.That(layout.Form.Right, Is.LessThan(layout.MapPreview.X));
			Assert.That(layout.ChangeMap.Bottom, Is.LessThanOrEqualTo(layout.Footer.Y));
		}

		[Test]
		public void FullWindowBrowserGivesDetailsThirtyPercentAndKeepsActionsReachable()
		{
			var policy = IosMenuLayoutPolicy.Create(true, 1920, 1080);
			var layout = IosMultiplayerBrowserLayout.Create(1766, 992, policy, true);
			Assert.That(layout.Details.Width / 1766d, Is.EqualTo(.3).Within(.001));
			Assert.That(layout.Table.Right, Is.LessThan(layout.Details.X));
			foreach (var button in new[] { layout.LocalMode, layout.OnlineMode, layout.Reload,
				layout.DirectConnect, layout.CreateButton, layout.Back, layout.RoomCodeButton })
			{
				Assert.That(button.Width, Is.GreaterThanOrEqualTo(48));
				Assert.That(button.Height, Is.GreaterThanOrEqualTo(48));
			}
		}

		[Test]
		public void MultiplayerShellsKeepNetworkControlsAndUseFullWindowArtwork()
		{
			var root = TestContext.CurrentContext.TestDirectory;
			while (root != null && !Directory.Exists(Path.Combine(root, "engine", "OpenRA.Mods.Common", "Widgets")))
				root = Directory.GetParent(root)?.FullName;
			Assert.That(root, Is.Not.Null);
			foreach (var file in new[] { "multiplayer-browser.yaml", "multiplayer-createserver.yaml", "multiplayer-directconnect.yaml" })
			{
				var yaml = MiniYaml.FromString(File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", file)), file).Single(node => node.Key.StartsWith("StretchBackground@", StringComparison.Ordinal));
				Assert.That(yaml.Key, Does.StartWith("StretchBackground@"));
				Assert.That(yaml.Value.NodeWithKey("PreserveAspectRatio").Value.Value, Is.EqualTo("false"));
				Assert.That(yaml.Value.NodeWithKey("Width").Value.Value, Is.EqualTo("WINDOW_WIDTH"));
				Assert.That(yaml.Value.NodeWithKey("Height").Value.Value, Is.EqualTo("WINDOW_HEIGHT"));
				Assert.That(yaml.Value.NodeWithKey("Children").Value.Nodes.Any(node =>
					node.Key.EndsWith("@MULTIPLAYER_CONTENT", StringComparison.Ordinal)), Is.True);
			}
		}
	}
}
