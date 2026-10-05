#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.Primitives;

namespace OpenRA.Mods.RA2.Widgets
{
	public static class CommandBarCatalog
	{
		public sealed class Slot
		{
			public readonly string Id;
			public readonly int Width;
			public readonly bool DefaultVisible;

			public Slot(string id, int width, bool defaultVisible)
			{
				Id = id;
				Width = width;
				DefaultVisible = defaultVisible;
			}
		}

		public static readonly Slot[] All =
		{
			// Selection / groups
			new("CTRL_TOGGLE", 48, false),
			new("GROUP_01", 48, true),
			new("GROUP_02", 48, true),
			new("GROUP_03", 48, true),
			new("GROUP_04", 48, true),
			new("GROUP_05", 48, true),
			new("PRODUCTION_X5", 48, true),
			new("GROUP_06", 48, true),
			new("GROUP_07", 48, true),
			new("GROUP_08", 48, true),
			new("GROUP_09", 48, true),
			new("GROUP_10", 48, true),
			new("SELECT_ALL", 48, true),
			new("SELECT_BY_TYPE", 48, true),
			new("CYCLE_BASE", 48, false),
			new("TO_SELECTION", 48, false),
			new("TO_LAST_EVENT", 48, false),
			new("CYCLE_HARVESTERS", 48, false),
			new("REMOVE_FROM_GROUP", 48, false),

			// Unit commands
			new("ATTACK_MOVE", 48, true),
			new("FORCE_MOVE", 48, true),
			new("FORCE_ATTACK", 48, true),
			new("GUARD", 48, true),
			new("DEPLOY", 48, true),
			new("SCATTER", 48, true),
			new("STOP", 48, true),
			new("QUEUE_ORDERS", 48, true),

			// Stances
			new("STANCE_ATTACKANYTHING", 48, true),
			new("STANCE_DEFEND", 48, true),
			new("STANCE_RETURNFIRE", 48, true),
			new("STANCE_HOLDFIRE", 48, true),

			// Orders
			new("SELL", 48, true),
			new("REPAIR", 48, true),
			new("AUTO_REPAIR", 48, true),
			new("BEACON", 48, true),
		};

		public static readonly HashSet<string> AllIds = new(All.Select(s => s.Id));

		public static IEnumerable<string> DefaultVisibleIds()
		{
			return All.Where(s => s.DefaultVisible).Select(s => s.Id);
		}

		public static Slot Get(string id)
		{
			return All.First(s => s.Id == id);
		}
	}

	public static class CommandBarLayoutPolicy
	{
		public const int CurrentVersion = 8;
		const int ProductionMultiplierMigrationVersion = 3;
		const int TouchCtrlMigrationVersion = 5;
		const int RepairSellMigrationVersion = 6;
		const int UtilityActionsMigrationVersion = 7;

		static readonly string[] RequiredTouchIds =
		{
			"CTRL_TOGGLE", "STOP", "CYCLE_BASE", "REPAIR", "AUTO_REPAIR", "SELL", "BEACON"
		};

		static readonly HashSet<string> TouchSuppressedIds = new()
		{
			"GROUP_06", "GROUP_07", "GROUP_08", "GROUP_09", "GROUP_10",
			"SELECT_BY_TYPE", "FORCE_ATTACK", "DEPLOY",
			"FORCE_MOVE", "GUARD", "STANCE_ATTACKANYTHING", "STANCE_DEFEND", "STANCE_RETURNFIRE"
		};

		public static bool IsRequiredTouchId(string id, bool touchLayout) =>
			touchLayout && RequiredTouchIds.Contains(id);

		public static bool CanHide(string id, bool touchLayout) =>
			!IsRequiredTouchId(id, touchLayout);

		public static IEnumerable<string> AvailableIds(IEnumerable<string> ids, bool touchLayout)
		{
			return ids.Where(id => CommandBarCatalog.AllIds.Contains(id) &&
				(touchLayout ? !TouchSuppressedIds.Contains(id) :
					id != "PRODUCTION_X5" && id != "CTRL_TOGGLE"));
		}

		public static IEnumerable<string> NormalizeVisibleOrder(
			IEnumerable<string> ids, bool touchLayout, int savedVersion)
		{
			var normalized = AvailableIds(ids, touchLayout).Distinct().ToList();
			if (touchLayout && savedVersion < ProductionMultiplierMigrationVersion &&
				!normalized.Contains("PRODUCTION_X5"))
			{
				var groupFive = normalized.IndexOf("GROUP_05");
				normalized.Insert(groupFive < 0 ? normalized.Count : groupFive + 1, "PRODUCTION_X5");
			}

			if (touchLayout && savedVersion < TouchCtrlMigrationVersion)
				normalized.Remove("STANCE_HOLDFIRE");

			if (savedVersion < RepairSellMigrationVersion)
			{
				if (touchLayout)
				{
					// Keep the one-row phone rail at its established capacity: repair and
					// sell replace two optional combat shortcuts that remain available in edit mode.
					normalized.Remove("ATTACK_MOVE");
					normalized.Remove("SCATTER");
				}

				var stop = normalized.IndexOf("STOP");
				if (!normalized.Contains("REPAIR"))
					normalized.Insert(stop < 0 ? normalized.Count : stop, "REPAIR");
				stop = normalized.IndexOf("STOP");
				if (!normalized.Contains("SELL"))
					normalized.Insert(stop < 0 ? normalized.Count : stop, "SELL");
			}

			if (savedVersion < UtilityActionsMigrationVersion)
			{
				if (touchLayout)
				{
					// Keep the expanded phone rail within the existing safe-width budget.
					// These remain available from edit mode and keep their desktop defaults.
					normalized.Remove("SELECT_ALL");
					normalized.Remove("QUEUE_ORDERS");
				}

				var stop = normalized.IndexOf("STOP");
				if (!normalized.Contains("AUTO_REPAIR"))
					normalized.Insert(stop < 0 ? normalized.Count : stop, "AUTO_REPAIR");
				stop = normalized.IndexOf("STOP");
				if (!normalized.Contains("BEACON"))
					normalized.Insert(stop < 0 ? normalized.Count : stop, "BEACON");
			}

			if (touchLayout && savedVersion < 8 && !normalized.Contains("GROUP_05"))
			{
				var fourth = normalized.IndexOf("GROUP_04");
				normalized.Insert(fourth < 0 ? 0 : fourth + 1, "GROUP_05");
			}

			if (touchLayout)
			{
				normalized.Remove("CTRL_TOGGLE");
				normalized.Insert(0, "CTRL_TOGGLE");
				foreach (var id in RequiredTouchIds)
					if (!normalized.Contains(id))
						normalized.Add(id);
				normalized.Remove("CYCLE_BASE");
				normalized.Add("CYCLE_BASE");
			}

			return normalized;
		}
	}

	public readonly struct TouchCommandBarSlotLayout
	{
		public readonly Size ButtonSize;
		public readonly Rectangle IconBounds;
		public readonly Rectangle LabelBounds;
		public readonly string Font;
		public readonly bool ShowLabel;

		public TouchCommandBarSlotLayout(
			Size buttonSize, Rectangle iconBounds, Rectangle labelBounds,
			string font, bool showLabel)
		{
			ButtonSize = buttonSize;
			IconBounds = iconBounds;
			LabelBounds = labelBounds;
			Font = font;
			ShowLabel = showLabel;
		}
	}

	public static class TouchCommandBarSlotPolicy
	{
		public static int FitRowButtonSize(int width, int count, int gap, int desired) =>
			Math.Max(1, Math.Min(desired, (width - gap * Math.Max(0, count - 1)) / Math.Max(1, count)));

		public static int ButtonPoints(IosScreenSnapshot snapshot, bool expanded) =>
			snapshot.IsCompactPhone ? expanded ? 46 : 50 : 56;

		public static int IconPoints(IosScreenSnapshot snapshot, bool expanded) =>
			snapshot.IsCompactPhone ? expanded ? 34 : 40 : 42;

		public static int Metric(IosScreenSnapshot snapshot, int points) =>
			snapshot.LogicalPoints(points);

		public static TouchCommandBarSlotLayout Create(
			IosScreenSnapshot snapshot, string id, bool expanded)
		{
			// Renderer pixels are much smaller than UIKit points on Retina phones.
			// Resolve the complete visual slot in physical points so its visible bounds
			// and hit target remain comfortable for a finger on every device class.
			var buttonSize = Metric(snapshot, ButtonPoints(snapshot, expanded));
			const bool showLabel = false;
			var iconSize = Metric(snapshot, IconPoints(snapshot, expanded));
			var icon = showLabel ? Rectangle.Empty : new Rectangle(
				(buttonSize - iconSize) / 2,
				Math.Max(0, (buttonSize - iconSize) / 2),
				iconSize,
				iconSize);
			return new TouchCommandBarSlotLayout(
				new Size(buttonSize, buttonSize), icon,
				showLabel ? new Rectangle(0, 0, buttonSize, buttonSize) : Rectangle.Empty,
				"TinyBold", showLabel);
		}

		public static Rectangle PlaceBar(IosScreenSnapshot snapshot, Size size)
		{
			return PlaceBar(snapshot, size, snapshot.SafeBounds);
		}

		public static Rectangle PlaceBar(IosScreenSnapshot snapshot, Size size, Rectangle availableBounds)
		{
			var safe = Rectangle.Intersect(snapshot.SafeBounds, availableBounds);
			if (safe.Width <= 0 || safe.Height <= 0)
				safe = snapshot.SafeBounds;

			var margin = snapshot.LogicalPoints(12);
			return new Rectangle(
				safe.Left + Math.Max(0, (safe.Width - size.Width) / 2),
				Math.Max(safe.Top, safe.Bottom - margin - size.Height),
				Math.Min(size.Width, safe.Width),
				Math.Min(size.Height, safe.Height));
		}
	}

	public sealed class CommandBarPrefs
	{
		public List<string> VisibleOrder = new();
		public bool Expanded = false;
		public int Version = CommandBarLayoutPolicy.CurrentVersion;
	}

	public static class CommandBarPreferences
	{
		const string FileName = "ra2-commandbar.yaml";

		static string FilePath => Path.Combine(Platform.SupportDir, FileName);

		public static CommandBarPrefs Load()
		{
			var prefs = new CommandBarPrefs
			{
				VisibleOrder = CommandBarCatalog.DefaultVisibleIds().ToList(),
				Expanded = false,
				Version = 1
			};

			try
			{
				if (!File.Exists(FilePath))
					return prefs;

				prefs.Version = 1;

				var root = MiniYaml.FromFile(FilePath);
				var version = root.FirstOrDefault(n => n.Key == "Version");
				if (version != null && int.TryParse(version.Value.Value, out var savedVersion))
					prefs.Version = savedVersion;

				var visible = root.FirstOrDefault(n => n.Key == "Visible");
				if (visible != null && !string.IsNullOrWhiteSpace(visible.Value.Value))
				{
					var ids = visible.Value.Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(s => s.Trim())
						.Where(id => CommandBarCatalog.AllIds.Contains(id))
						.Distinct()
						.ToList();
					if (ids.Count > 0)
						prefs.VisibleOrder = ids;
				}

				var expanded = root.FirstOrDefault(n => n.Key == "Expanded");
				if (expanded != null && bool.TryParse(expanded.Value.Value, out var exp))
					prefs.Expanded = exp;
			}
			catch (Exception e)
			{
				Log.Write("debug", $"Failed to load command bar preferences: {e}");
			}

			return prefs;
		}

		public static List<string> LoadVisibleOrder()
		{
			return Load().VisibleOrder;
		}

		public static void SaveVisibleOrder(IEnumerable<string> visibleOrder)
		{
			Save(visibleOrder, Load().Expanded);
		}

		public static void Save(IEnumerable<string> visibleOrder, bool expanded)
		{
			try
			{
				var ids = visibleOrder.Where(id => CommandBarCatalog.AllIds.Contains(id)).Distinct().ToList();
				var yaml = $"Version: {CommandBarLayoutPolicy.CurrentVersion}\n" +
					$"Visible: {string.Join(", ", ids)}\nExpanded: {expanded}\n";
				File.WriteAllText(FilePath, yaml);
			}
			catch (Exception e)
			{
				Log.Write("debug", $"Failed to save command bar preferences: {e}");
			}
		}
	}
}
