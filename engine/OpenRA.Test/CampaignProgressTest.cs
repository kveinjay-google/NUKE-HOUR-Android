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
using OpenRA.Mods.Common;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class CampaignProgressTest
	{
		[Test]
		public void FirstMissionIsUnlockedWithoutAPrerequisite()
		{
			Assert.That(CampaignProgress.IsUnlocked(null, new[] { "allied-01" }), Is.True);
			Assert.That(CampaignProgress.IsUnlocked("", System.Array.Empty<string>()), Is.True);
		}

		[Test]
		public void LaterMissionRequiresCompletedPredecessorCaseInsensitively()
		{
			Assert.That(CampaignProgress.IsUnlocked("allied-01", new[] { "ALLIED-01" }), Is.True);
			Assert.That(CampaignProgress.IsUnlocked("allied-01", new[] { "soviet-01" }), Is.False);
			Assert.That(CampaignProgress.IsCompleted("allied-01", null), Is.False);
		}

		[Test]
		public void AddingCompletionRejectsEmptyIdsAndDeduplicatesExistingProgress()
		{
			Assert.That(CampaignProgress.AddCompletion(new[] { "ALLIED-01" }, "allied-01"),
				Is.EqualTo(new[] { "ALLIED-01" }));
			Assert.That(CampaignProgress.AddCompletion(new[] { "allied-01" }, "soviet-01"),
				Is.EqualTo(new[] { "allied-01", "soviet-01" }));
			Assert.That(CampaignProgress.AddCompletion(new[] { "allied-01" }, "  "),
				Is.EqualTo(new[] { "allied-01" }));
		}
	}
}
