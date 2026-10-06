using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Network;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture, NonParallelizable]
	public class AiDifficultyInspectionTest
	{
		object previousBundle;
		static readonly FieldInfo BundleField = typeof(FluentProvider).GetField("modFluentBundle", BindingFlags.Static | BindingFlags.NonPublic);

		[SetUp]
		public void SaveBundle() => previousBundle = BundleField.GetValue(null);

		[TearDown]
		public void RestoreBundle() => BundleField.SetValue(null, previousBundle);

		static string Root()
		{
			var path = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (path != null && !Directory.Exists(Path.Combine(path.FullName, ".git"))) path = path.Parent;
			return path.FullName;
		}

		static void LoadLanguage(string language)
		{
			var folder = language == "en" ? "" : language;
			var text = File.ReadAllText(Path.Combine(Root(), "mods", "ra2", "fluent", folder, "chrome.ftl"));
			BundleField.SetValue(null, new FluentBundle(language, text, error => Assert.Fail(error.Message)));
		}

		[TestCase("en", "Master", "Rush")]
		[TestCase("zh-CN", "大师", "快攻")]
		public void ReadOnlySummaryContainsEverySynchronizedParameter(string language, string tier, string style)
		{
			LoadLanguage(language);
			var profile = AiDifficultyCatalog.CreateCustom("master");
			profile.Name = "My Army";
			profile.Style = "rush";
			profile.AttackIntervalSeconds = 37;
			profile.WaveSize = 23;
			profile.Expansion = 3;
			profile.IncomePercent = 175;
			profile.ProductionSpeedPercent = 135;
			var client = new Session.Client { Bot = AiDifficultyCatalog.CustomBotType, BotDifficulty = AiDifficultyCatalog.Encode(profile) };
			var summary = LobbyUtils.DifficultySummary(client);
			foreach (var expected in new[] { "My Army", tier, style, "37", "23", "3", "175%", "135%" })
				Assert.That(summary, Does.Contain(expected));
			Assert.That(summary.Split('\n').Length, Is.EqualTo(4), "Keep the phone dialog compact.");
			Assert.That(LobbyUtils.DifficultySummary(new Session.Client()), Is.Empty);
		}

		[TestCase("en", "Custom", "Hard")]
		[TestCase("zh-CN", "自定义", "困难")]
		public void SavedCustomProfileStatusIdentifiesItsTranslatedBase(string language, string custom, string tier)
		{
			LoadLanguage(language);
			var method = typeof(AiDifficultySettingsLogic).GetMethod("ProfileStatus", BindingFlags.Public | BindingFlags.Static);
			Assert.That(method, Is.Not.Null);
			var status = (string)method.Invoke(null, new object[] { AiDifficultyCatalog.CreateCustom("hard"), false });
			Assert.That(status, Does.Contain(custom).And.Contain(tier));
		}

		[Test]
		public void GuestInspectionReusesExistingButtonAndRemainsEnabledWhenPreviouslyDisabled()
		{
			var method = typeof(LobbyUtils).GetMethod("BindDifficultyInspection", BindingFlags.Public | BindingFlags.Static);
			Assert.That(method, Is.Not.Null);
			var button = (DropDownButtonWidget)FormatterServices.GetUninitializedObject(typeof(DropDownButtonWidget));
			button.IsDisabled = () => true;
			button.IsVisible = () => false;
			var opened = 0;
			method.Invoke(null, new object[] { button, (Action)(() => opened++) });
			Assert.That(button.IsVisible(), Is.True);
			Assert.That(button.IsDisabled(), Is.False);
			button.OnClick();
			Assert.That(opened, Is.EqualTo(1));
			var yaml = File.ReadAllText(Path.Combine(Root(), "engine", "mods", "common", "chrome", "lobby-players.yaml"));
			Assert.That(yaml, Does.Contain("DropDownButton@PLAYER_ACTION:").And.Contain("DropDownButton@SLOT_OPTIONS:"));
		}
		[TestCase(1558, 720, 844, 390, 47d, 21d, false)]
		[TestCase(1560, 720, 932, 430, 59d, 21d, false)]
		[TestCase(1180, 820, 1180, 820, 0d, 20d, true)]
		public void SummaryLeavesRoomForFourReadableLinesAndCloseButton(int width, int height,
			int nativeWidth, int nativeHeight, double horizontalInset, double bottomInset, bool title)
		{
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(nativeWidth, nativeHeight),
				new IosSafeAreaInsets(horizontalInset, 0, horizontalInset, bottomInset));
			var method = typeof(LobbyUtils).GetMethod("DifficultySummaryHasTitle", BindingFlags.Public | BindingFlags.Static);
			Assert.That(method, Is.Not.Null);
			var hasTitle = (bool)method.Invoke(null, new object[] { true, snapshot });
			Assert.That(hasTitle, Is.EqualTo(title));
			Assert.That(method.Invoke(null, new object[] { false, snapshot }), Is.True, "Mac keeps the title regardless of render resolution.");
			var layout = IosTouchDialogLayout.CreateConfirmation(snapshot, 4, 1, false, hasTitle);
			Assert.That(snapshot.SafeBounds.Contains(layout.Panel), Is.True);
			for (var line = 0; line < 4; line++)
			{
				Assert.That(layout.PromptLineBounds(line).Height, Is.GreaterThanOrEqualTo(title ? 24 : 48), "Keep at least one 24-pixel line; phones also have room to wrap.");
				Assert.That(layout.PromptLineBounds(line).Bottom, Is.LessThanOrEqualTo(layout.ButtonBounds(0).Top));
			}
		}

		[TestCase("en")]
		[TestCase("zh-CN")]
		public void CustomNamesThatLookLikeTranslationKeysRemainLiteral(string language)
		{
			LoadLanguage(language);
			var method = typeof(LobbyUtils).GetMethod("ResolveClientDisplayName", BindingFlags.Public | BindingFlags.Static);
			Assert.That(method, Is.Not.Null);
			var profile = AiDifficultyCatalog.CreateCustom("normal");
			profile.Name = "ai-difficulty-nightmare";
			var client = new Session.Client { Bot = AiDifficultyCatalog.CustomBotType,
				Name = profile.Name, BotDifficulty = AiDifficultyCatalog.Encode(profile) };
			Assert.That(method.Invoke(null, new object[] { null, client }), Is.EqualTo(profile.Name));
			client.Name = "stale lobby name";
			Assert.That(method.Invoke(null, new object[] { null, client }), Is.EqualTo(profile.Name), "Use the synchronized profile snapshot.");
		}

	}
}
