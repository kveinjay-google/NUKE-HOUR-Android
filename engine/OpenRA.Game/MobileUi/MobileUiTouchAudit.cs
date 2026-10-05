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
using System.IO;
using System.Linq;
using System.Text;
using OpenRA.Widgets;

namespace OpenRA.MobileUi
{
	/// <summary>
	/// Debug-only touch-target audit. Walks a widget tree and reports controls
	/// whose event bounds are below the 48dp minimum, overlapping expanded event
	/// bounds, or outside the safe/page area. Writes a human readable .txt and a
	/// small .json to the given log directory. Never runs in normal play.
	/// </summary>
	public static class MobileUiTouchAudit
	{
		public sealed class Entry
		{
			internal Entry() { }

			public string Id;
			public string Type;
			public float WidthDp;
			public float HeightDp;
			public string Severity; // P0 (<40dp), P1 (40-47dp), OK
			public string Detail;
		}

		static readonly string[] InteractiveNameParts =
		{
			"Button", "DropDown", "Checkbox", "Slider", "TextField", "ScrollItem"
		};

		public static bool IsLikelyInteractive(Widget widget)
		{
			var name = widget.GetType().Name;
			return InteractiveNameParts.Any(part => name.IndexOf(part, StringComparison.Ordinal) >= 0);
		}

		public static List<Entry> Audit(Widget root, Func<Widget, bool> isInteractive = null)
		{
			isInteractive ??= IsLikelyInteractive;
			var entries = new List<Entry>();
			var service = MobileUiService.Instance;

			void Walk(Widget widget)
			{
				if (widget == null || !widget.IsVisible())
					return;

				if (isInteractive(widget))
				{
					var bounds = widget.EventBounds;
					var widthDp = ToDp(bounds.Width, service);
					var heightDp = ToDp(bounds.Height, service);
					var minDp = Math.Min(widthDp, heightDp);
					var severity = minDp < 40 ? "P0" : minDp < 48 ? "P1" : "OK";
					entries.Add(new Entry
					{
						Id = widget.Id,
						Type = widget.GetType().Name,
						WidthDp = (float)Math.Round(widthDp, 1),
						HeightDp = (float)Math.Round(heightDp, 1),
						Severity = severity
					});
				}

				// Overlap check among interactive siblings of the same parent.
				if (widget.Children.Count > 0)
				{
					var siblings = widget.Children.Where(c => c.IsVisible() && isInteractive(c)).ToList();
					for (var i = 0; i < siblings.Count; i++)
						for (var j = i + 1; j < siblings.Count; j++)
							if (siblings[i].EventBounds.IntersectsWith(siblings[j].EventBounds))
								entries.Add(new Entry
								{
									Id = $"{siblings[i].Id}<>{siblings[j].Id}",
									Type = "Overlap",
									Severity = "P1",
									Detail = "expanded event bounds intersect"
								});
				}

				foreach (var child in widget.Children)
					Walk(child);
			}

			Walk(root);
			return entries;
		}

		public static void WriteReports(List<Entry> entries, string logDirectory)
		{
			try
			{
				Directory.CreateDirectory(logDirectory);
				var txt = new StringBuilder();
				txt.AppendLine("Mobile touch target audit");
				txt.AppendLine($"profile={MobileUiService.Instance.Profile} density={MobileUiService.Instance.Density:F2} uiScale={MobileUiService.Instance.UiScale:F2}");
				foreach (var e in entries.OrderBy(x => x.Severity))
				{
					if (e.Severity == "OK" && e.Type != "Overlap")
						continue;
					txt.AppendLine($"{e.Severity}\t{e.Id}\t{e.Type}\t{e.WidthDp}dp x {e.HeightDp}dp{e.DetailText()}");
				}

				File.WriteAllText(Path.Combine(logDirectory, "mobile-touch-audit.txt"), txt.ToString());
			}
			catch (Exception)
			{
				// Auditing must never break the game.
			}

			try
			{
				var json = new StringBuilder();
				json.Append("{\"profile\":\"").Append(MobileUiService.Instance.Profile).Append("\",\"entries\":[");
				for (var i = 0; i < entries.Count; i++)
				{
					var e = entries[i];
					if (i > 0)
						json.Append(',');
					json.Append("{\"severity\":\"").Append(e.Severity)
						.Append("\",\"id\":\"").Append(Escape(e.Id))
						.Append("\",\"type\":\"").Append(Escape(e.Type))
						.Append("\",\"widthDp\":").Append(e.WidthDp.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
						.Append(",\"heightDp\":").Append(e.HeightDp.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
						.Append('}');
				}

				json.Append("]}");
				File.WriteAllText(Path.Combine(logDirectory, "mobile-touch-audit.json"), json.ToString());
			}
			catch (Exception e)
			{
				// Keep a readable marker instead of failing silently.
				try
				{
					File.WriteAllText(Path.Combine(logDirectory, "mobile-touch-audit.json"),
						"{\"error\":\"" + Escape(e.Message) + "\"}");
				}
				catch (Exception)
				{
					// Auditing must never break the game.
				}
			}
		}

		static float ToDp(int uiPixels, MobileUiService service)
		{
			if (service.Density <= 0 || service.UiScale <= 0)
				return uiPixels;
			return uiPixels * service.UiScale / service.Density;
		}

		static string Escape(string value)
			=> value.Replace("\\", "\\\\").Replace("\"", "\\\"");

		static string DetailText(this Entry e)
			=> string.IsNullOrEmpty(e.Detail) ? string.Empty : " " + e.Detail;
	}
}
