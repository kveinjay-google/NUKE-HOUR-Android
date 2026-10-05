using System;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public static class MultiplayerScreenLayout
	{
		public static bool SupportsDesktop(string id, bool hasContent)
		{
			return hasContent && id is "MULTIPLAYER_PANEL" or "MULTIPLAYER_CREATESERVER_PANEL" or "DIRECTCONNECT_PANEL";
		}

		public static WidgetBounds ContentBounds(IosScreenSnapshot snapshot, int maximumWidth = 0, int maximumHeight = 0,
			bool compactPhone = false)
		{
			var safe = snapshot.SafeBounds;
			if (compactPhone && IosMenuLayoutPolicy.Create(true, snapshot).IsPhone)
			{
				var border = snapshot.LogicalPoints(2);
				return new WidgetBounds(safe.Left + border, safe.Top + border,
					Math.Max(1, safe.Width - 2 * border), Math.Max(1, safe.Height - 2 * border));
			}
			var insetX = (int)Math.Ceiling(snapshot.EffectiveSize.Width * .04);
			var verticalInset = snapshot.NativePointSize.Height >= 600 ? .06 : .04;
			var insetY = (int)Math.Ceiling(snapshot.EffectiveSize.Height * verticalInset);
			var bottomInset = insetY + (snapshot.NativePointSize.Height >= 600 ? snapshot.LogicalPoints(24) : 0);
			var left = Math.Max(safe.Left, insetX);
			var top = Math.Max(safe.Top, insetY);
			var right = Math.Min(safe.Right, snapshot.EffectiveSize.Width - insetX);
			var bottom = Math.Min(safe.Bottom, snapshot.EffectiveSize.Height - bottomInset);
			var width = Math.Max(1, right - left);
			var height = Math.Max(1, bottom - top);
			if (maximumWidth > 0)
				width = Math.Min(width, snapshot.LogicalPoints(maximumWidth));
			if (maximumHeight > 0)
				height = Math.Min(height, snapshot.LogicalPoints(maximumHeight));
			return new WidgetBounds(left + (right - left - width) / 2,
				top + (bottom - top - height) / 2, width, height);
		}

		public static WidgetBounds FilterPanelBounds(IosScreenSnapshot snapshot, int preferredWidth, int rowCount)
		{
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var width = Math.Min(snapshot.SafeBounds.Width, Math.Max(preferredWidth, snapshot.LogicalPoints(420)));
			var height = Math.Min(snapshot.SafeBounds.Height,
				Math.Max(0, rowCount) * (policy.MinimumTarget + policy.Gap) + policy.Gap);
			return new WidgetBounds(0, 0, width, height);
		}

		public static WidgetBounds FilterRowBounds(int width, int index, IosMenuLayoutPolicy policy)
		{
			return new WidgetBounds(policy.Gap, policy.Gap + index * (policy.MinimumTarget + policy.Gap),
				Math.Max(1, width - 2 * policy.Gap), policy.MinimumTarget);
		}
	}
}
