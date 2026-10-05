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
using System.Linq;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Network;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class NukeHourRankedUiPolicyTest
	{
		[TestCase("Commander", "password12345", "password12345", null)]
		[TestCase("指挥官", "password12345", "password12345", null)]
		[TestCase("Commander", "password12345", "different1234", "label-ranked-error-password-match")]
		[TestCase("Commander", "short", "short", "label-ranked-error-password-length")]
		[TestCase("12abc", "password12345", "password12345", "label-ranked-error-username-format")]
		[TestCase("ab", "password12345", "password12345", "label-ranked-error-username-length")]
		[TestCase("abcdefghijklmnopqrstuvwxy", "password12345", "password12345", "label-ranked-error-username-length")]
		[TestCase("bad name", "password12345", "password12345", "label-ranked-error-username-format")]
		public void RegistrationValidatesBothPasswordsBeforeSubmitting(string username, string password, string confirmation, string expected)
		{
			var type = typeof(RankedPanelLayout).Assembly.GetType("OpenRA.Mods.Common.Widgets.Logic.RankedRegistrationForm");
			Assert.That(type, Is.Not.Null, "Registration needs an explicit confirmation policy");
			Assert.That(type.GetMethod("Validate").Invoke(null, new object[] { username, password, confirmation }), Is.EqualTo(expected));
		}

		[TestCase(true, 750, 328, true)]
		[TestCase(true, 844, 390, true)]
		[TestCase(true, 956, 440, true)]
		[TestCase(true, 1180, 820, false)]
		[TestCase(false, 1280, 720, false)]
		public void RegistrationThreeRowsFitDevicePanel(bool ios, int width, int height, bool phone)
		{
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(width, height),
				phone ? new IosSafeAreaInsets(47, 0, 47, 21) : default);
			var content = new ContainerWidget { Bounds = new WidgetBounds(0, 0, width, height) };
			foreach (var id in new[] { "USERNAME_LABEL", "USERNAME", "PASSWORD_LABEL", "PASSWORD", "CONFIRM_PASSWORD_LABEL", "CONFIRM_PASSWORD", "STATUS", "LOGIN_BUTTON", "REGISTER_BUTTON" })
				content.AddChild(new ContainerWidget { Id = id });
			var type = typeof(RankedPanelLayout).Assembly.GetType("OpenRA.Mods.Common.Widgets.Logic.RankedRegistrationForm");
			Assert.That(type, Is.Not.Null);
			type.GetMethod("ApplyLayout").Invoke(null, new object[] { content, ios, snapshot });
			var minimum = IosMenuLayoutPolicy.Create(ios, snapshot).MinimumTarget;
			foreach (var id in new[] { "USERNAME", "PASSWORD", "CONFIRM_PASSWORD", "LOGIN_BUTTON", "REGISTER_BUTTON" })
			{
				var b = content.Get(id).Bounds;
				Assert.That(b.Height, Is.GreaterThanOrEqualTo(minimum), id);
				Assert.That(b.Bottom, Is.LessThanOrEqualTo(height), id);
				Assert.That(b.Right, Is.LessThanOrEqualTo(width), id);
			}
			Assert.That(content.Get("USERNAME").Bounds.Bottom, Is.LessThanOrEqualTo(content.Get("PASSWORD").Bounds.Top));
			Assert.That(content.Get("PASSWORD").Bounds.Bottom, Is.LessThanOrEqualTo(content.Get("CONFIRM_PASSWORD").Bounds.Top));
			Assert.That(content.Get("CONFIRM_PASSWORD").Bounds.Bottom, Is.LessThanOrEqualTo(content.Get("STATUS").Bounds.Top));
			Assert.That(content.Get("STATUS").Bounds.Bottom, Is.LessThanOrEqualTo(content.Get("LOGIN_BUTTON").Bounds.Top));
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null &&
				!File.Exists(Path.Combine(directory.FullName, ".git")) &&
				!Directory.Exists(Path.Combine(directory.FullName, ".git")))
				directory = directory.Parent;
			Assert.That(directory, Is.Not.Null);
			return directory!.FullName;
		}

		[TestCase(RankedClientState.Accepted, false)]
		[TestCase(RankedClientState.Assigned, false)]
		[TestCase(RankedClientState.Queued, true)]
		[TestCase(RankedClientState.Proposal, true)]
		[TestCase(RankedClientState.Ready, true)]
		public void AcceptedMatchCannotLeaveWhileServerAssignmentIsPending(RankedClientState state, bool expected)
		{
			Assert.That(RankedPanelLayout.CanLeaveMatch(state, false), Is.EqualTo(expected));
			Assert.That(RankedPanelLayout.CanLeaveMatch(state, true), Is.False);
		}

		[TestCase(RankedClientState.Ready, true)]
		[TestCase(RankedClientState.Error, true)]
		[TestCase(RankedClientState.Queued, false)]
		[TestCase(RankedClientState.Proposal, false)]
		[TestCase(RankedClientState.Accepted, false)]
		[TestCase(RankedClientState.Assigned, false)]
		[TestCase(RankedClientState.Cooldown, false)]
		public void LogoutCannotAbandonLiveMatchState(RankedClientState state, bool expected)
		{
			Assert.That(RankedPanelLayout.CanLogout(state), Is.EqualTo(expected));
		}

		[TestCase(true, 1558, 720, 844, 390, true)]
		[TestCase(true, 2360, 1640, 1180, 820, false)]
		[TestCase(false, 844, 390, 844, 390, false)]
		public void MatchControlsUseNativeDeviceClassAndUsableTouchTargets(
			bool ios, int width, int height, int nativeWidth, int nativeHeight, bool phone)
		{
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(nativeWidth, nativeHeight),
				phone ? new IosSafeAreaInsets(47, 0, 47, 21) : default);
			var bounds = ios ? RankedPanelLayout.ContentBounds(snapshot, 820, 620) : new WidgetBounds(0, 0, width, height);
			width = bounds.Width;
			height = bounds.Height;
			var content = new ContainerWidget { Id = "RANKED_CONTENT", Bounds = bounds };
			var ids = new[] { "SCENE", "DOSSIER", "OPERATION_SURFACE", "TITLE", "USERNAME_LABEL", "PASSWORD_LABEL",
				"USERNAME", "PASSWORD", "ACCOUNT", "STATUS", "LOGIN_BUTTON", "REGISTER_BUTTON", "QUEUE_BUTTON",
				"CANCEL_BUTTON", "ACCEPT_BUTTON", "DECLINE_BUTTON", "LOGOUT_BUTTON", "BACK_BUTTON" };
			foreach (var id in ids)
				content.AddChild(new ContainerWidget { Id = id });
			RankedPanelLayout.ApplyResponsive(content, ios, snapshot);
			foreach (var child in content.Children)
			{
				Assert.That(child.Bounds.X, Is.GreaterThanOrEqualTo(0), child.Id);
				Assert.That(child.Bounds.Y, Is.GreaterThanOrEqualTo(0), child.Id);
				Assert.That(child.Bounds.Right, Is.LessThanOrEqualTo(width), child.Id);
				Assert.That(child.Bounds.Bottom, Is.LessThanOrEqualTo(height), child.Id);
				if (child.Id.EndsWith("_BUTTON") || child.Id is "USERNAME" or "PASSWORD")
					Assert.That(child.Bounds.Height, Is.GreaterThanOrEqualTo(IosMenuLayoutPolicy.Create(ios, snapshot).MinimumTarget), child.Id);
			}

			Assert.That(content.Get("SCENE").IsVisible(), Is.True);
			Assert.That(content.Get("USERNAME_LABEL").Bounds.X, Is.GreaterThanOrEqualTo(width * 4 / 10));
			Assert.That(content.Get("USERNAME").Bounds.Bottom, Is.LessThanOrEqualTo(content.Get("PASSWORD").Bounds.Top));
			Assert.That(content.Get("PASSWORD").Bounds.Bottom, Is.LessThanOrEqualTo(content.Get("STATUS").Bounds.Top));
			Assert.That(content.Get("STATUS").Bounds.Bottom, Is.LessThanOrEqualTo(content.Get("LOGIN_BUTTON").Bounds.Top));
			Assert.That(content.Get("LOGIN_BUTTON").Bounds.Bottom, Is.LessThanOrEqualTo(content.Get("BACK_BUTTON").Bounds.Top));
		}

		[TestCase(956, 440)]
		[TestCase(844, 390)]
		public void PhoneUsesDedicatedWholeArtAndKeepsLiveControlsInSafeArea(int width, int height)
		{
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(width, height), new IosSafeAreaInsets(62, 0, 62, 21));
			var content = new BackgroundWidget { Bounds = RankedPanelLayout.ContentBounds(snapshot, 820, 620) };
			foreach (var id in new[] { "FULL_ART", "SCENE", "DOSSIER", "OPERATION_SURFACE", "TITLE", "USERNAME_LABEL", "PASSWORD_LABEL",
				"USERNAME", "PASSWORD", "ACCOUNT", "STATUS", "LOGIN_BUTTON", "REGISTER_BUTTON", "QUEUE_BUTTON", "CANCEL_BUTTON", "ACCEPT_BUTTON", "DECLINE_BUTTON", "LOGOUT_BUTTON", "BACK_BUTTON" })
				content.AddChild(new ContainerWidget { Id = id });
			RankedPanelLayout.ApplyResponsive(content, true, snapshot);
			Assert.That(content.Get("FULL_ART").IsVisible(), Is.True);
			Assert.That(content.Get("SCENE").IsVisible(), Is.False);
			Assert.That(content.Get("OPERATION_SURFACE").IsVisible(), Is.False);
			Assert.That(content.Get("PASSWORD").Bounds.Bottom, Is.LessThanOrEqualTo(content.Get("STATUS").Bounds.Top));
			Assert.That(content.Get("STATUS").Bounds.Bottom, Is.LessThanOrEqualTo(content.Get("CANCEL_BUTTON").Bounds.Top));
			Assert.That(content.Get("CANCEL_BUTTON").Bounds.Height, Is.GreaterThanOrEqualTo(48));
		}

		[TestCase(956, 440, true)]
		[TestCase(844, 390, true)]
		[TestCase(956, 440, false)]
		[TestCase(844, 390, false)]
		public void PhoneHeaderReturnClearsBodyWithoutFooter(int width, int height, bool hub)
		{
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(width, height), new IosSafeAreaInsets(62, 0, 62, 26));
			var content = new BackgroundWidget { Bounds = RankedPanelLayout.ContentBounds(snapshot, 820, 620) };
			foreach (var id in new[] { "FULL_ART", "TITLE", "BACK_BUTTON", hub ? "LOCAL_BUTTON" : "USERNAME", hub ? "RANKED_BUTTON" : "PASSWORD", "ONLINE_BUTTON", "LEADERBOARD_BUTTON", "STATUS", "LOGIN_BUTTON", "CANCEL_BUTTON", "ACCOUNT", "MATCH_HEADING", "MATCH_SIGNAL" })
				content.AddChild(new ContainerWidget { Id = id });
			RankedPanelLayout.ApplyResponsive(content, true, snapshot);
			var back = content.Get("BACK_BUTTON").Bounds;
			Assert.That(back.Y, Is.LessThan(12));
			Assert.That(back.Height, Is.GreaterThanOrEqualTo(48));
			Assert.That(back.Right, Is.LessThanOrEqualTo(content.Bounds.Width));
			Assert.That(content.Get(hub ? "LOCAL_BUTTON" : "USERNAME").Bounds.Top, Is.GreaterThanOrEqualTo(back.Bottom));
			if (!hub)
			{
				Assert.That(content.Get("ACCOUNT").Bounds.Bottom, Is.LessThanOrEqualTo(content.Get("MATCH_HEADING").Bounds.Top));
				Assert.That(content.Get("MATCH_HEADING").Bounds.Bottom, Is.LessThanOrEqualTo(content.Get("MATCH_SIGNAL").Bounds.Top));
				Assert.That(content.Get("MATCH_SIGNAL").Bounds.Bottom, Is.LessThanOrEqualTo(content.Get("STATUS").Bounds.Top));
			}
			if (hub)
				Assert.That(content.Get("ONLINE_BUTTON").Bounds.Bottom, Is.GreaterThanOrEqualTo(content.Bounds.Height - 8));
		}

		[Test]
		public void ArmoredPagesKeepArtworkProportionsAndLiveText()
		{
			var yaml = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "chrome", "ranked.yaml"));
			Assert.That(yaml, Does.Not.Contain("PreserveAspectRatio: false"));
			Assert.That(yaml, Does.Contain("Background: cc-ranked-armor-shell"));
			Assert.That(yaml, Does.Contain("Background: cc-ranked-desktop-primary"));
			Assert.That(yaml, Does.Contain("Background: cc-ranked-armor-archive"));
		}

		[TestCase(true, 1558, 720, 844, 390)]
		[TestCase(true, 2360, 1640, 1180, 820)]
		[TestCase(false, 1280, 720, 1280, 720)]
		public void HubCardsClearHeaderAndFooter(bool ios, int width, int height, int nativeWidth, int nativeHeight)
		{
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(nativeWidth, nativeHeight), default);
			var content = new BackgroundWidget { Bounds = new WidgetBounds(0, 0, width, height) };
			foreach (var id in new[] { "TITLE", "LOCAL_BUTTON", "RANKED_BUTTON", "ONLINE_BUTTON", "LEADERBOARD_BUTTON", "BACK_BUTTON" })
				content.AddChild(new ContainerWidget { Id = id });
			RankedPanelLayout.ApplyResponsive(content, ios, snapshot);
			var title = content.Get("TITLE").Bounds;
			var back = content.Get("BACK_BUTTON").Bounds;
			foreach (var id in new[] { "LOCAL_BUTTON", "RANKED_BUTTON", "ONLINE_BUTTON", "LEADERBOARD_BUTTON" })
			{
				var card = content.Get(id).Bounds;
				Assert.That(card.Width, Is.GreaterThan(0));
				Assert.That(card.Y, Is.GreaterThanOrEqualTo(title.Bottom));
				Assert.That(card.Bottom, Is.LessThanOrEqualTo(back.Y));
			}

			Assert.That(content.Get("LOCAL_BUTTON").Bounds.X, Is.LessThan(content.Get("RANKED_BUTTON").Bounds.X));
			Assert.That(content.Get("LOCAL_BUTTON").Bounds.Y, Is.LessThan(content.Get("ONLINE_BUTTON").Bounds.Y));
			Assert.That(content.Background, Is.EqualTo(ios && nativeHeight < 600 ? "cc-ranked-armor-phone-shell" : "cc-ranked-armor-shell"));
		}

		[TestCase(620, 88)]
		[TestCase(1100, 48)]
		public void AsynchronouslyLoadedLeaderboardRowsReflowAtCurrentListWidth(int width, int rowHeight)
		{
#pragma warning disable SYSLIB0050
			var list = (ScrollPanelWidget)FormatterServices.GetUninitializedObject(typeof(ScrollPanelWidget));
#pragma warning restore SYSLIB0050
			typeof(Widget).GetField(nameof(Widget.Children)).SetValue(list, new List<Widget>());
			list.Bounds = new WidgetBounds(0, 0, width, 400);
			list.Layout = new ListLayout(list);
			for (var i = 0; i < 3; i++)
			{
				var row = new ContainerWidget { Width = new IntegerExpression("PARENT_WIDTH - 24"), Bounds = new WidgetBounds(0, 0, 20, 48) };
				row.AddChild(new ContainerWidget { Width = new IntegerExpression("PARENT_WIDTH * 43 / 100"), Height = new IntegerExpression("PARENT_HEIGHT") });
				list.AddChild(row);
			}

			RankedPanelLayout.LayoutLeaderboardRows(list, new Size(width, 720), rowHeight);
			for (var i = 0; i < 3; i++)
			{
				var row = list.Children[i];
				Assert.That(row.Bounds.Width, Is.EqualTo(width - 24));
				Assert.That(row.Bounds.Height, Is.EqualTo(rowHeight));
				Assert.That(row.Children[0].Bounds.Width, Is.EqualTo((width - 24) * 43 / 100));
				if (i > 0)
					Assert.That(row.Bounds.Y, Is.GreaterThanOrEqualTo(list.Children[i - 1].Bounds.Bottom));
			}
		}

		[TestCase(1280, 720, 1672, 941)]
		[TestCase(1180, 800, 1448, 1086)]
		[TestCase(2000, 900, 1672, 941)]
		[TestCase(400, 64, 1564, 250)]
		public void CompleteArtworkFitsWithoutStretchingOrLosingItsFrame(int width, int height, int sourceWidth, int sourceHeight)
		{
			var target = new Rectangle(0, 0, width, height);
			var actual = ButtonWidget.AspectFitBounds(target, new Size(sourceWidth, sourceHeight));
			Assert.That(actual.Width / (double)actual.Height, Is.EqualTo(sourceWidth / (double)sourceHeight).Within(0.04));
			Assert.That(actual.X, Is.GreaterThanOrEqualTo(0));
			Assert.That(actual.Y, Is.GreaterThanOrEqualTo(0));
			Assert.That(actual.Right, Is.LessThanOrEqualTo(width));
			Assert.That(actual.Bottom, Is.LessThanOrEqualTo(height));
		}

		[Test]
		public void MultiplayerHubKeepsRankedRoomsLocalAndLeaderboardSeparate()
		{
			var root = RepositoryRoot();
			var yaml = File.ReadAllText(Path.Combine(root, "mods", "ra2", "chrome", "ranked.yaml"));
			var manifest = File.ReadAllText(Path.Combine(root, "mods", "ra2", "mod.yaml"));
			Assert.Multiple(() =>
			{
				Assert.That(manifest, Does.Contain("ra2|chrome/ranked.yaml"));
				Assert.That(yaml, Does.Contain("Button@RANKED_BUTTON:"));
				Assert.That(yaml, Does.Contain("Button@ONLINE_BUTTON:"));
				Assert.That(yaml, Does.Contain("Button@LOCAL_BUTTON:"));
				Assert.That(yaml, Does.Contain("Button@LEADERBOARD_BUTTON:"));
				Assert.That(yaml, Does.Contain("Label@ACCOUNT:"));
				Assert.That(yaml, Does.Contain("StretchBackground@RANKED_MATCH_PANEL:"));
				Assert.That(yaml, Does.Contain("StretchBackground@RANKED_RESULT_PANEL:"));
				Assert.That(yaml, Does.Contain("StretchBackground@RANKED_LEADERBOARD_PANEL:"));
				Assert.That(yaml, Does.Not.Contain("CUSTOM_COLOR"));
				Assert.That(yaml, Does.Not.Contain("HSV"));
			});
		}

		[TestCase(760, 560)]
		[TestCase(844, 369)]
		[TestCase(1920, 1080)]
		[TestCase(820, 620)]
		[TestCase(1400, 650)]
		[TestCase(1000, 720)]
		public void RankedTemplatesReflowAllControlsInsideTheirParent(int width, int height)
		{
			var path = Path.Combine(RepositoryRoot(), "mods", "ra2", "chrome", "ranked.yaml");
			ContainerWidget Read(MiniYamlNode node)
			{
				string Value(string key) => node.Value.Nodes.FirstOrDefault(n => n.Key == key)?.Value.Value;
				var widget = new ContainerWidget { Id = node.Key.Split('@').Last() };
				widget.X = Value("X") is string x ? new IntegerExpression(x) : null;
				widget.Y = Value("Y") is string y ? new IntegerExpression(y) : null;
				widget.Width = Value("Width") is string w ? new IntegerExpression(w) : null;
				widget.Height = Value("Height") is string h ? new IntegerExpression(h) : null;
				var children = node.Value.Nodes.FirstOrDefault(n => n.Key == "Children");
				if (children != null)
					foreach (var child in children.Value.Nodes)
						widget.AddChild(Read(child));
				return widget;
			}

			void Check(Widget parent)
			{
				foreach (var child in parent.Children)
				{
					Assert.That(child.Bounds.X, Is.GreaterThanOrEqualTo(0), child.Id);
					Assert.That(child.Bounds.Y, Is.GreaterThanOrEqualTo(0), child.Id);
					Assert.That(child.Bounds.Right, Is.LessThanOrEqualTo(parent.Bounds.Width), child.Id);
					Assert.That(child.Bounds.Bottom, Is.LessThanOrEqualTo(parent.Bounds.Height), child.Id);
					Check(child);
				}
			}

			foreach (var page in MiniYaml.FromFile(path))
			{
				var content = Read(page).Children.Single();
				content.Bounds = new WidgetBounds(0, 0, width, height);
				RankedPanelLayout.Reflow(content, new Size(width, height));
				RankedPanelLayout.ApplyResponsive(content, false, new IosScreenSnapshot(new Size(width, height), new Size(width, height), default));
				Check(content);
			}
		}

		[Test]
		public void PhoneRankedContentFillsSafeAreaBehindExactlyTwoPointBorder()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(1558, 720), new Size(844, 390), new IosSafeAreaInsets(47, 0, 47, 21));
			var safe = snapshot.SafeBounds;
			var border = snapshot.LogicalPoints(2);
			var content = RankedPanelLayout.ContentBounds(snapshot, 820, 620);

			Assert.Multiple(() =>
			{
				Assert.That(RankedPanelLayout.PhoneBorderPoints, Is.EqualTo(2));
				Assert.That(content.X, Is.EqualTo(safe.Left + border));
				Assert.That(content.Y, Is.EqualTo(safe.Top + border));
				Assert.That(content.Right, Is.EqualTo(safe.Right - border));
				Assert.That(content.Bottom, Is.EqualTo(safe.Bottom - border));
			});
		}

		[Test]
		public void TabletRankedLayoutFillsSafeArea()
		{
			var snapshot = new IosScreenSnapshot(
				new Size(2360, 1640), new Size(1180, 820), new IosSafeAreaInsets(0, 0, 0, 20));
			var content = RankedPanelLayout.ContentBounds(snapshot, 820, 620);
			Assert.Multiple(() =>
			{
				Assert.That(content.Width, Is.EqualTo(snapshot.SafeBounds.Width));
				Assert.That(content.Height, Is.EqualTo(snapshot.SafeBounds.Height));
				Assert.That(content.X, Is.EqualTo(snapshot.SafeBounds.Left));
			});
		}

		[TestCase("")]
		[TestCase("zh-CN")]
		public void RankedNavigationIsLocalized(string language)
		{
			var root = RepositoryRoot();
			var path = string.IsNullOrEmpty(language)
				? Path.Combine(root, "mods", "ra2", "fluent", "chrome.ftl")
				: Path.Combine(root, "mods", "ra2", "fluent", language, "chrome.ftl");
			var text = File.ReadAllText(path);
			Assert.Multiple(() =>
			{
				Assert.That(text, Does.Contain("button-ranked-match ="));
				Assert.That(text, Does.Contain("button-online-rooms ="));
				Assert.That(text, Does.Contain("button-local-multiplayer ="));
				Assert.That(text, Does.Contain("button-season-leaderboard ="));
				Assert.That(text, Does.Contain("label-ranked-result-rating ="));
			});
		}
	}
}
