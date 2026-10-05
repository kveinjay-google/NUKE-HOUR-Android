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

using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class MissionBrowserPresentationPolicyTest
	{
		[Test]
		public void FixedRuleMissionsHideCustomization()
		{
			Assert.That(MissionBrowserPresentationPolicy.ShowCustomOptions(fixedRules: true), Is.False);
			Assert.That(MissionBrowserPresentationPolicy.ShowCustomOptions(fixedRules: false), Is.True);
		}

		[Test]
		public void FixedRuleMissionsAlwaysUseMissionDefaults()
		{
			var allowed = new[] { "easy", "normal", "hard" };

			Assert.That(MissionBrowserPresentationPolicy.ResolveOptionValue(
				fixedRules: true, defaultValue: "normal", rememberedValue: "hard", allowed), Is.EqualTo("normal"));
			Assert.That(MissionBrowserPresentationPolicy.ResolveOptionValue(
				fixedRules: false, defaultValue: "normal", rememberedValue: "hard", allowed), Is.EqualTo("hard"));
			Assert.That(MissionBrowserPresentationPolicy.ResolveOptionValue(
				fixedRules: false, defaultValue: "normal", rememberedValue: "invalid", allowed), Is.EqualTo("normal"));
		}
	}
}
