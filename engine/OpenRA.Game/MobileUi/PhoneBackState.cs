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

namespace OpenRA.MobileUi
{
	/// <summary>
	/// Cross-thread bridge between the Android activity (UI thread) and the
	/// engine (game thread) for the hardware back button while a phone modal
	/// (e.g. the Mobile Selection Sheet) is open.
	///
	/// The engine marks <see cref="ModalOpen"/> while a modal owns the screen;
	/// the Android activity then turns the hardware back press into
	/// <see cref="Pressed"/> instead of finishing the app, and the engine's
	/// modal consumes it on its next tick and closes. Plain volatile fields
	/// are sufficient: each side writes one field and reads the other.
	/// </summary>
	public static class PhoneBackState
	{
		/// <summary>True while an engine modal (sheet/dialog) is open.</summary>
		public static volatile bool ModalOpen;

		/// <summary>Set by the Android activity when the user pressed Back.</summary>
		public static volatile bool Pressed;
	}
}
