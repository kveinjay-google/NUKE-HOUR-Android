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
	public sealed class RetailCampaignCatalogTest
	{
		[Test]
		public void CatalogDefinesBothCompleteOriginalCampaignsInOrder()
		{
			Assert.That(RetailCampaignCatalog.Missions.Count, Is.EqualTo(24));
			Assert.That(RetailCampaignCatalog.Missions.Take(12).Select(m => m.Id),
				Is.EqualTo(Enumerable.Range(1, 12).Select(i => $"allied-{i:00}")));
			Assert.That(RetailCampaignCatalog.Missions.Skip(12).Select(m => m.Id),
				Is.EqualTo(Enumerable.Range(1, 12).Select(i => $"soviet-{i:00}")));
		}

		[Test]
		public void SourceResolutionIsCaseInsensitiveAndUnambiguous()
		{
			var allied = RetailCampaignCatalog.Resolve("MAPS01.MIX", "ALL01T.MAP");
			var soviet = RetailCampaignCatalog.Resolve("maps02.mix", "sov12s.map");

			Assert.That(allied?.Id, Is.EqualTo("allied-01"));
			Assert.That(soviet?.Id, Is.EqualTo("soviet-12"));
			Assert.That(RetailCampaignCatalog.Resolve("maps02.mix", "all01t.map"), Is.Null);
			Assert.That(RetailCampaignCatalog.Missions.SelectMany(m => m.SourceNames)
				.GroupBy(n => n, System.StringComparer.OrdinalIgnoreCase)
				.Any(g => g.Count() > 1), Is.False);
		}

		[Test]
		public void CatalogProvidesNamedMissionsAndIndependentFactionProgression()
		{
			var allied = RetailCampaignCatalog.Missions.Take(12).ToArray();
			var soviet = RetailCampaignCatalog.Missions.Skip(12).ToArray();

			Assert.That(allied.Select(m => m.BrowserTitle),
				Is.EqualTo(Enumerable.Range(1, 12).Select(i => $"mission-allied-{i:00}-title")));
			Assert.That(soviet.Select(m => m.BrowserTitle),
				Is.EqualTo(Enumerable.Range(1, 12).Select(i => $"mission-soviet-{i:00}-title")));
			Assert.That(RetailCampaignCatalog.Missions.Select(m => m.CompatibilityRevision), Is.All.EqualTo(10));
			Assert.That(allied[0].UnlockPrerequisite, Is.Null.Or.Empty);
			Assert.That(soviet[0].UnlockPrerequisite, Is.Null.Or.Empty);
			for (var i = 1; i < 12; i++)
			{
				Assert.That(allied[i].UnlockPrerequisite, Is.EqualTo(allied[i - 1].Id));
				Assert.That(soviet[i].UnlockPrerequisite, Is.EqualTo(soviet[i - 1].Id));
			}
		}
	}
}
