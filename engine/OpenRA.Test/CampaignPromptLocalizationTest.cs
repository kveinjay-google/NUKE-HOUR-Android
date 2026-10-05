using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OpenRA.Mods.RA2.Missions;

namespace OpenRA.Test
{
	[TestFixture]
	public class CampaignPromptLocalizationTest
	{
		static string Root([CallerFilePath] string path = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), "../.."));

		[Test]
		public void FirstMissionsHaveChinesePromptsIndependentOfImportedLanguage()
		{
			var root = Environment.GetEnvironmentVariable("NUKE_HOUR_CAMPAIGN_AUDIT_ROOT");
			if (string.IsNullOrEmpty(root))
				Assert.Ignore("Requires user-imported mission packages (read-only).");
			var bundle = new FluentBundle("zh-CN", File.ReadAllText(Path.Combine(Root(), "mods/ra2/fluent/zh-CN/chrome.ftl")), e => Assert.Fail(e.ToString()));
			foreach (var mission in new[] { "allied-01", "soviet-01" })
			{
				using var map = ZipFile.OpenRead(Path.Combine(root, mission + ".oramap"));
				using var reader = new StreamReader(map.GetEntry("mission.ini").Open());
				var keys = Regex.Matches(reader.ReadToEnd(), @"(?i)MISSION:[a-z0-9]+").Select(m => m.Value.ToLowerInvariant()).Distinct().ToArray();
				Assert.That(keys, Is.Not.Empty);
				foreach (var key in keys)
				{
					var text = RetailMissionText.Resolve(key, id => bundle.TryGetMessage(id, out var value) ? value : null,
						_ => throw new AssertionException("Imported language must not override first-mission UI text: " + key));
					Assert.That(Regex.IsMatch(text, @"[\u4e00-\u9fff]"), Is.True, key);
				}
			}
		}

		[TestCase("zh-CN", "zh-CN/", "任务", "战场指挥")]
		[TestCase("en", "", "Mission", "Battlefield Control")]
		public void EveryFirstMissionPromptAndPrefixUsesSelectedLanguage(string language, string directory, string prefix, string system)
		{
			var bundle = new FluentBundle(language, File.ReadAllText(Path.Combine(Root(), "mods/ra2/fluent", directory, "chrome.ftl")), e => Assert.Fail(e.ToString()));
			var keys = "all01a all01b all01h all01i all01q all01f all01l all01m all01n all01o all01p all01s all01t all01u all05g all05k objective1 sov01a sov1b sov1c sov1e sov1g sov1h sov11e sov11g sov11k sov11f".Split(' ');
			foreach (var key in keys)
			{
				var text = RetailMissionText.Resolve("MISSION:" + key.ToUpperInvariant(),
					id => bundle.TryGetMessage(id, out var value) ? value : null,
					_ => throw new AssertionException("Unexpected imported-language fallback: " + key));
				Assert.That(Regex.IsMatch(text, @"[\u4e00-\u9fff]"), Is.EqualTo(language == "zh-CN"), key);
			}

			Assert.That(bundle.GetMessage("retail-mission-prefix"), Is.EqualTo(prefix));
			Assert.That(bundle.GetMessage("notification-system-prefix"), Is.EqualTo(system));
		}

		[Test]
		public void CustomMissionFallbackRemainsReadable()
		{
			Assert.That(RetailMissionText.Resolve("MISSION:CUSTOM", _ => null, _ => "Imported instruction\0"), Is.EqualTo("Imported instruction"));
			Assert.That(RetailMissionText.Resolve("MISSION:MISSING", _ => null, _ => null), Is.EqualTo("MISSION:MISSING"));
			Assert.That(RetailMissionText.Resolve(" ", _ => throw new Exception(), _ => throw new Exception()), Is.Empty);
		}

		[Test]
		public void RuntimeUsesLanguageResolverAndDynamicSystemPrefix()
		{
			var runtime = File.ReadAllText(Path.Combine(Root(), "OpenRA.Mods.RA2/Traits/RetailCampaignRuntime.cs"));
			StringAssert.Contains("RetailMissionText.Resolve(key", runtime);
			StringAssert.Contains("FluentProvider.GetMessage(\"retail-mission-prefix\")", runtime);
			var system = File.ReadAllText(Path.Combine(Root(), "engine/OpenRA.Game/TextNotificationsManager.cs"));
			StringAssert.Contains("SystemMessageLabel => FluentProvider.GetMessage(SystemMessageLabelKey)", system);
			StringAssert.Contains("SystemMessageLabel: notification-system-prefix", File.ReadAllText(Path.Combine(Root(), "mods/ra2/metrics.yaml")));
		}
	}
}
