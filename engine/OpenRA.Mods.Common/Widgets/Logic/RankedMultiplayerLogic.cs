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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using OpenRA.Network;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public static class RankedPanelLayout
	{
		public const int PhoneBorderPoints = 2;

		public static bool CanLeaveMatch(RankedClientState state, bool busy) =>
			!busy && state is not (RankedClientState.Accepted or RankedClientState.Assigned);

		public static bool CanLogout(RankedClientState state) =>
			state is RankedClientState.Ready or RankedClientState.Error;

		public static WidgetBounds ContentBounds(IosScreenSnapshot snapshot, int maximumWidth, int maximumHeight)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			if (!policy.IsPhone)
				return new WidgetBounds(snapshot.SafeBounds.X, snapshot.SafeBounds.Y,
					snapshot.SafeBounds.Width, snapshot.SafeBounds.Height);

			var border = snapshot.LogicalPoints(PhoneBorderPoints);
			var safe = snapshot.SafeBounds;
			return new WidgetBounds(
				safe.Left + border,
				safe.Top + border,
				Math.Max(1, safe.Width - 2 * border),
				Math.Max(1, safe.Height - 2 * border));
		}

		public static void Apply(Widget root, int maximumWidth = 820, int maximumHeight = 620)
		{
			var content = root.GetOrNull("RANKED_CONTENT");
			if (content == null)
				return;
			var size = Game.Renderer.Resolution;
			if (Platform.UsesMobileLayout)
				content.Bounds = ContentBounds(IosScreenMetrics.SnapshotFor(size), maximumWidth, maximumHeight);
			else
				content.Bounds = new WidgetBounds(0, 0, size.Width, size.Height);

			var snapshot = IosScreenMetrics.SnapshotFor(size);
			var phone = IosMenuLayoutPolicy.Create(Platform.UsesMobileLayout, snapshot).IsPhone;
			if (Platform.UsesMobileLayout && !phone && content.GetOrNull("FULL_ART") != null)
			{
				var page = content.GetOrNull("LOCAL_BUTTON") != null ? "hub" : content.GetOrNull("USERNAME") != null ? "match" : "leaderboard";
				var wide = (double)size.Width / size.Height >= 1.39;
				var images = ChromeProvider.TryGetPanelImages("cc-ranked-tablet-" + (wide ? "wide-" : "") + page);
				if (images != null && images.Length > 4 && images[4] != null)
					{
					var fit = ButtonWidget.AspectFitBounds(new Rectangle(0, 0, size.Width, size.Height), images[4].Bounds.Size);
					content.Bounds = new WidgetBounds(fit.X, fit.Y, fit.Width, fit.Height);
				}
			}


			Reflow(content, size);
			ApplyResponsive(content, Platform.UsesMobileLayout, IosScreenMetrics.SnapshotFor(size));
			if (Platform.UsesMobileLayout)
				IosTouchMenuLogic.ApplyIosFonts(content);
		}

		public static void ApplyResponsive(Widget content, bool isIos, IosScreenSnapshot snapshot)
		{
			var policy = IosMenuLayoutPolicy.Create(isIos, snapshot);
			var phone = policy.IsPhone;
			var tablet = isIos && !phone;
			var w = content.Bounds.Width;
			var h = content.Bounds.Height;
			var target = policy.MinimumTarget;
			var fullArt = content.GetOrNull("FULL_ART");
			var wholePage = fullArt != null;
			if (fullArt != null)
				fullArt.IsVisible = () => true;
			if (content is BackgroundWidget surface)
				surface.Background = phone ? "cc-ranked-armor-phone-shell" : wholePage ? "" : "cc-ranked-armor-shell";
			Widget Find(string id) => content.GetOrNull(id);
			void Place(string id, int x, int y, int width, int height)
			{
				var child = Find(id);
				if (child == null)
					return;
				child.Bounds = new WidgetBounds(x, y, width, height);
				Reflow(child, snapshot.EffectiveSize);
			}

			if (Find("USERNAME") != null)
			{
				var left = w * (phone ? 44 : tablet ? 44 : 46) / 100;
				var width = w * 95 / 100 - left;
				var controlHeight = Math.Max(target, h * 9 / 100);
				Place("SCENE", w * 3 / 100, h * 16 / 100, w * 33 / 100, h * 48 / 100);
				var scene = Find("SCENE");
				if (scene != null)
					scene.IsVisible = () => !wholePage;
				Place("DOSSIER", w * 5 / 100, h * 68 / 100, w * 35 / 100, h * 13 / 100);
				var dossier = Find("DOSSIER");
				if (dossier != null)
					dossier.IsVisible = () => !phone && !wholePage;
				var operation = Find("OPERATION_SURFACE");
				if (operation != null)
					operation.IsVisible = () => !wholePage;
				Place("OPERATION_SURFACE", left - w / 100, h * 17 / 100, width + w * 2 / 100, h * 64 / 100);
				Place("TITLE", w * 5 / 100, h * 5 / 100, w * 90 / 100, h * 10 / 100);
				Place("MATCH_HEADING", left, h * 17 / 100, width, h * 12 / 100);
				Place("MATCH_SIGNAL", left + (width - h * 16 / 100) / 2, h * 30 / 100, h * 16 / 100, h * 16 / 100);
				var labelWidth = width * 25 / 100;
				var fieldX = left + labelWidth + w / 100;
				foreach (var row in new[] { ("USERNAME_LABEL", "USERNAME", 20), ("PASSWORD_LABEL", "PASSWORD", 36) })
				{
					Place(row.Item1, left, h * row.Item3 / 100, labelWidth, controlHeight);
					Place(row.Item2, fieldX, h * row.Item3 / 100, left + width - fieldX, controlHeight);
				}

				Place("ACCOUNT", phone && wholePage ? left : w * 5 / 100, h * (phone && wholePage ? 35 : 70) / 100, phone && wholePage ? width : w * 31 / 100, controlHeight);
				Place("STATUS", left, h * 51 / 100, width, h * 12 / 100);
				var gap = Math.Max(8, w / 100);
				var half = (width - gap) / 2;
				foreach (var pair in new[] { ("LOGIN_BUTTON", "REGISTER_BUTTON"), ("ACCEPT_BUTTON", "DECLINE_BUTTON") })
				{
					Place(pair.Item1, left, h * 65 / 100, half, controlHeight);
					Place(pair.Item2, left + half + gap, h * 65 / 100, width - half - gap, controlHeight);
				}

				Place("QUEUE_BUTTON", left, h * 65 / 100, width, controlHeight);
				Place("CANCEL_BUTTON", left, h * 65 / 100, width, controlHeight);
				Place("LOGOUT_BUTTON", w * 5 / 100, Math.Min(h * 85 / 100, h - controlHeight), w * 25 / 100, controlHeight);
				Place("BACK_BUTTON", w * 70 / 100, Math.Min(h * 85 / 100, h - controlHeight), w * 25 / 100, controlHeight);
			}
			else if (Find("LEADERBOARD_LIST") != null)
			{
				Place("TITLE", w * 5 / 100, h * 4 / 100, w * 90 / 100, h * 10 / 100);
				Place("SCENE", w * 5 / 100, h * 16 / 100, w * 90 / 100, h * 14 / 100);
				var scene = Find("SCENE");
				if (scene != null)
					scene.IsVisible = () => !wholePage;
				var statusY = 31;
				var headerY = 39;
				var listY = 47;
				Place("STATUS", w * 6 / 100, h * statusY / 100, w * 88 / 100, h * 7 / 100);
				Place("HEADER", w * 6 / 100, h * headerY / 100, w * 88 / 100, h * 7 / 100);
				Place("LEADERBOARD_LIST", w * 6 / 100, h * listY / 100, w * 88 / 100, h * (83 - listY) / 100);
				if (Find("LEADERBOARD_LIST") is ScrollPanelWidget list)
					LayoutLeaderboardRows(list, snapshot.EffectiveSize, Math.Max(48, target));
				var footerHeight = Math.Max(target, h * 9 / 100);
				Place("BACK_BUTTON", w * 70 / 100, Math.Min(h * 85 / 100, h - footerHeight), w * 25 / 100, footerHeight);
			}
			else if (Find("RESULT") != null)
			{
				Place("TITLE", w * 5 / 100, h * 5 / 100, w * 90 / 100, h * 10 / 100);
				Place("OPPONENT", w * 6 / 100, h * 22 / 100, w * 88 / 100, h * 9 / 100);
				Place("RESULT", w * 6 / 100, h * 35 / 100, w * 88 / 100, h * 14 / 100);
				Place("RATING", w * 6 / 100, h * 52 / 100, w * 88 / 100, h * 10 / 100);
				Place("STATUS", w * 6 / 100, h * 65 / 100, w * 88 / 100, h * 15 / 100);
				Place("BACK_BUTTON", w * 30 / 100, Math.Min(h * 85 / 100, h - target), w * 40 / 100, target);
			}
			else if (Find("LOCAL_BUTTON") != null)
			{
				var gap = policy.Gap;
				var margin = Math.Max(gap, w * 3 / 100);
				var headerHeight = Math.Max(target, h * 8 / 100);
				var footerHeight = Math.Max(target, h * 8 / 100);
				var cardWidth = (w - 2 * margin - gap) / 2;
				var cardTop = gap + headerHeight + gap;
				var footerTop = h - gap - footerHeight;
				var cardHeight = (footerTop - gap - cardTop - gap) / 2;
				Place("TITLE", margin, gap, w - 2 * margin, headerHeight);
				Place("LOCAL_BUTTON", margin, cardTop, cardWidth, cardHeight);
				Place("RANKED_BUTTON", margin + cardWidth + gap, cardTop, cardWidth, cardHeight);
				Place("ONLINE_BUTTON", margin, cardTop + cardHeight + gap, cardWidth, cardHeight);
				Place("LEADERBOARD_BUTTON", margin + cardWidth + gap, cardTop + cardHeight + gap, cardWidth, cardHeight);
				Place("BACK_BUTTON", w - margin - w / 5, footerTop, w / 5, footerHeight);
			}

			if (wholePage && phone)
			{
				var headerPage = Find("LOCAL_BUTTON") != null || Find("USERNAME") != null;
				var headerHeight = Math.Max(target + 12, snapshot.EffectiveSize.Height * 13 / 100);
				Place("TITLE", w * 22 / 100, 0, w * 56 / 100, headerPage ? headerHeight : Math.Max(24, h * 7 / 100));
				Place("BACK_BUTTON", headerPage ? w * 83 / 100 : w * 70 / 100, headerPage ? 4 : h - target - 4,
					headerPage ? w * 17 / 100 : w * 25 / 100, target);
				if (headerPage && Find("BACK_BUTTON") is ButtonWidget back)
					back.GetText = () => "← " + FluentProvider.GetMessage("button-back");
				if (Find("LOCAL_BUTTON") != null)
				{
					var gap = Math.Max(4, w / 100);
					var cw = (w - gap) / 2;
					var ch = (h - headerHeight - gap - 2) / 2;
					foreach (var slot in new[] { ("LOCAL_BUTTON", 0, headerHeight), ("RANKED_BUTTON", cw + gap, headerHeight),
						("ONLINE_BUTTON", 0, headerHeight + ch + gap), ("LEADERBOARD_BUTTON", cw + gap, headerHeight + ch + gap) })
					{
						Place(slot.Item1, slot.Item2, slot.Item3, cw, ch);
						var button = Find(slot.Item1);
						foreach (var id in new[] { "CARD_SCENE", "CARD_FRAME", "CARD_PLATE" })
							if (button.GetOrNull(id) is Widget decoration) decoration.IsVisible = () => false;
						var right = slot.Item1 is "RANKED_BUTTON" or "LEADERBOARD_BUTTON";
						var lower = slot.Item1 is "ONLINE_BUTTON" or "LEADERBOARD_BUTTON";
						var plateY = lower ? 699 : 354;
						var iconBounds = PhoneHubArtworkBounds(new WidgetBounds(right ? 1070 : 142, plateY, 80, 80), snapshot, content.Bounds);
						var textBounds = PhoneHubArtworkBounds(new WidgetBounds(right ? 1190 : 262, plateY, 580, 80), snapshot, content.Bounds);
						if (button.GetOrNull("CARD_ICON") is Widget icon)
						{
							icon.IsVisible = () => true;
							icon.Bounds = new WidgetBounds(iconBounds.X - button.Bounds.X, iconBounds.Y - button.Bounds.Y, iconBounds.Width, iconBounds.Height);
						}
						if (button.GetOrNull("CARD_TITLE") is LabelWidget label)
						{
							label.Bounds = new WidgetBounds(textBounds.X - button.Bounds.X, textBounds.Y - button.Bounds.Y, textBounds.Width, textBounds.Height);
							label.VAlign = TextVAlign.Middle;
						}
					}
				}
				else if (Find("USERNAME") != null)
				{
					var left = w * 46 / 100;
					var width = w * 96 / 100 - left;
					var labelWidth = width * 22 / 100;
					foreach (var row in new[] { ("USERNAME_LABEL", "USERNAME", 24), ("PASSWORD_LABEL", "PASSWORD", 42) })
					{
						Place(row.Item1, left, h * row.Item3 / 100, labelWidth, target);
						Place(row.Item2, left + labelWidth, h * row.Item3 / 100, width - labelWidth, target);
						if (Find(row.Item2) is TextFieldWidget field)
						{ field.RankedPhoneMetalStyle = true; field.LeftMargin = 12; field.RightMargin = 12; }
					}
					Place("STATUS", left, h * 60 / 100, width, h * 9 / 100);
					Place("ACCOUNT", left, h * 18 / 100, width, target);
					Place("MATCH_HEADING", left, h * 32 / 100, width, target);
					Place("MATCH_SIGNAL", left + width / 2 - target / 2, h * 32 / 100 + target + 4, target, target);
					var buttonWidth = (width - 12) / 2;
					foreach (var pair in new[] { ("LOGIN_BUTTON", "REGISTER_BUTTON"), ("ACCEPT_BUTTON", "DECLINE_BUTTON") })
					{
						Place(pair.Item1, left, h * 71 / 100, buttonWidth, target);
						Place(pair.Item2, left + buttonWidth + 12, h * 71 / 100, buttonWidth, target);
					}
					Place("QUEUE_BUTTON", left, h * 71 / 100, width, target);
					Place("CANCEL_BUTTON", left, h * 71 / 100, width, target);
					Place("LOGOUT_BUTTON", left, h - target - 2, width, target);
				}
			}

			if (wholePage && !phone)
			{
				// These coordinates belong to complete device-authored pages, not
				// a generic arrangement of cropped panels. Paint and input use the
				// same uniformly fitted content rectangle.
				Place("TITLE", w * (tablet ? 5 : 36) / 100, h / 100, w * (tablet ? 90 : 50) / 100, h * 5 / 100);
				if (Find("BACK_BUTTON") is ButtonWidget back)
				{
					back.Background = "";
					back.VisualHeight = 0;
					back.TextColor = Color.White;
				}

				Place("BACK_BUTTON", w * 87 / 100, Math.Min(h * 92 / 100, h - Math.Max(target, h * 6 / 100)), w * 10 / 100, Math.Max(target, h * 6 / 100));
				if (tablet)
				{
					var hub = Find("LOCAL_BUTTON") != null;
					Place("BACK_BUTTON", w * (hub ? 86 : 79) / 100, h * (hub ? 91 : 88) / 100,
						w * (hub ? 11 : 18) / 100, Math.Max(target, h * (hub ? 7 : 9) / 100));
				}
				if (Find("LOCAL_BUTTON") != null)
				{
					var top = tablet ? 6 : 7;
					var lower = tablet ? 47 : 48;
					var height = tablet ? 40 : 39;
					foreach (var slot in new[] { ("LOCAL_BUTTON", 2, top), ("RANKED_BUTTON", 51, top),
						("ONLINE_BUTTON", 2, lower), ("LEADERBOARD_BUTTON", 51, lower) })
					{
						Place(slot.Item1, w * slot.Item2 / 100, h * slot.Item3 / 100, w * 47 / 100, h * height / 100);
						var button = Find(slot.Item1);
						foreach (var id in new[] { "CARD_SCENE", "CARD_FRAME", "CARD_PLATE", "CARD_ICON" })
						{
							var decoration = button.GetOrNull(id);
							if (decoration != null)
								decoration.IsVisible = () => false;
						}

						var label = button.GetOrNull("CARD_TITLE");
						if (label != null)
							label.Bounds = new WidgetBounds(button.Bounds.Width * 27 / 100, button.Bounds.Height * (tablet ? 79 : 74) / 100,
								button.Bounds.Width * 63 / 100, button.Bounds.Height * 20 / 100);
					}
				}
				else if (Find("USERNAME") != null)
				{
					var left = w * 54 / 100;
					var width = w * 39 / 100;
					var controlHeight = Math.Max(target, h * 8 / 100);
					Place("MATCH_HEADING", left, h * 17 / 100, width, h * 12 / 100);
					Place("MATCH_SIGNAL", left + (width - h * 16 / 100) / 2, h * 30 / 100, h * 16 / 100, h * 16 / 100);
					foreach (var row in new[] { ("USERNAME_LABEL", "USERNAME", 23), ("PASSWORD_LABEL", "PASSWORD", 37) })
					{
						Place(row.Item1, left, h * row.Item3 / 100, width * 25 / 100, controlHeight);
						Place(row.Item2, left + width * 28 / 100, h * row.Item3 / 100, width * 72 / 100, controlHeight);
					}

					Place("ACCOUNT", w * 20 / 100, h * 68 / 100, w * 21 / 100, controlHeight);
					Place("STATUS", left, h * 51 / 100, width, h * 12 / 100);
					foreach (var pair in new[] { ("LOGIN_BUTTON", "REGISTER_BUTTON"), ("ACCEPT_BUTTON", "DECLINE_BUTTON") })
					{
						Place(pair.Item1, left, h * 68 / 100, width * 47 / 100, controlHeight);
						Place(pair.Item2, left + width * 53 / 100, h * 68 / 100, width * 47 / 100, controlHeight);
					}

					Place("QUEUE_BUTTON", left, h * 68 / 100, width, controlHeight);
					Place("CANCEL_BUTTON", left, h * 68 / 100, width, controlHeight);
					Place("LOGOUT_BUTTON", w * 6 / 100, Math.Min(h * 92 / 100, h - Math.Max(target, h * 6 / 100)), w * 23 / 100, Math.Max(target, h * 6 / 100));
				}
				else if (Find("LEADERBOARD_LIST") != null)
				{
					Place("STATUS", w * 6 / 100, h * 31 / 100, w * 88 / 100, h * 5 / 100);
					Place("HEADER", w * 6 / 100, h * 37 / 100, w * 88 / 100, h * 6 / 100);
					Place("LEADERBOARD_LIST", w * 6 / 100, h * 43 / 100, w * 88 / 100, h * 42 / 100);
					if (Find("LEADERBOARD_LIST") is ScrollPanelWidget list)
						LayoutLeaderboardRows(list, snapshot.EffectiveSize, Math.Max(48, target));
				}
			}

		}

		// The phone artwork is sliced at 120/28 source pixels. Its center is
		// aspect-filled independently of the safe-area content rectangle.
		public static WidgetBounds PhoneHubArtworkBounds(WidgetBounds source, IosScreenSnapshot snapshot, WidgetBounds content)
		{
			var width = snapshot.EffectiveSize.Width;
			var height = snapshot.EffectiveSize.Height;
			var scale = Math.Min(width / 1850d, height / 850d);
			var rail = Math.Max(1, (int)Math.Round(36 * scale));
			var top = Math.Max(1, (int)Math.Round(120 * scale));
			var bottom = Math.Min(height - top - 1, Math.Max(1, (int)Math.Round(28 * scale)));
			var targetWidth = width - 2 * rail;
			var targetHeight = height - top - bottom;
			var sourceWidth = 1778;
			var sourceHeight = 702;
			var cropWidth = sourceWidth;
			var cropHeight = sourceHeight;
			if (sourceWidth / (double)sourceHeight > targetWidth / (double)targetHeight)
				cropWidth = Math.Clamp((int)Math.Round(sourceHeight * targetWidth / (double)targetHeight), 1, sourceWidth);
			else
				cropHeight = Math.Clamp((int)Math.Round(sourceWidth * targetHeight / (double)targetWidth), 1, sourceHeight);
			var cropX = 36 + (sourceWidth - cropWidth) / 2;
			var cropY = 120 + (sourceHeight - cropHeight) / 2;
			int X(int x) => rail + (int)Math.Round((x - cropX) * targetWidth / (double)cropWidth) - content.X;
			int Y(int y) => top + (int)Math.Round((y - cropY) * targetHeight / (double)cropHeight) - content.Y;
			return new WidgetBounds(X(source.X), Y(source.Y), X(source.Right) - X(source.X), Y(source.Bottom) - Y(source.Y));
		}

		public static void LayoutLeaderboardRows(ScrollPanelWidget list, Size viewport, int rowHeight)
		{
			foreach (var row in list.Children)
				row.Height = new IntegerExpression(rowHeight.ToString(CultureInfo.InvariantCulture));
			Reflow(list, viewport);
			list.Layout.AdjustChildren();
		}

		public static void Reflow(Widget parent, Size viewport)
		{
			foreach (var child in parent.Children)
			{
				var variables = new Dictionary<string, int>
				{
					{ "WINDOW_WIDTH", viewport.Width }, { "WINDOW_HEIGHT", viewport.Height },
					{ "PARENT_WIDTH", parent.Bounds.Width }, { "PARENT_HEIGHT", parent.Bounds.Height },
				};
				var width = child.Width?.Evaluate(variables) ?? child.Bounds.Width;
				var height = child.Height?.Evaluate(variables) ?? child.Bounds.Height;
				variables["WIDTH"] = width;
				variables["HEIGHT"] = height;
				// ScrollPanel owns row Y positions and scroll offsets.
				var y = parent is ScrollPanelWidget ? child.Bounds.Y : child.Y?.Evaluate(variables) ?? 0;
				child.Bounds = new WidgetBounds(child.X?.Evaluate(variables) ?? 0, y, width, height);
				Reflow(child, viewport);
			}
		}
	}

	public sealed class MultiplayerHubLogic : ChromeLogic
	{
		readonly Widget root;
		Size lastResolution;

		[ObjectCreator.UseCtor]
		public MultiplayerHubLogic(
			Widget widget, Action onExit, Action openOnline, Action openLocal,
			Action openRanked, Action openLeaderboard)
		{
			root = widget;
			void Open(Action action)
			{
				Ui.CloseWindow();
				action();
			}

			widget.Get<ButtonWidget>("RANKED_BUTTON").OnClick = () => Open(openRanked);
			widget.Get<ButtonWidget>("ONLINE_BUTTON").OnClick = () => Open(openOnline);
			widget.Get<ButtonWidget>("LOCAL_BUTTON").OnClick = () => Open(openLocal);
			widget.Get<ButtonWidget>("LEADERBOARD_BUTTON").OnClick = () => Open(openLeaderboard);
			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () => { Ui.CloseWindow(); onExit(); };
			bool RankedUnavailable() =>
				!Game.Settings.Game.EnableRanked || string.IsNullOrWhiteSpace(Game.Settings.Game.RankedLobbyUrl);
			widget.Get<ButtonWidget>("RANKED_BUTTON").IsDisabled = RankedUnavailable;
			widget.Get<ButtonWidget>("LEADERBOARD_BUTTON").IsDisabled = RankedUnavailable;
			foreach (var id in new[] { "RANKED_BUTTON", "ONLINE_BUTTON", "LOCAL_BUTTON", "LEADERBOARD_BUTTON" })
			{
				var button = widget.Get<ButtonWidget>(id);
				button.Get<LabelWidget>("CARD_TITLE").GetColor = () => button.IsDisabled()
					? Color.FromArgb(255, 150, 145, 137)
					: button.IsVisuallyPressed || Ui.MouseOverWidget == button
						? Color.White : Color.FromArgb(255, 232, 232, 232);
			}
		}

		public override void Tick()
		{
			if (lastResolution != Game.Renderer.Resolution)
			{
				lastResolution = Game.Renderer.Resolution;
				RankedPanelLayout.Apply(root, 760, 560);
			}
		}
	}

	public sealed class RankedMatchLogic : ChromeLogic
	{
		readonly Widget root;
		readonly RankedLobbyClient client;
		readonly RankedClientController controller;
		readonly Action<RankedMatchAssignment> onAssigned;
		readonly TextFieldWidget username;
		readonly PasswordFieldWidget password;
		readonly PasswordFieldWidget confirmation;
		bool registering;
		bool reviewingRecovery;
		string recoveryText;
		readonly LabelWidget status;
		volatile bool busy;
		volatile bool initializationStarted;
		string statusText;
		long nextPoll;
		Size lastResolution;
		bool disposed;

		[ObjectCreator.UseCtor]
		public RankedMatchLogic(Widget widget, Action onExit, Action<RankedMatchAssignment> onAssigned)
		{
			root = widget;
			this.onAssigned = onAssigned;
			username = widget.Get<TextFieldWidget>("USERNAME");
			password = widget.Get<PasswordFieldWidget>("PASSWORD");
			confirmation = widget.Get<PasswordFieldWidget>("CONFIRM_PASSWORD");
			status = widget.Get<LabelWidget>("STATUS");
			status.GetText = () => statusText ?? FluentProvider.GetMessage(reviewingRecovery ? "label-ranked-recovery-heading" :
				registering ? (busy ? "label-ranked-registering" : "label-ranked-registration-hint") : StatusMessage(controller?.State ?? RankedClientState.SignedOut));

			var http = HttpClientFactory.Create();
			http.Timeout = TimeSpan.FromSeconds(10);
			client = new RankedLobbyClient(http,
				RankedLobbyClient.ParseBaseUri(Game.Settings.Game.RankedLobbyUrl));
			var sessions = new RankedSessionManager(client, RankedCredentialStoreFactory.Create());
			controller = new RankedClientController(
				client, sessions, RankedInstallationIdentity.FromLocalProfile,
				() => RankedCompatibility.FromActiveMod(Game.Settings.Game.RankedRegion));

			var login = widget.Get<ButtonWidget>("LOGIN_BUTTON");
			var register = widget.Get<ButtonWidget>("REGISTER_BUTTON");
			var queue = widget.Get<ButtonWidget>("QUEUE_BUTTON");
			var cancel = widget.Get<ButtonWidget>("CANCEL_BUTTON");
			var accept = widget.Get<ButtonWidget>("ACCEPT_BUTTON");
			var decline = widget.Get<ButtonWidget>("DECLINE_BUTTON");
			var logout = widget.Get<ButtonWidget>("LOGOUT_BUTTON");

			login.IsVisible = register.IsVisible = () => !reviewingRecovery && controller.State is RankedClientState.SignedOut or RankedClientState.Error;
			confirmation.IsVisible = widget.Get("CONFIRM_PASSWORD_LABEL").IsVisible = () => registering && login.IsVisible();
			widget.Get<LabelWidget>("TITLE").GetText = () => FluentProvider.GetMessage(registering ? "label-ranked-registration-title" : "label-ranked-match-title");
			login.GetText = () => FluentProvider.GetMessage(registering ? "button-ranked-return-login" : "button-ranked-login");
			register.GetText = () => FluentProvider.GetMessage(registering ? "button-ranked-register-submit" : "button-ranked-register");
			var recovery = widget.Get<ScrollPanelWidget>("RECOVERY_CODES");
			recovery.IsVisible = () => reviewingRecovery;
			recovery.Get<LabelWidget>("CODE_TEXT").GetText = () => recoveryText ?? "";
			var continueButton = widget.Get<ButtonWidget>("RECOVERY_CONTINUE");
			continueButton.IsVisible = () => reviewingRecovery;
			continueButton.OnClick = () => { reviewingRecovery = false; recoveryText = null; statusText = null; ApplyAccountLayout(); };
			username.IsVisible = password.IsVisible = login.IsVisible;
			widget.Get<LabelWidget>("USERNAME_LABEL").IsVisible = login.IsVisible;
			widget.Get<LabelWidget>("PASSWORD_LABEL").IsVisible = login.IsVisible;
			var heading = widget.Get<LabelWidget>("MATCH_HEADING");
			heading.IsVisible = () => !reviewingRecovery && !login.IsVisible() && controller.State != RankedClientState.Restoring;
			heading.GetText = () => FluentProvider.GetMessage(controller.State switch
			{
				RankedClientState.Queued => "label-ranked-heading-queued",
				RankedClientState.Proposal => "label-ranked-heading-found",
				RankedClientState.Accepted => "label-ranked-heading-accepted",
				RankedClientState.Assigned => "label-ranked-heading-connecting",
				_ => "label-ranked-heading-ready",
			});
			widget.Get("MATCH_SIGNAL").IsVisible = () => controller.State is RankedClientState.Queued or RankedClientState.Accepted or RankedClientState.Assigned;
			var account = widget.Get<LabelWidget>("ACCOUNT");
			account.IsVisible = () => !reviewingRecovery && !login.IsVisible() && controller.State != RankedClientState.Restoring &&
				!(Platform.UsesMobileLayout && IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution).IsCompactPhone && controller.State != RankedClientState.Ready);
			account.GetText = () => FluentProvider.GetMessage("label-ranked-account", "username", controller.Username ?? "");
			login.IsDisabled = () => busy || (!registering && (username.Text.Trim().Length < 3 || password.Text.Length < 12));
			register.IsDisabled = () => busy;
			queue.IsVisible = () => !reviewingRecovery && controller.State == RankedClientState.Ready;
			queue.IsDisabled = () => busy;
			cancel.IsVisible = () => controller.State is RankedClientState.Queued or RankedClientState.Cooldown;
			cancel.IsDisabled = () => busy;
			accept.IsVisible = decline.IsVisible = () => controller.State == RankedClientState.Proposal;
			accept.IsDisabled = decline.IsDisabled = () => busy;
			logout.IsVisible = () => !reviewingRecovery && RankedPanelLayout.CanLogout(controller.State) &&
				(!(Platform.UsesMobileLayout && IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution).IsCompactPhone) ||
					controller.State == RankedClientState.Ready || !string.IsNullOrEmpty(controller.Username));
			logout.IsDisabled = () => busy || controller.State is RankedClientState.Assigned or RankedClientState.Accepted;

			login.OnClick = () => { if (registering) SetRegistration(false); else Authenticate(false); };
			register.OnClick = () => { Log.Write("debug", registering ? "Ranked: confirm registration tapped" : "Ranked: open registration tapped"); if (registering) Authenticate(true); else SetRegistration(true); };
			queue.OnClick = () => Begin(() => controller.JoinQueueAsync(CancellationToken.None));
			cancel.OnClick = () => Begin(() => controller.CancelAsync(CancellationToken.None));
			accept.OnClick = () => Begin(async () =>
			{
				await controller.AcceptAsync(CancellationToken.None).ConfigureAwait(false);
				TryConnectAssignment();
			});
			decline.OnClick = () => Begin(() => controller.DeclineAsync(CancellationToken.None));
			logout.OnClick = () =>
			{
				if (RankedPanelLayout.CanLogout(controller.State))
					Begin(() => controller.LogoutAsync(CancellationToken.None));
			};
			var back = widget.Get<ButtonWidget>("BACK_BUTTON");
			back.IsDisabled = () => !RankedPanelLayout.CanLeaveMatch(controller.State, busy);
			back.OnClick = () =>
			{
				if (!RankedPanelLayout.CanLeaveMatch(controller.State, busy))
					return;
				if (registering) { SetRegistration(false); return; }
				if (controller.State is RankedClientState.Queued or RankedClientState.Proposal or RankedClientState.Cooldown)
					Begin(async () =>
					{
						await controller.CancelAsync(CancellationToken.None).ConfigureAwait(false);
						Game.RunAfterTick(() =>
						{
							if (!disposed && controller.State == RankedClientState.Ready)
							{
								Ui.CloseWindow();
								onExit();
							}
						});
					});
				else
				{
					Ui.CloseWindow();
					onExit();
				}
			};

			username.OnEnterKey = _ => { password.TakeKeyboardFocus(); return true; };
			password.OnEnterKey = _ => { if (registering) confirmation.TakeKeyboardFocus(); else if (!login.IsDisabled()) login.OnClick(); return true; };
			confirmation.OnEnterKey = _ => { if (!busy) Authenticate(true); return true; };
			confirmation.OnTabKey = _ => username.TakeKeyboardFocus();
			username.OnTabKey = _ => password.TakeKeyboardFocus();
			password.OnTabKey = _ => { if (registering) confirmation.TakeKeyboardFocus(); else username.TakeKeyboardFocus(); return true; };
		}

		static string StatusFor(RankedClientState state) => FluentProvider.GetMessage(StatusMessage(state));

		static string StatusMessage(RankedClientState state) => state switch
		{
			RankedClientState.SignedOut => "label-ranked-status-signed-out",
			RankedClientState.Restoring => "label-ranked-status-restoring",
			RankedClientState.BindingDevice => "label-ranked-status-binding",
			RankedClientState.Ready => "label-ranked-status-ready",
			RankedClientState.Queued => "label-ranked-status-queued",
			RankedClientState.Proposal => "label-ranked-status-found",
			RankedClientState.Accepted => "label-ranked-status-waiting-opponent",
			RankedClientState.Assigned => "label-ranked-status-connecting",
			RankedClientState.Cooldown => "label-ranked-status-cooldown",
			_ => "label-ranked-status-error",
		};

		void SetRegistration(bool value)
		{
			if (busy) return;
			username.YieldKeyboardFocus();
			password.YieldKeyboardFocus();
			confirmation.YieldKeyboardFocus();
			password.Text = confirmation.Text = "";
			registering = value;
			statusText = null;
			ApplyAccountLayout();
		}

		void ApplyAccountLayout()
		{
			RankedPanelLayout.Apply(root);
			var content = root.Get("RANKED_CONTENT");
			var snapshot = IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution);
			if (registering)
				RankedRegistrationForm.ApplyLayout(content, Platform.UsesMobileLayout, snapshot);
			if (reviewingRecovery)
			{
				var w = content.Bounds.Width;
				var h = content.Bounds.Height;
				var phone = IosMenuLayoutPolicy.Create(Platform.UsesMobileLayout, snapshot).IsPhone;
				var left = w * (phone ? 46 : 54) / 100;
				var width = w * (phone ? 96 : 93) / 100 - left;
				content.Get("STATUS").Bounds = new WidgetBounds(left, h * 17 / 100, width, h * 17 / 100);
				content.Get("RECOVERY_CODES").Bounds = new WidgetBounds(left, h * 35 / 100, width, h * 35 / 100);
				var codes = content.Get<ScrollPanelWidget>("RECOVERY_CODES");
				var label = codes.Get<LabelWidget>("CODE_TEXT");
				label.Bounds = new WidgetBounds(12, 12, width - 48,
					Game.Renderer.Fonts[label.Font].Measure(recoveryText ?? "").Y + 24);
				codes.ContentHeight = label.Bounds.Bottom + 12;
				codes.ScrollToTop();
				content.Get("RECOVERY_CONTINUE").Bounds = new WidgetBounds(left, h * 75 / 100, width,
					Math.Max(IosMenuLayoutPolicy.Create(Platform.UsesMobileLayout, snapshot).MinimumTarget, h * 9 / 100));
			}
		}

		void Authenticate(bool create)
		{
			if (busy || (create && !registering)) return;
			if (create && RankedRegistrationForm.Validate(username.Text, password.Text, confirmation.Text) is string error)
			{ Log.Write("debug", $"Ranked: registration validation rejected: {error}"); statusText = FluentProvider.GetMessage(error); return; }
			username.YieldKeyboardFocus();
			password.YieldKeyboardFocus();
			confirmation.YieldKeyboardFocus();
			var enteredUsername = username.Text.Trim();
			var enteredPassword = password.Text;
			Begin(async () =>
			{
				if (create)
				{
					Log.Write("debug", "Ranked: registration operation started");
					var registered = await controller.RegisterAsync(
						enteredUsername, enteredPassword, CancellationToken.None).ConfigureAwait(false);
					Log.Write("debug", "Ranked: registration and device binding completed");
					Game.RunAfterTick(() =>
					{
						if (disposed) return;
						registering = false;
						reviewingRecovery = true;
						password.Text = confirmation.Text = "";
						recoveryText = string.Join("\n", registered.RecoveryCodes);
						statusText = null;
						ApplyAccountLayout();
					});
				}
				else
					await controller.LoginAsync(enteredUsername, enteredPassword, CancellationToken.None).ConfigureAwait(false);
			});
		}

		void Begin(Func<Task> operation)
		{
			if (busy || disposed)
				return;
			busy = true;
			statusText = null;
			_ = Task.Run(async () =>
			{
				try
				{
					await operation().ConfigureAwait(false);
				}
				catch (RankedLobbyException ex)
				{
					Log.Write("debug", $"Ranked: request failed: {ex.Error}, HTTP {(int?)ex.StatusCode}");
					statusText = FluentProvider.GetMessage(ex.Error == RankedLobbyError.RateLimited ? "label-ranked-error-rate-limit" :
						registering && !string.IsNullOrEmpty(controller.Username) ? "label-ranked-error-device-after-register" :
						registering && ex.StatusCode == System.Net.HttpStatusCode.Conflict ? "label-ranked-error-username-taken" :
						registering && (int?)ex.StatusCode == 422 ? "label-ranked-error-account-input" :
						ex.Error == RankedLobbyError.Unauthorized ? "label-ranked-error-credentials" : "label-ranked-error-service");
				}
				catch (Exception ex)
				{
					Log.Write("debug", $"Ranked: operation failed: {ex.GetType().FullName}\n{ex.StackTrace}");
					statusText = FluentProvider.GetMessage("label-ranked-status-error");
				}
				finally
				{
					busy = false;
				}
			});
		}

		void TryInitialize()
		{
			if (initializationStarted || busy || disposed)
				return;
			var profile = Game.LocalPlayerProfile;
			if (profile.State == LocalPlayerProfile.LinkState.Uninitialized)
			{
				profile.GenerateKeypair();
				statusText = FluentProvider.GetMessage("label-ranked-status-keygen");
				return;
			}
			if (profile.State == LocalPlayerProfile.LinkState.GeneratingKeys)
				return;

			initializationStarted = true;
			Begin(() => controller.RestoreAsync(CancellationToken.None));
		}

		void TryConnectAssignment()
		{
			if (controller.State != RankedClientState.Assigned || disposed)
				return;
			RankedMatchAssignment assignment;
			try
			{
				assignment = controller.TakeAssignment();
			}
			catch (InvalidOperationException)
			{
				return;
			}

			Game.RunAfterTick(() =>
			{
				if (disposed)
					return;
				Ui.CloseWindow();
				onAssigned(assignment);
			});
		}

		public override void Tick()
		{
			if (lastResolution != Game.Renderer.Resolution)
			{
				lastResolution = Game.Renderer.Resolution;
				ApplyAccountLayout();
			}

			TryInitialize();
			if (!busy && Game.RunTime >= nextPoll && controller.State is
				(RankedClientState.Queued or RankedClientState.Proposal or RankedClientState.Accepted))
			{
				nextPoll = Game.RunTime + 1000;
				Begin(async () =>
				{
					await controller.PollAsync(CancellationToken.None).ConfigureAwait(false);
					TryConnectAssignment();
				});
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !disposed)
			{
				disposed = true;
				client.Dispose();
			}
			base.Dispose(disposing);
		}
	}

	public sealed class RankedLeaderboardLogic : ChromeLogic
	{
		readonly Widget root;
		readonly RankedLobbyClient client;
		readonly ScrollPanelWidget list;
		readonly ScrollItemWidget template;
		readonly LabelWidget status;
		Size lastResolution;
		bool disposed;
		string statusText;

		[ObjectCreator.UseCtor]
		public RankedLeaderboardLogic(Widget widget, Action onExit)
		{
			root = widget;
			list = widget.Get<ScrollPanelWidget>("LEADERBOARD_LIST");
			template = list.Get<ScrollItemWidget>("PLAYER_TEMPLATE");
			list.RemoveChildren();
			status = widget.Get<LabelWidget>("STATUS");
			status.GetText = () => statusText ?? "";
			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () => { Ui.CloseWindow(); onExit(); };

			var http = HttpClientFactory.Create();
			http.Timeout = TimeSpan.FromSeconds(10);
			client = new RankedLobbyClient(http,
				RankedLobbyClient.ParseBaseUri(Game.Settings.Game.RankedLobbyUrl));
			statusText = FluentProvider.GetMessage("label-ranked-leaderboard-loading");
			_ = LoadAsync();
		}

		async Task LoadAsync()
		{
			try
			{
				var seasons = await client.GetSeasonsAsync(CancellationToken.None).ConfigureAwait(false);
				var season = seasons.Seasons.FirstOrDefault(item => item.Status == "active") ?? seasons.Seasons.FirstOrDefault();
				if (season == null)
				{
					statusText = FluentProvider.GetMessage("label-ranked-leaderboard-empty");
					return;
				}
				var leaderboard = await client.GetLeaderboardAsync(season.Id, 100, CancellationToken.None).ConfigureAwait(false);
				Game.RunAfterTick(() => Populate(leaderboard));
			}
			catch (Exception)
			{
				statusText = FluentProvider.GetMessage("label-ranked-error-service");
			}
		}

		void Populate(RankedLeaderboard leaderboard)
		{
			if (disposed)
				return;
			list.RemoveChildren();
			foreach (var player in leaderboard.Players)
			{
				var row = ScrollItemWidget.Setup(template, () => false, () => { });
				row.Get<LabelWidget>("RANK").GetText = () => player.Rank.ToString();
				row.Get<LabelWidget>("PLAYER").GetText = () => player.Username;
				row.Get<LabelWidget>("RATING").GetText = () => player.Rating.ToString();
				row.Get<LabelWidget>("RECORD").GetText = () => $"{player.Wins}-{player.Losses}";
				list.AddChild(row);
			}
			statusText = leaderboard.Players.Length == 0
				? FluentProvider.GetMessage("label-ranked-leaderboard-empty")
				: leaderboard.Season.Name;
			RankedPanelLayout.Apply(root);
		}

		public override void Tick()
		{
			if (lastResolution != Game.Renderer.Resolution)
			{
				lastResolution = Game.Renderer.Resolution;
				RankedPanelLayout.Apply(root);
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !disposed)
			{
				disposed = true;
				client.Dispose();
			}
			base.Dispose(disposing);
		}
	}

	public sealed class RankedResultLogic : ChromeLogic
	{
		readonly Widget root;
		readonly RankedLobbyClient client;
		readonly RankedSessionManager sessions;
		readonly string matchId;
		readonly LabelWidget result;
		readonly LabelWidget rating;
		readonly LabelWidget status;
		Size lastResolution;
		long nextPoll;
		bool busy;
		bool restored;
		bool complete;
		bool disposed;
		string resultText;
		string ratingText;
		string statusText;

		[ObjectCreator.UseCtor]
		public RankedResultLogic(Widget widget, string matchId, string opponent, Action onExit)
		{
			root = widget;
			this.matchId = Guid.TryParse(matchId, out _) ? matchId :
				throw new ArgumentException("Ranked result match id is invalid.", nameof(matchId));
			widget.Get<LabelWidget>("OPPONENT").GetText = () =>
				FluentProvider.GetMessage("label-ranked-result-opponent", "opponent", opponent ?? "");
			result = widget.Get<LabelWidget>("RESULT");
			rating = widget.Get<LabelWidget>("RATING");
			status = widget.Get<LabelWidget>("STATUS");
			result.GetText = () => resultText ?? FluentProvider.GetMessage("label-ranked-result-pending");
			rating.GetText = () => ratingText ?? "";
			status.GetText = () => statusText ?? FluentProvider.GetMessage("label-ranked-result-settling");
			widget.Get<ButtonWidget>("BACK_BUTTON").OnClick = () => { Ui.CloseWindow(); onExit(); };

			var http = HttpClientFactory.Create();
			http.Timeout = TimeSpan.FromSeconds(10);
			client = new RankedLobbyClient(http,
				RankedLobbyClient.ParseBaseUri(Game.Settings.Game.RankedLobbyUrl));
			sessions = new RankedSessionManager(client, RankedCredentialStoreFactory.Create());
		}

		async Task RefreshAsync()
		{
			if (busy || complete || disposed)
				return;
			busy = true;
			try
			{
				if (!restored)
				{
					restored = await sessions.RestoreAsync(CancellationToken.None).ConfigureAwait(false);
					if (!restored)
					{
						statusText = FluentProvider.GetMessage("label-ranked-result-sign-in");
						complete = true;
						return;
					}
				}

				var match = await client.GetPublicMatchAsync(matchId, CancellationToken.None).ConfigureAwait(false);
				if (match.State == "VOID")
				{
					resultText = FluentProvider.GetMessage("label-ranked-result-void");
					statusText = FluentProvider.GetMessage("label-ranked-result-void-detail");
					complete = true;
					return;
				}
				if (match.State != "SETTLED")
					return;

				var access = await sessions.GetAccessTokenAsync(CancellationToken.None).ConfigureAwait(false);
				var events = await client.GetRatingEventsAsync(access, 50, CancellationToken.None).ConfigureAwait(false);
				var ratingEvent = events.Events.FirstOrDefault(item =>
					StringComparer.Ordinal.Equals(item.MatchId, matchId));
				if (ratingEvent == null)
					return;

				resultText = FluentProvider.GetMessage(ratingEvent.Outcome == "win"
					? "label-ranked-result-win" : "label-ranked-result-loss");
				var delta = ratingEvent.RatingDelta.ToString("+0;-0;0", CultureInfo.InvariantCulture);
				ratingText = FluentProvider.GetMessage(
					"label-ranked-result-rating", "before", ratingEvent.RatingBefore,
					"after", ratingEvent.RatingAfter, "delta", delta);
				statusText = FluentProvider.GetMessage("label-ranked-result-complete");
				complete = true;
			}
			catch (RankedLobbyException ex) when (ex.Error == RankedLobbyError.Conflict)
			{
				statusText = FluentProvider.GetMessage("label-ranked-result-settling");
			}
			catch (RankedLobbyException ex) when (ex.Error is RankedLobbyError.ServiceUnavailable or RankedLobbyError.RateLimited)
			{
				statusText = FluentProvider.GetMessage("label-ranked-result-retrying");
			}
			catch (Exception)
			{
				statusText = FluentProvider.GetMessage("label-ranked-error-service");
				complete = true;
			}
			finally
			{
				busy = false;
			}
		}

		public override void Tick()
		{
			if (lastResolution != Game.Renderer.Resolution)
			{
				lastResolution = Game.Renderer.Resolution;
				RankedPanelLayout.Apply(root, 760, 560);
			}

			if (!busy && !complete && Game.RunTime >= nextPoll)
			{
				nextPoll = Game.RunTime + 2000;
				_ = Task.Run(RefreshAsync);
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !disposed)
			{
				disposed = true;
				client.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
