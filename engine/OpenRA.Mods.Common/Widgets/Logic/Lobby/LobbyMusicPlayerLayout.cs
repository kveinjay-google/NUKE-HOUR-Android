using System;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	// Coordinates inside the lobby music module. Control bounds are relative to
	// PlayerCard, while PlayerCard and TrackList are relative to the module.
	public sealed class LobbyMusicPlayerLayout
	{
		public Rectangle PlayerCard { get; }
		public Rectangle PlayerContent { get; }
		public Rectangle TrackList { get; }
		public Rectangle Mute { get; }
		public Rectangle Title { get; }
		public Rectangle Time { get; }
		public Rectangle Buttons { get; }
		public Rectangle Icon { get; }
		public Rectangle Shuffle { get; }
		public Rectangle Repeat { get; }
		public Rectangle VolumeLabel { get; }
		public Rectangle VolumeSlider { get; }
		public int Target { get; }
		public int Gap { get; }

		public LobbyMusicPlayerLayout(int width, int height, IosMenuLayoutPolicy policy)
		{
			width = Math.Max(1, width);
			height = Math.Max(1, height);
			Target = policy.MinimumTarget;
			Gap = policy.Gap;
			var minimumCardWidth = 4 * Target + 5 * Gap;
			var minimumListWidth = 5 * Target;
			var stacked = width < minimumCardWidth + minimumListWidth + 3 * Gap;
			var preferredCardWidth = (int)Math.Ceiling(440 * policy.LogicalPerPoint);
			var cardWidth = stacked ? Math.Max(1, width - 2 * Gap) :
				Math.Min(Math.Max(minimumCardWidth, preferredCardWidth), width - minimumListWidth - 3 * Gap);
			if (stacked)
			{
				var cardHeight = Math.Max(1, Math.Min(height - Target - Gap, height * 3 / 5));
				PlayerCard = new Rectangle(Gap, 0, cardWidth, cardHeight);
				TrackList = new Rectangle(Gap, cardHeight + Gap, cardWidth,
					Math.Max(0, height - cardHeight - Gap));
			}
			else
			{
				var columnWidth = Math.Max(cardWidth + 2 * Gap, width * 43 / 100);
				columnWidth = Math.Min(columnWidth, width - minimumListWidth - Gap);
				PlayerCard = new Rectangle((columnWidth - cardWidth) / 2, 0, cardWidth, height);
				TrackList = new Rectangle(columnWidth + Gap, 0, width - columnWidth - Gap, height);
			}

			var status = policy.MinimumReadableTextHeight;
			var contentHeight = 7 * Gap + 2 * status + 4 * Target;
			PlayerContent = new Rectangle(0, 0, cardWidth, Math.Max(contentHeight, PlayerCard.Height));
			var top = Math.Max(0, (PlayerCard.Height - contentHeight) / 2);
			Mute = new Rectangle(Gap, top + Gap, Math.Max(0, cardWidth - 2 * Gap), status);
			Title = new Rectangle(Gap, Mute.Bottom + Gap, Math.Max(0, cardWidth - 2 * Gap), Target);
			Time = new Rectangle(Gap, Title.Bottom + Gap, Math.Max(0, cardWidth - 2 * Gap), status);
			var buttonWidth = 4 * Target + 3 * Gap;
			Buttons = new Rectangle((cardWidth - buttonWidth) / 2, Time.Bottom + Gap, buttonWidth, Target);
			var iconSize = Math.Min(Target, Math.Max(16, Target / 2));
			if ((Target - iconSize) % 2 != 0)
				iconSize++;

			Icon = new Rectangle((Target - iconSize) / 2, (Target - iconSize) / 2, iconSize, iconSize);
			var toggleWidth = Math.Max(1, (cardWidth - 3 * Gap) / 2);
			Shuffle = new Rectangle(Gap, Buttons.Bottom + Gap, toggleWidth, Target);
			Repeat = new Rectangle(cardWidth - Gap - toggleWidth, Shuffle.Top, toggleWidth, Target);
			var volumeY = Shuffle.Bottom + Gap;
			var labelWidth = Math.Min(2 * Target, Math.Max(Target, cardWidth / 3));
			VolumeLabel = new Rectangle(Gap, volumeY, labelWidth, Target);
			VolumeSlider = new Rectangle(VolumeLabel.Right + Gap, volumeY,
				Math.Max(0, cardWidth - VolumeLabel.Right - 2 * Gap), Target);
		}

		public Rectangle Button(int index) => new(index * (Target + Gap), 0, Target, Target);
	}
}
