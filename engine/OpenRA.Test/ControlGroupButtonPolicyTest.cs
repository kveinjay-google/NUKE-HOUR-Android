#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 * For more information, see COPYING.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.RA2.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class ControlGroupButtonPolicyTest
	{
		[TearDown]
		public void ResetTouchModifierOverride()
		{
			TouchModifierOverride.Reset();
		}

		[Test]
		public void TouchCtrlToggleIsLatchedScopedAndResettable()
		{
			TouchModifierOverride.Reset();
			Assert.That(TouchModifierOverride.Enabled, Is.False);
			Assert.That(TouchModifierOverride.Apply(Modifiers.None, true), Is.EqualTo(Modifiers.None));

			TouchModifierOverride.Toggle();
			Assert.That(TouchModifierOverride.Enabled, Is.True);
			Assert.That(TouchModifierOverride.Apply(Modifiers.None, true), Is.EqualTo(Modifiers.Ctrl));
			Assert.That(TouchModifierOverride.Apply(Modifiers.Shift, true), Is.EqualTo(Modifiers.Ctrl | Modifiers.Shift));
			Assert.That(TouchModifierOverride.Apply(Modifiers.None, false), Is.EqualTo(Modifiers.None));

			TouchModifierOverride.Reset();
			Assert.That(TouchModifierOverride.Enabled, Is.False);
		}

		[TestCase(false, Modifiers.None, true, true, false, false, TouchControlGroupAction.UseDesktopRules)]
		[TestCase(true, Modifiers.Ctrl, true, true, false, false, TouchControlGroupAction.Create)]
		[TestCase(true, Modifiers.Ctrl, false, false, false, false, TouchControlGroupAction.Ignore)]
		[TestCase(true, Modifiers.Shift, true, true, false, false, TouchControlGroupAction.UseDesktopRules)]
		[TestCase(true, Modifiers.None, true, true, false, false, TouchControlGroupAction.Ignore)]
		[TestCase(true, Modifiers.None, true, false, false, false, TouchControlGroupAction.Ignore)]
		[TestCase(true, Modifiers.None, false, true, false, false, TouchControlGroupAction.Recall)]
		[TestCase(true, Modifiers.None, false, false, false, false, TouchControlGroupAction.Recall)]
		[TestCase(true, Modifiers.None, false, true, true, false, TouchControlGroupAction.Recall)]
		[TestCase(true, Modifiers.None, false, true, true, true, TouchControlGroupAction.Recall)]
		public void TouchTapRequiresExplicitCtrlToOverwriteAndOtherwiseOnlyRecalls(
			bool touchPlatform, Modifiers modifiers, bool groupEmpty, bool hasOwnedSelection,
			bool hasGroupedOwnedSelection, bool jump,
			TouchControlGroupAction expected)
		{
			Assert.That(ControlGroupButtonPolicy.Resolve(
				touchPlatform, modifiers, groupEmpty, hasOwnedSelection, hasGroupedOwnedSelection, jump),
				Is.EqualTo(expected));
		}
	}
}
