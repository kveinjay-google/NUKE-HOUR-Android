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

using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets.Logic
{
	/// <summary>
	/// Presents campaign-only mission messages in the dedicated upper HUD area.
	/// Other notification pools remain owned by the normal chat and transient displays.
	/// </summary>
	public sealed class RetailCampaignMissionNotificationsLogic : ChromeLogic, INotificationHandler<TextNotification>
	{
		readonly TextNotificationsDisplayWidget display;
		TextNotification lastLine;
		int repetitions;

		[ObjectCreator.UseCtor]
		public RetailCampaignMissionNotificationsLogic(Widget widget)
		{
			display = widget.Get<TextNotificationsDisplayWidget>("MISSION_NOTIFICATIONS_DISPLAY");
		}

		void INotificationHandler<TextNotification>.Handle(TextNotification notification)
		{
			if (notification.Pool != TextNotificationPool.Mission)
				return;

			var lineToDisplay = notification;
			if (display.Children.Count > 0 && notification.CanIncrementOnDuplicate() && notification == lastLine)
			{
				repetitions++;
				lineToDisplay = new TextNotification(
					notification.Pool,
					notification.ClientId,
					notification.Prefix,
					$"{notification.Text} ({repetitions + 1})",
					notification.PrefixColor,
					notification.TextColor);

				display.RemoveMostRecentNotification();
			}
			else
				repetitions = 0;

			lastLine = notification;
			display.AddNotification(lineToDisplay);
		}
	}
}
