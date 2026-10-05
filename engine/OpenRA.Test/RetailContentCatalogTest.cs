#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class RetailContentCatalogTest
	{
		readonly RetailContentCatalog catalog = new();

		[Test]
		public void EmptyDirectoryReportsEveryContentGroupMissing()
		{
			var status = catalog.Inspect(System.Array.Empty<string>());

			Assert.That(status.Base, Is.EqualTo(ContentState.Missing));
			Assert.That(status.Yuri, Is.EqualTo(ContentState.Missing));
			Assert.That(status.Audio, Is.EqualTo(ContentState.Missing));
			Assert.That(status.Maps, Is.EqualTo(ContentState.Missing));
			Assert.That(status.CanLaunchBaseGame, Is.False);
			Assert.That(status.CanLaunchYuriGame, Is.False);
		}

		[Test]
		public void BaseGameRequirementsAreCaseInsensitive()
		{
			var status = catalog.Inspect(new[] { "disc/RA2.MIX", "Language.mix" });

			Assert.That(status.Base, Is.EqualTo(ContentState.Ready));
			Assert.That(status.CanLaunchBaseGame, Is.True);
			Assert.That(status.CanEnterGame, Is.False);
			Assert.That(status.Yuri, Is.EqualTo(ContentState.Missing));
		}

		[Test]
		public void LanguageArchivesProvideAudioWithoutLooseBagFiles()
		{
			var baseOnly = catalog.Inspect(new[] { "ra2.mix", "language.mix" });
			var yuri = catalog.Inspect(new[] { "ra2.mix", "language.mix", "ra2md.mix", "langmd.mix" });

			Assert.That(baseOnly.Audio, Is.EqualTo(ContentState.Ready));
			Assert.That(baseOnly.CanLaunchBaseGame, Is.True);
			Assert.That(yuri.Audio, Is.EqualTo(ContentState.Ready));
		}

		[Test]
		public void YuriRequiresBothExpansionArchives()
		{
			var incomplete = catalog.Inspect(new[] { "ra2.mix", "language.mix", "ra2md.mix" });
			var ready = catalog.Inspect(new[] { "ra2.mix", "language.mix", "ra2md.mix", "langmd.mix" });

			Assert.That(incomplete.Yuri, Is.EqualTo(ContentState.Incomplete));
			Assert.That(incomplete.CanLaunchYuriGame, Is.False);
			Assert.That(ready.Yuri, Is.EqualTo(ContentState.Ready));
			Assert.That(ready.CanLaunchYuriGame, Is.True);
		}

		[Test]
		public void StatusEnumeratesEveryRecognizedRetailArchiveAndItsImportState()
		{
			var steamFiles = new[]
			{
				"RA2.MIX", "language.mix", "ra2md.mix", "langmd.mix", "MULTI.MIX", "multimd.mix",
				"MAPS01.MIX", "MAPS02.MIX", "mapsmd03.mix", "MOVIES01.MIX", "MOVIES02.MIX",
				"movmd03.mix", "THEME.MIX", "thememd.mix", "WDT.MIX",
			};
			var status = catalog.Inspect(steamFiles);
			var filesProperty = typeof(RetailContentStatus).GetProperty("Files");

			Assert.That(filesProperty, Is.Not.Null,
				"The import UI needs a per-file status model instead of category summaries.");
			var files = ((IEnumerable)filesProperty!.GetValue(status)!).Cast<object>().ToArray();
			var names = files.Select(file => (string)file.GetType().GetProperty("FileName")!.GetValue(file)!).ToArray();
			var imported = files.ToDictionary(
				file => (string)file.GetType().GetProperty("FileName")!.GetValue(file)!,
				file => (bool)file.GetType().GetProperty("Imported")!.GetValue(file)!);

			Assert.That(names, Is.EqualTo(new[]
			{
				"ra2.mix", "language.mix", "ra2md.mix", "langmd.mix", "multi.mix", "multimd.mix",
				"maps01.mix", "maps02.mix", "mapsmd03.mix",
				"movies01.mix", "movies02.mix", "movmd03.mix",
				"theme.mix", "thememd.mix", "wdt.mix",
			}));
			Assert.That(imported.Values, Is.All.True);
		}

		[Test]
		public void ConvertibleLegacyMapsAreReportedReadyForRuntimeImport()
		{
			var status = catalog.Inspect(new[] { "ra2.mix", "language.mix", "Maps/Custom.MPR" });

			Assert.That(status.Maps, Is.EqualTo(ContentState.Ready));
			Assert.That(status.CanLaunchBaseGame, Is.True);
			Assert.That(status.CanEnterGame, Is.True);
		}

		[Test]
		public void OpenRaMapPackagesAreReportedReady()
		{
			var status = catalog.Inspect(new[] { "ra2.mix", "language.mix", "Maps/Custom.oramap" });

			Assert.That(status.Maps, Is.EqualTo(ContentState.Ready));
			Assert.That(status.CanEnterGame, Is.True);
		}

		[Test]
		public void GeneratedStarterMapDoesNotMaskMissingRetailMaps()
		{
			var status = catalog.Inspect(new[]
			{
				"ra2.mix", "language.mix", "maps/nukehour-starter-v1.oramap",
			});

			Assert.That(status.Maps, Is.EqualTo(ContentState.Missing));
			Assert.That(status.CanEnterGame, Is.False);
		}

		[TestCase("multi.mix")]
		[TestCase("MULTIMD.MIX")]
		public void RetailMultiplayerMapArchivesAreReportedReady(string archive)
		{
			var status = catalog.Inspect(new[] { "ra2.mix", "language.mix", archive });

			Assert.That(status.Maps, Is.EqualTo(ContentState.Ready));
			Assert.That(status.CanEnterGame, Is.True);
		}

		[TestCase("maps01.mix")]
		[TestCase("maps02.mix")]
		[TestCase("mapsmd03.mix")]
		public void CampaignArchivesDoNotPretendToProvideSkirmishMaps(string archive)
		{
			var status = catalog.Inspect(new[] { "ra2.mix", "language.mix", archive });

			Assert.That(status.Maps, Is.EqualTo(ContentState.Missing));
			Assert.That(status.CanEnterGame, Is.False);
		}

		[Test]
		public void CampaignArchivesHaveAnIndependentReadinessState()
		{
			var status = catalog.Inspect(new[]
			{
				"ra2.mix", "language.mix", "maps01.mix", "maps02.mix",
			});

			Assert.That(status.CampaignSources, Is.EqualTo(ContentState.Ready));
			Assert.That(status.MissingCampaignFiles, Is.Empty);
			Assert.That(status.Maps, Is.EqualTo(ContentState.Missing));
			Assert.That(status.CanEnterCampaign, Is.False,
				"Raw retail archives must not enable a campaign until converted missions are admitted.");
		}

		[Test]
		public void CampaignArchiveRowsAreVisibleToEveryImporter()
		{
			Assert.That(RetailContentCatalog.KnownFiles,
				Does.Contain("maps01.mix").And.Contain("maps02.mix")
					.And.Contain("mapsmd03.mix")
					.And.Contain("movies01.mix").And.Contain("movies02.mix")
					.And.Contain("movmd03.mix"));
		}

		[TestCase(new[] { "ra2.mix", "language.mix", "multi.mix" }, "RedAlert2")]
		[TestCase(new[] { "ra2.mix", "language.mix", "ra2md.mix", "langmd.mix", "multimd.mix" }, "CombinedCollection")]
		public void CompatibleRetailLayoutsAreClassifiedByCapabilities(string[] files, string expectedProfile)
		{
			var status = catalog.Inspect(files);
			var profile = typeof(RetailContentStatus).GetProperty("Profile");

			Assert.That(profile, Is.Not.Null,
				"The UI needs a diagnostic compatibility profile without rejecting legitimate retail variants.");
			Assert.That(profile?.GetValue(status)?.ToString(), Is.EqualTo(expectedProfile));
		}

		[Test]
		public void CompleteSteamCollectionIsRecognizedWithoutHashLocking()
		{
			var status = catalog.Inspect(new[]
			{
				"MAPS01.MIX", "MAPS02.MIX", "MOVIES01.MIX", "MOVIES02.MIX", "MULTI.MIX",
				"THEME.MIX", "WDT.MIX", "langmd.mix", "language.mix", "mapsmd03.mix",
				"movmd03.mix", "multimd.mix", "ra2.mix", "ra2md.mix", "thememd.mix",
			});
			var profile = typeof(RetailContentStatus).GetProperty("Profile");

			Assert.That(profile, Is.Not.Null);
			Assert.That(profile?.GetValue(status)?.ToString(), Is.EqualTo("SteamComplete"));
			Assert.That(status.CanLaunchBaseGame, Is.True);
			Assert.That(status.CanLaunchYuriGame, Is.True);
			Assert.That(status.CanEnterGame, Is.True);
		}
	}
}
