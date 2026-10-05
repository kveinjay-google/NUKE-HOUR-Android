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

using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailCampaignInstallerTest
	{
		[TestCase("maps01.mix")]
		[TestCase("MAPS02.MIX")]
		public void RecognizesOnlyOriginalCampaignSourceArchives(string name)
		{
			Assert.That(RetailCampaignInstaller.IsCampaignArchive(name), Is.True);
			Assert.That(RetailCampaignInstaller.IsCampaignArchive("multimd.mix"), Is.False);
		}

		[Test]
		public void CacheIdentityIncludesSourceHashAndCompatibilityRevision()
		{
			Assert.That(RetailCampaignInstaller.CacheIdentity("abcdef", 3),
				Is.EqualTo("abcdef-compat-3-bridges-v4"));
		}

		[Test]
		public void AlliedFirstMissionUsesTheActorPreservationCompatibilityRevision()
		{
			var mission = RetailCampaignCatalog.Missions.Single(candidate => candidate.Id == "allied-01");

			Assert.That(mission.CompatibilityRevision, Is.GreaterThanOrEqualTo(10));
		}
	}
}
