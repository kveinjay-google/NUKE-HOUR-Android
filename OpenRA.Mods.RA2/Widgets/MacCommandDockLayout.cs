using System;
using System.Linq;
using OpenRA.Primitives;

namespace OpenRA.Mods.RA2.Widgets
{
	public sealed class MacCommandDockLayout
	{
		public static readonly string[] Primary = { "ATTACK_MOVE", "STOP", "DEPLOY", "SCATTER", "GUARD", "REPAIR", "SELL" };
		public static readonly string[][] Pages =
		{
			Enumerable.Range(1, 10).Select(i => $"GROUP_{i:D2}").ToArray(),
			new[] { "FORCE_MOVE", "FORCE_ATTACK", "QUEUE_ORDERS", "STANCE_ATTACKANYTHING", "STANCE_DEFEND",
				"STANCE_RETURNFIRE", "STANCE_HOLDFIRE", "AUTO_REPAIR", "BEACON" },
			new[] { "SELECT_ALL", "SELECT_BY_TYPE", "CYCLE_BASE", "TO_SELECTION", "TO_LAST_EVENT", "CYCLE_HARVESTERS", "REMOVE_FROM_GROUP" }
		};

		public static bool Enabled(PlatformType platform) => platform == PlatformType.OSX;
		public Rectangle Bounds { get; }
		public int Cell { get; }
		public int MainY { get; }
		public int Columns { get; }
		public const int Padding = 10;
		public const int Gap = 4;
		public const int RowHeight = 58;

		public MacCommandDockLayout(Rectangle available, bool open, int page)
		{
			// Eight slots including More; never expand across the production sidebar.
			Cell = Math.Clamp((available.Width - 2 * Padding - 7 * Gap) / 8, 24, 60);
			var width = Math.Min(available.Width, 2 * Padding + Cell * 8 + 7 * Gap);
			Columns = Math.Max(1, (width - 2 * Padding + Gap) / (Cell + Gap));
			var rows = open ? (Pages[page].Length + Columns - 1) / Columns : 0;
			MainY = Padding + (open ? 32 + rows * (RowHeight + Gap) + 8 : 0);
			var height = MainY + RowHeight + Padding;
			Bounds = new Rectangle(available.X + Math.Max(0, (available.Width - width) / 2),
				Math.Max(available.Top, available.Bottom - height - 12), width, height);
		}

		public Rectangle PrimarySlot(int index) => new(Padding + index * (Cell + Gap), MainY, Cell, RowHeight);
		public Rectangle DrawerSlot(int index) => new(Padding + index % Columns * (Cell + Gap),
			Padding + 32 + index / Columns * (RowHeight + Gap), Cell, RowHeight);
	}
}
