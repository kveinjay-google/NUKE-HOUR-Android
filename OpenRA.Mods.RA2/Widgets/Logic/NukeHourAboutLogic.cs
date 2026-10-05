#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available under the terms of the GNU General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or (at
 * your option) any later version. For more information, see COPYING.
 */
#endregion

using System;
using OpenRA.MobileUi;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA2.Widgets.Logic
{
	public sealed class NukeHourAboutLogic : ChromeLogic
	{
		[ObjectCreator.UseCtor]
		public NukeHourAboutLogic(Widget widget, Action onExit)
		{
			var panel = widget.Get("ABOUT_PANEL");
			panel.Get<ButtonWidget>("BACK_BUTTON").OnClick = () =>
			{
				Ui.CloseWindow();
				onExit();
			};

			if (!PhoneDialogScaler.IsActive)
				return;

			PhoneDialogScaler.Apply(panel);
			var safe = MobileUiService.Instance.LogicalSafeBounds;
			var parentOrigin = panel.Parent == null ? int2.Zero : panel.Parent.ChildOrigin;
			panel.Bounds.X = safe.X + (safe.Width - panel.Bounds.Width) / 2 - parentOrigin.X;
			panel.Bounds.Y = safe.Y + (safe.Height - panel.Bounds.Height) / 2 - parentOrigin.Y;
		}
	}
}
