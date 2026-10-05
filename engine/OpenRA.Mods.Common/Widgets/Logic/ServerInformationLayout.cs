using System;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public readonly struct ServerInformationLayout
	{
		public readonly Rectangle Dialog;
		public readonly Rectangle Title;
		public readonly Rectangle Content;
		public readonly Rectangle Close;

		public ServerInformationLayout(IosScreenSnapshot screen)
		{
			var safe = screen.SafeBounds;
			var gap = screen.LogicalPoints(24);
			var height = Math.Max(1, Math.Min(screen.LogicalPoints(480), safe.Height - 2 * gap));
			var width = Math.Max(1, Math.Min(screen.LogicalPoints(680), Math.Min(safe.Width - 2 * gap, height * 3 / 2)));
			Dialog = new Rectangle(safe.X + (safe.Width - width) / 2, safe.Y + (safe.Height - height) / 2, width, height);
			// The artwork reserves 0–17% for its header and 77–100% for its footer.
			var inset = Math.Max(1, width * 7 / 100);
			Title = new Rectangle(inset, height * 5 / 100, Math.Max(1, width - 2 * inset), Math.Max(1, height * 10 / 100));
			Content = new Rectangle(inset, height * 20 / 100, Math.Max(1, width - 2 * inset), Math.Max(1, height * 53 / 100));
			var buttonWidth = Math.Min(screen.LogicalPoints(180), width - 2 * inset);
			var buttonHeight = Math.Min(screen.LogicalPoints(44), height * 16 / 100);
			Close = new Rectangle((width - buttonWidth) / 2, height * 80 / 100, Math.Max(1, buttonWidth), Math.Max(1, buttonHeight));
		}
	}
}
