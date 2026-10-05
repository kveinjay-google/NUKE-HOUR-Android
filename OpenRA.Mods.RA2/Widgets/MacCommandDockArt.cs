using System;
using System.Collections.Generic;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets
{
	// All icon and shell artwork comes from generated PNGs. No vector icon fallback.
	sealed class MacCommandDockArt
	{
		readonly World world;
		string skin;
		Sprite[] frame;
		readonly Dictionary<string, Sprite> icons = new();
		readonly Dictionary<string, Sprite> activeIcons = new();
		static readonly string[] Names =
		{
			"ATTACK_MOVE", "STOP", "DEPLOY", "SCATTER", "GUARD",
			"REPAIR", "SELL", "MORE", "CLOSE", "GROUP",
			"FORCE_MOVE", "FORCE_ATTACK", "QUEUE_ORDERS", "STANCE_ATTACKANYTHING", "STANCE_DEFEND",
			"STANCE_RETURNFIRE", "STANCE_HOLDFIRE", "BEACON", "SELECT_ALL", "SELECT_BY_TYPE",
			"CYCLE_BASE", "TO_SELECTION", "TO_LAST_EVENT", "CYCLE_HARVESTERS", "REMOVE_FROM_GROUP"
		};

		public Color Accent { get; private set; }
		public MacCommandDockArt(World world) { this.world = world; }

		public void Refresh()
		{
			// Resolve only: never change the mobile skin's global state.
			var next = TouchFactionSkin.ResolveForPlayer(world?.LocalPlayer?.Faction.InternalName, world?.LocalPlayer == null);
			if (skin == next) return;
			skin = next;
			Accent = skin == "soviets" ? Color.FromArgb(241, 128, 106) :
				skin == "yuri" ? Color.FromArgb(205, 158, 246) : Color.FromArgb(137, 190, 242);
			var sheet = ChromeProvider.GetImage("mac-dock-icons-" + skin, "atlas");
			var activeSheet = ChromeProvider.GetImage("mac-dock-icons-" + skin + "-active", "atlas");
			for (var i = 0; i < Names.Length; i++)
			{
				var x = sheet.Bounds.X + sheet.Bounds.Width * (i % 5) / 5;
				var y = sheet.Bounds.Y + sheet.Bounds.Height * (i / 5) / 5;
				var right = sheet.Bounds.X + sheet.Bounds.Width * (i % 5 + 1) / 5;
				var bottom = sheet.Bounds.Y + sheet.Bounds.Height * (i / 5 + 1) / 5;
				icons[Names[i]] = new Sprite(sheet.Sheet, new Rectangle(x + 2, y + 2, right - x - 4, bottom - y - 4), sheet.Channel);
				activeIcons[Names[i]] = new Sprite(activeSheet.Sheet,
					new Rectangle(activeSheet.Bounds.X + x - sheet.Bounds.X + 2, activeSheet.Bounds.Y + y - sheet.Bounds.Y + 2,
						right - x - 4, bottom - y - 4), activeSheet.Channel);
			}

			icons["AUTO_REPAIR"] = ChromeProvider.GetImage("mac-dock-auto-repair-" + skin, "normal");
			activeIcons["AUTO_REPAIR"] = ChromeProvider.GetImage("mac-dock-auto-repair-" + skin + "-active", "normal");

			var panel = ChromeProvider.GetImage("mac-dock-panel-" + skin, "panel");
			var b = panel.Bounds;
			const int edge = 64;
			var xs = new[] { b.Left, b.Left + edge, b.Right - edge, b.Right };
			var ys = new[] { b.Top, b.Top + edge, b.Bottom - edge, b.Bottom };
			frame = new Sprite[9];
			for (var y = 0; y < 3; y++)
				for (var x = 0; x < 3; x++)
					frame[y * 3 + x] = new Sprite(panel.Sheet,
						new Rectangle(xs[x], ys[y], xs[x + 1] - xs[x], ys[y + 1] - ys[y]), panel.Channel);
		}

		public void DrawPanel(Rectangle b)
		{
			const int edge = 8;
			var xs = new[] { b.Left, b.Left + edge, b.Right - edge, b.Right };
			var ys = new[] { b.Top, b.Top + edge, b.Bottom - edge, b.Bottom };
			for (var y = 0; y < 3; y++)
				for (var x = 0; x < 3; x++)
					WidgetUtils.DrawSprite(frame[y * 3 + x], new float2(xs[x], ys[y]),
						new float2(xs[x + 1] - xs[x], ys[y + 1] - ys[y]));
		}

		public void DrawIcon(string id, Rectangle rect, bool disabled, bool activated = false)
		{
			var name = id.StartsWith("GROUP_", StringComparison.Ordinal) ? "GROUP" : id;
			var icon = activated && !disabled ? activeIcons[name] : icons[name];
			WidgetUtils.DrawSprite(icon, new float2(rect.X, rect.Y), new float2(rect.Width, rect.Height));
			if (activated && !disabled) ButtonActivationOverlayWidget.DrawLight(rect, skin);
			if (disabled) WidgetUtils.FillRectWithColor(rect, Color.FromArgb(150, 0, 0, 0));
		}
	}
}
