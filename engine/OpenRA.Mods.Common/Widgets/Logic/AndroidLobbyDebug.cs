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

#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using OpenRA.MobileUi;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	/// <summary>
	/// Debug-only tooling for mapping and auditing the real RA2 SERVER_LOBBY
	/// widget tree on Android phones:
	///   - one-shot widget tree dump (json + txt) into the support dir;
	///   - a debug overlay that labels containers and paints VisualBounds /
	///     EventBounds / undersized targets / overlaps / the safe area.
	/// Everything here is compiled out of Release builds and gated to Android
	/// phone touch mode so desktop, iOS and tablet behaviour never changes.
	/// The dump/overlay are enabled by placing a flag file in the support dir:
	///   lobby-widget-dump.flag      -> dump the tree once per lobby session
	///   lobby-debug-overlay.flag    -> show the overlay while in the lobby
	/// </summary>
	public static class AndroidLobbyDebug
	{
		public const string DumpFlagFile = "lobby-widget-dump.flag";
		public const string OverlayFlagFile = "lobby-debug-overlay.flag";
		public const string DumpBaseName = "server-lobby-widget-tree";
		public const string MainMenuPromptDumpFlagFile = "mainmenu-prompt-dump.flag";
		public const string MainMenuPromptDumpBaseName = "mainmenu-introduction-prompt";

		public static bool PhoneLobbyActive
		{
			get
			{
				var service = MobileUiService.Instance;
				return Platform.IsAndroid && service.IsEnabledMobile && PhoneInput.PhoneTouchMode;
			}
		}

		public static bool DumpRequested()
		{
			return File.Exists(Path.Combine(Platform.SupportDir, DumpFlagFile));
		}

		public static bool OverlayRequested()
		{
			return File.Exists(Path.Combine(Platform.SupportDir, OverlayFlagFile));
		}

		public static bool MainMenuPromptDumpRequested()
		{
			return File.Exists(Path.Combine(Platform.SupportDir, MainMenuPromptDumpFlagFile));
		}

		/// <summary>
		/// Injects a real Down/Up mouse (touch) pair through Ui.HandleInput at
		/// UI logical coordinates. Debug/autotap driver only.
		/// </summary>
		public static void SimulateTap(int x, int y)
		{
			var pos = new int2(x, y);
			Ui.HandleInput(new MouseInput(MouseInputEvent.Down, MouseButton.Left, pos, int2.Zero, Modifiers.None, 0));
			Ui.HandleInput(new MouseInput(MouseInputEvent.Up, MouseButton.Left, pos, int2.Zero, Modifiers.None, 0));
		}

		/// <summary>Attaches the debug overlay as the topmost child of the lobby.</summary>
		public static void AttachOverlay(Widget lobby)
		{
			if (lobby == null || lobby.GetOrNull("__ANDROID_LOBBY_DEBUG_OVERLAY__") != null)
				return;

			lobby.AddChild(new AndroidLobbyDebugOverlay(lobby));
		}

		/// <summary>
		/// One-shot dump of the lobby tree. Writes json + txt into
		/// Platform.SupportDir (app-private on Android; pulled to
		/// artifacts/android-lobby/ via run-as).
		/// </summary>
		public static void WriteWidgetTree(Widget root)
		{
			WriteWidgetTreeCore(root, DumpBaseName);
		}

		/// <summary>Dumps any subtree under its own base name (Debug builds).</summary>
		public static void WriteWidgetTree(Widget root, string baseName)
		{
			WriteWidgetTreeCore(root, baseName);
		}

		/// <summary>
		/// One-shot dump of the main menu introduction prompt tree into
		/// Platform.SupportDir (mirrors <see cref="WriteWidgetTree"/>).
		/// </summary>
		public static void WriteMainMenuPromptWidgetTree(Widget root)
		{
			WriteWidgetTreeCore(root, MainMenuPromptDumpBaseName);
		}

		static void WriteWidgetTreeCore(Widget root, string baseName)
		{
			try
			{
				if (root == null)
					return;

				var service = MobileUiService.Instance;
				var nodes = new List<Node>();
				Collect(root, null, nodes, 0);

				var context = new StringBuilder();
				context.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
				context.AppendLine("Window (logical): " + Game.Renderer.Resolution.Width + "x" + Game.Renderer.Resolution.Height);
				context.AppendLine("Window native: " + Game.Renderer.NativeResolution.Width + "x" + Game.Renderer.NativeResolution.Height
					+ " nativeScale=" + Game.Renderer.NativeWindowScale.ToString("0.###", CultureInfo.InvariantCulture)
					+ " effectiveScale=" + Game.Renderer.WindowScale.ToString("0.###", CultureInfo.InvariantCulture)
					+ " settingsUIScale=" + Game.Settings.Graphics.UIScale.ToString("0.###", CultureInfo.InvariantCulture));
				context.AppendLine("Layout profile: " + service.Profile);
				context.AppendLine("Pixel: " + service.PixelWidth + "x" + service.PixelHeight
					+ " drawable: " + service.DrawableWidth + "x" + service.DrawableHeight
					+ " logical: " + service.LogicalWidth + "x" + service.LogicalHeight);
				context.AppendLine("Density: " + service.Density.ToString("0.###", CultureInfo.InvariantCulture)
					+ " FontScale: " + service.FontScale.ToString("0.###", CultureInfo.InvariantCulture)
					+ " UiScale: " + service.UiScale.ToString("0.###", CultureInfo.InvariantCulture)
					+ " UI size: " + service.UISize);
				context.AppendLine("Usable (dp): " + service.UsableWidthDp.ToString("0.#", CultureInfo.InvariantCulture)
					+ "x" + service.UsableHeightDp.ToString("0.#", CultureInfo.InvariantCulture));
				context.AppendLine("Safe insets (dp) left/top/right/bottom: "
					+ service.InsetLeftDp.ToString("0.#", CultureInfo.InvariantCulture) + "/"
					+ service.InsetTopDp.ToString("0.#", CultureInfo.InvariantCulture) + "/"
					+ service.InsetRightDp.ToString("0.#", CultureInfo.InvariantCulture) + "/"
					+ service.InsetBottomDp.ToString("0.#", CultureInfo.InvariantCulture));
				context.AppendLine("LogicalSafeBounds: " + service.LogicalSafeBounds);
				context.AppendLine("1dp -> " + service.DpToUi(1).ToString("0.###", CultureInfo.InvariantCulture) + " ui px");
				context.AppendLine("1sp -> " + service.SpToUi(1).ToString("0.###", CultureInfo.InvariantCulture) + " ui px");
				context.AppendLine("Minimum touch target: " + service.Tokens.MinimumTouchTargetDp + "dp -> "
					+ service.DpToUi(service.Tokens.MinimumTouchTargetDp).ToString("0.###", CultureInfo.InvariantCulture) + " ui px");
				context.AppendLine("Rows: " + nodes.Count);
				context.AppendLine();

				var text = new StringBuilder();
				text.AppendLine("Android widget tree dump (" + baseName + ")");
				text.Append(context.ToString());
				foreach (var n in nodes)
				{
					for (var i = 0; i < n.Depth; i++)
						text.Append("  ");
					text.AppendLine(FormatNodeText(n));
				}

				var json = new StringBuilder();
				json.AppendLine("{");
				json.AppendLine("  \"context\": {");
				foreach (var line in ContextJsonLines(service))
					json.AppendLine("    " + line + ",");
				json.AppendLine("    \"rows\": " + nodes.Count);
				json.AppendLine("  },");
				json.AppendLine("  \"root\": \"" + JsonEscape(root.Id ?? "<root>") + "\",");
				json.AppendLine("  \"nodes\": [");
				for (var i = 0; i < nodes.Count; i++)
				{
					var n = nodes[i];
					json.Append("    {");
					json.Append("\"index\": " + n.Index);
					json.Append(", \"id\": \"" + JsonEscape(n.Id ?? "") + "\"");
					json.Append(", \"type\": \"" + JsonEscape(n.Type) + "\"");
					json.Append(", \"parent\": " + FormatNullable(n.ParentIndex));
					json.Append(", \"order\": " + n.Order);
					json.Append(", \"depth\": " + n.Depth);
					json.Append(", \"bounds\": " + BoundsJson(n.Bounds));
					json.Append(", \"visual\": " + BoundsJson(n.Visual));
					json.Append(", \"event\": " + BoundsJson(n.Event));
					json.Append(", \"visibleField\": " + Bool(n.VisibleField));
					json.Append(", \"visible\": " + Bool(n.Visible));
					json.Append(", \"disabled\": " + (n.Disabled == null ? "null" : Bool(n.Disabled.Value)));
					json.Append(", \"isScrollPanel\": " + Bool(n.IsScrollPanel));
					json.Append(", \"scrollContentHeight\": " + n.ScrollContentHeight);
					json.Append(", \"scrollListOffset\": " + n.ScrollListOffset);
					json.Append(", \"isDropDownButton\": " + Bool(n.IsDropDownButton));
					json.Append(", \"isScrollItem\": " + Bool(n.IsScrollItem));
					json.AppendLine("}" + (i == nodes.Count - 1 ? "" : ","));
				}

				json.AppendLine("  ]");
				json.AppendLine("}");

				var dir = Platform.SupportDir;
				var jsonPath = Path.Combine(dir, baseName + ".json");
				var textPath = Path.Combine(dir, baseName + ".txt");
				File.WriteAllText(jsonPath, json.ToString());
				File.WriteAllText(textPath, text.ToString());
				Log.Write("debug", "AndroidLobbyDebug: wrote " + jsonPath + " and " + textPath);
			}
			catch (Exception e)
			{
				Log.Write("debug", "AndroidLobbyDebug: dump failed: " + e);
			}
		}

		static string FormatNodeText(Node n)
		{
			var id = string.IsNullOrEmpty(n.Id) ? "<unnamed>" : n.Id;
			return $"{id} [{n.Type}] order={n.Order} bounds=({n.Bounds.X},{n.Bounds.Y} {n.Bounds.Width}x{n.Bounds.Height}) " +
				$"visual=({n.Visual.X},{n.Visual.Y} {n.Visual.Width}x{n.Visual.Height}) " +
				$"event=({n.Event.X},{n.Event.Y} {n.Event.Width}x{n.Event.Height}) " +
				$"visibleField={n.VisibleField} visible={n.Visible} disabled={FormatNullableBool(n.Disabled)}" +
				(n.IsScrollPanel ? $" scrollPanel(content={n.ScrollContentHeight},offset={n.ScrollListOffset})" : "") +
				(n.IsDropDownButton ? " dropDown" : "") +
				(n.IsScrollItem ? " scrollItem" : "");
		}

		static IEnumerable<string> ContextJsonLines(MobileUiService service)
		{
			yield return $"\"window\": \"{Game.Renderer.Resolution.Width}x{Game.Renderer.Resolution.Height}\"";
			yield return $"\"profile\": \"{service.Profile}\"";
			yield return $"\"pixel\": \"{service.PixelWidth}x{service.PixelHeight}\"";
			yield return $"\"drawable\": \"{service.DrawableWidth}x{service.DrawableHeight}\"";
			yield return $"\"logical\": \"{service.LogicalWidth}x{service.LogicalHeight}\"";
			yield return $"\"density\": {service.Density.ToString("0.###", CultureInfo.InvariantCulture)}";
			yield return $"\"fontScale\": {service.FontScale.ToString("0.###", CultureInfo.InvariantCulture)}";
			yield return $"\"uiScale\": {service.UiScale.ToString("0.###", CultureInfo.InvariantCulture)}";
			yield return $"\"uiSize\": \"{service.UISize}\"";
			yield return $"\"usableDp\": \"{service.UsableWidthDp.ToString("0.#", CultureInfo.InvariantCulture)}x{service.UsableHeightDp.ToString("0.#", CultureInfo.InvariantCulture)}\"";
			yield return $"\"safeInsetsDp\": \"{service.InsetLeftDp.ToString("0.#", CultureInfo.InvariantCulture)},{service.InsetTopDp.ToString("0.#", CultureInfo.InvariantCulture)},{service.InsetRightDp.ToString("0.#", CultureInfo.InvariantCulture)},{service.InsetBottomDp.ToString("0.#", CultureInfo.InvariantCulture)}\"";
			yield return $"\"logicalSafeBounds\": \"{service.LogicalSafeBounds}\"";
			yield return $"\"dpToUi\": {service.DpToUi(1).ToString("0.###", CultureInfo.InvariantCulture)}";
			yield return $"\"spToUi\": {service.SpToUi(1).ToString("0.###", CultureInfo.InvariantCulture)}";
			yield return $"\"minimumTouchTargetUi\": {service.DpToUi(service.Tokens.MinimumTouchTargetDp).ToString("0.###", CultureInfo.InvariantCulture)}";
		}

		static string BoundsJson(Rectangle r)
		{
			return "{" + $"\"x\": {r.X}, \"y\": {r.Y}, \"width\": {r.Width}, \"height\": {r.Height}" + "}";
		}

		static string FormatNullable(int? value)
		{
			return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null";
		}

		static string FormatNullableBool(bool? value)
		{
			return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null";
		}

		static string Bool(bool value)
		{
			return value ? "true" : "false";
		}

		static string JsonEscape(string value)
		{
			if (string.IsNullOrEmpty(value))
				return value ?? "";

			var sb = new StringBuilder(value.Length + 8);
			foreach (var c in value)
			{
				switch (c)
				{
					case '"': sb.Append("\\\""); break;
					case '\\': sb.Append("\\\\"); break;
					case '\n': sb.Append("\\n"); break;
					case '\r': sb.Append("\\r"); break;
					case '\t': sb.Append("\\t"); break;
					default: sb.Append(c); break;
				}
			}

			return sb.ToString();
		}

		static void Collect(Widget widget, Node parent, List<Node> nodes, int order)
		{
			if (widget == null)
				return;

			Rectangle VisualBounds(Widget w)
			{
				try
				{
					return w.RenderBounds;
				}
				catch
				{
					return default;
				}
			}

			Rectangle EventBounds(Widget w)
			{
				try
				{
					return w.EventBounds;
				}
				catch
				{
					return default;
				}
			}

			var node = new Node
			{
				Index = nodes.Count,
				Id = widget.Id,
				Type = widget.GetType().Name,
				ParentIndex = parent?.Index,
				Order = order,
				Depth = parent == null ? 0 : parent.Depth + 1,
				Bounds = new Rectangle(widget.Bounds.X, widget.Bounds.Y, widget.Bounds.Width, widget.Bounds.Height),
				Visual = VisualBounds(widget),
				Event = EventBounds(widget),
				VisibleField = widget.Visible
			};

			try
			{
				node.Visible = widget.IsVisible();
			}
			catch
			{
				node.Visible = widget.Visible;
			}

			if (widget is InputWidget input)
			{
				try
				{
					node.Disabled = input.IsDisabled();
				}
				catch
				{
					node.Disabled = null;
				}
			}

			if (widget is ScrollPanelWidget scroll)
			{
				node.IsScrollPanel = true;
				node.ScrollContentHeight = scroll.ContentHeight;
				node.ScrollListOffset = scroll.ListOffset;
			}

			node.IsDropDownButton = widget is DropDownButtonWidget;
			node.IsScrollItem = widget is ScrollItemWidget;
			nodes.Add(node);

			for (var i = 0; i < widget.Children.Count; i++)
				Collect(widget.Children[i], node, nodes, i);
		}

		sealed class Node
		{
			public int Index;
			public string Id;
			public string Type;
			public int? ParentIndex;
			public int Order;
			public int Depth;
			public Rectangle Bounds;
			public Rectangle Visual;
			public Rectangle Event;
			public bool VisibleField;
			public bool Visible;
			public bool? Disabled;
			public bool IsScrollPanel;
			public int ScrollContentHeight;
			public int ScrollListOffset;
			public bool IsDropDownButton;
			public bool IsScrollItem;
		}
	}

	/// <summary>
	/// Debug overlay that renders on top of the lobby: container id labels,
	/// blue VisualBounds, green EventBounds, red for touch targets smaller than
	/// the mobile minimum, orange for overlapping EventBounds of siblings and a
	/// translucent purple wash outside the logical safe area.
	/// </summary>
	public sealed class AndroidLobbyDebugOverlay : Widget
	{
		static readonly Color VisualColor = Color.FromArgb(255, 30, 120, 255);
		static readonly Color EventColor = Color.FromArgb(255, 40, 200, 40);
		static readonly Color SmallTargetColor = Color.FromArgb(255, 255, 40, 40);
		static readonly Color OverlapColor = Color.FromArgb(255, 255, 150, 0);
		static readonly Color SafeColor = Color.FromArgb(60, 190, 80, 255);

		readonly Widget root;

		public AndroidLobbyDebugOverlay(Widget root)
		{
			Id = "__ANDROID_LOBBY_DEBUG_OVERLAY__";
			this.root = root;
			Bounds = new WidgetBounds(0, 0, Game.Renderer.Resolution.Width, Game.Renderer.Resolution.Height);
			Visible = true;
			IgnoreMouseOver = true;
			IgnoreChildMouseOver = true;
		}

		public override Widget Clone()
		{
			throw new InvalidOperationException("Debug overlay is not cloneable");
		}

		public override bool EventBoundsContains(int2 location)
		{
			// The overlay is purely visual and never participates in input routing.
			return false;
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			return false;
		}

		public override void Draw()
		{
			var service = MobileUiService.Instance;
			if (root == null || !service.IsEnabledMobile)
				return;

			var resolution = new Rectangle(0, 0, Game.Renderer.Resolution.Width, Game.Renderer.Resolution.Height);
			var minimumTarget = Math.Max(1, service.RoundDpToUi(service.Tokens.MinimumTouchTargetDp));

			// Safe area wash + border. The mobile service reports the safe area
			// in render-canvas pixels (it adapts to the actual window scale).
			var safe = service.LogicalSafeBounds;
			if (!safe.IsEmpty)
			{
				if (safe.Left > resolution.Left)
					WidgetUtils.FillRectWithColor(new Rectangle(resolution.Left, resolution.Top, safe.Left - resolution.Left, resolution.Height), SafeColor);
				if (safe.Top > resolution.Top)
					WidgetUtils.FillRectWithColor(new Rectangle(resolution.Left, resolution.Top, resolution.Width, safe.Top - resolution.Top), SafeColor);
				if (resolution.Right > safe.Right)
					WidgetUtils.FillRectWithColor(new Rectangle(safe.Right, resolution.Top, resolution.Right - safe.Right, resolution.Height), SafeColor);
				if (resolution.Bottom > safe.Bottom)
					WidgetUtils.FillRectWithColor(new Rectangle(resolution.Left, safe.Bottom, resolution.Width, resolution.Bottom - safe.Bottom), SafeColor);

				DrawRect(safe, SafeColor, 1);
			}

			// Tree of visible widgets; skip the overlay itself and any invisible branches.
			foreach (var child in root.Children)
				if (child != this && child.IsVisible())
					DrawNode(child, 0, minimumTarget);
		}

		void DrawNode(Widget widget, int depth, int minimumTarget)
		{
			var visual = SafeVisual(widget);
			var eventBounds = SafeEvent(widget);
			var interactive = IsInteractive(widget);

			var highlight = HighlightKind.None;
			if (interactive && (eventBounds.Width < minimumTarget || eventBounds.Height < minimumTarget))
				highlight = HighlightKind.SmallTarget;

			// Overlap detection only for interactive siblings of the same parent.
			if (interactive && widget.Parent != null)
			{
				foreach (var sibling in widget.Parent.Children)
				{
					if (sibling == widget || !sibling.IsVisible() || !IsInteractive(sibling))
						continue;

					var other = SafeEvent(sibling);
					if (other.Width <= 0 || other.Height <= 0 || eventBounds.Width <= 0 || eventBounds.Height <= 0)
						continue;

					if (eventBounds.IntersectsWith(other))
					{
						highlight = HighlightKind.Overlap;
						break;
					}
				}
			}

			if (visual.Width > 0 && visual.Height > 0)
				DrawRect(visual, highlight == HighlightKind.SmallTarget ? SmallTargetColor : VisualColor,
					highlight == HighlightKind.SmallTarget ? 2 : 1);

			if (interactive && eventBounds.Width > 0 && eventBounds.Height > 0)
			{
				var color = highlight == HighlightKind.Overlap ? OverlapColor : highlight == HighlightKind.SmallTarget ? SmallTargetColor : EventColor;
				DrawRect(eventBounds, color, highlight == HighlightKind.None ? 1 : 2);
			}

			// Label containers (with children) and interactive controls; skip deep non-interactive noise.
			var showLabel = interactive || (widget.Children.Count > 0 && depth <= 3);
			if (showLabel && !string.IsNullOrEmpty(widget.Id) && widget.Id != Id)
				DrawLabel(widget.Id, visual, interactive);

			foreach (var child in widget.Children)
				if (child.IsVisible())
					DrawNode(child, depth + 1, minimumTarget);
		}

		void DrawLabel(string id, Rectangle visual, bool interactive)
		{
			if (visual.Width <= 0 || visual.Height <= 0)
				return;

			var font = Game.Renderer.Fonts.TryGetValue(interactive ? "TinyBold" : "Tiny", out var f) ? f : null;
			if (font == null)
				return;

			var label = id.Length > 48 ? id[..48] : id;
			var size = font.Measure(label);
			var x = visual.Left + 1;
			var y = visual.Top - size.Y - 1;
			if (y < 0)
				y = Math.Max(0, visual.Top + 1);

			if (y + size.Y > Game.Renderer.Resolution.Height)
				y = Math.Max(0, Game.Renderer.Resolution.Height - size.Y);

			x = Math.Max(0, Math.Min(x, Game.Renderer.Resolution.Width - size.X));
			var backing = new Rectangle(x - 1, y - 1, size.X + 2, size.Y + 2);
			WidgetUtils.FillRectWithColor(backing, Color.FromArgb(200, 0, 0, 0));
			font.DrawText(label, new float2(x, y), Color.White);
		}

		static bool IsInteractive(Widget widget)
		{
			return widget is ButtonWidget
				|| widget is DropDownButtonWidget
				|| widget is ScrollItemWidget
				|| widget is TextFieldWidget
				|| widget is CheckboxWidget
				|| widget is SliderWidget;
		}

		static Rectangle SafeVisual(Widget widget)
		{
			try
			{
				return widget.RenderBounds;
			}
			catch
			{
				return default;
			}
		}

		static Rectangle SafeEvent(Widget widget)
		{
			try
			{
				return widget.EventBounds;
			}
			catch
			{
				return default;
			}
		}

		static void DrawRect(Rectangle r, Color color, int width)
		{
			var tl = new float3(r.Left, r.Top, 0);
			var br = new float3(r.Right, r.Bottom, 0);
			Game.Renderer.RgbaColorRenderer.DrawRect(tl, br, width, color);
		}

		enum HighlightKind
		{
			None,
			SmallTarget,
			Overlap
		}
	}
}
#endif
