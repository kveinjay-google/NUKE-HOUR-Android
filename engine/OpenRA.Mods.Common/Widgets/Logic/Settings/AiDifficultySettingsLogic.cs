using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class AiDifficultySettingsLogic : ChromeLogic
	{
		[FluentReference] const string TabLabel = "ai-settings-tab";
		[FluentReference] const string DeleteTitle = "ai-settings-delete-title";
		[FluentReference] const string DeletePrompt = "ai-settings-delete-prompt";
		[FluentReference] const string InvalidName = "ai-settings-invalid-name";
		[FluentReference] const string Saved = "ai-settings-saved";
		[FluentReference] const string Official = "ai-settings-official";
		[FluentReference] const string Editing = "ai-settings-editing";
		[FluentReference("base")] const string CustomStatus = "ai-settings-custom-status";
		[FluentReference] const string CustomSuffix = "ai-settings-copy-name";
		[FluentReference] const string Balanced = "ai-style-balanced";
		[FluentReference] const string Defensive = "ai-style-defensive";
		[FluentReference] const string Rush = "ai-style-rush";
		[FluentReference("income", "speed")] const string Bonuses = "ai-settings-bonuses";

		List<AiDifficultyProfile> profiles;
		AiDifficultyProfile selected, draft;
		Widget panel;
		ScrollPanelWidget scroll;
		TextFieldWidget name;
		bool editing;
		string status = "";
		readonly List<Action> refresh = new();

		public static string DisplayName(AiDifficultyProfile profile) => profile.Id.StartsWith("custom-", StringComparison.Ordinal) ?
			profile.Name : FluentProvider.GetMessage("ai-difficulty-" + profile.Id);

		public static string StyleName(string style) => FluentProvider.GetMessage(style switch
		{
			"defensive" => Defensive,
			"rush" => Rush,
			_ => Balanced
		});

		public static string ProfileStatus(AiDifficultyProfile profile, bool editing) => editing
			? FluentProvider.GetMessage(Editing)
			: profile.Id.StartsWith("custom-", StringComparison.Ordinal)
				? FluentProvider.GetMessage(CustomStatus, "base", DisplayName(AiDifficultyCatalog.GetOfficial(profile.BaseDifficulty)))
				: FluentProvider.GetMessage(Official);

		[ObjectCreator.UseCtor]
		public AiDifficultySettingsLogic(Action<string, string, Func<Widget, Func<bool>>, Func<Widget, Action>> registerPanel, string panelID, string label)
		{
			registerPanel(panelID, label, InitPanel, ResetPanel);
		}

		bool IsCustom => selected.Id.StartsWith("custom-", StringComparison.Ordinal);
		bool IsPhone => (Platform.UsesMobileLayout ? IosSettingsLayout.ForSnapshot(true,
			IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution)) :
			IosSettingsLayout.ForPreview(Environment.GetEnvironmentVariable("NUKEHOUR_SETTINGS_PREVIEW"), Game.Renderer.Resolution)).IsPhone;

		Func<bool> InitPanel(Widget widget)
		{
			panel = widget;
			scroll = panel.Get<ScrollPanelWidget>("SETTINGS_SCROLLPANEL");
			profiles = AiDifficultyPresets.Load(Game.Settings.Game.AiCustomProfiles);
			selected = AiDifficultyPresets.Find(Game.Settings.Game.AiDefaultProfile, profiles);
			draft = AiDifficultyCatalog.Normalize(selected);
			name = panel.Get<TextFieldWidget>("AI_NAME");
			name.MaxLength = 24;
			name.IsDisabled = () => !editing;
			name.OnTextEdited = () => draft.Name = name.Text;
			var picker = panel.Get<DropDownButtonWidget>("AI_PRESET");
			picker.GetText = () => DisplayName(selected);
			picker.IsDisabled = () => editing;
			picker.OnClick = () => Show(picker, AiDifficultyCatalog.OfficialProfiles.Concat(profiles), DisplayName, p => p.Id == selected.Id,
				p => { selected = AiDifficultyCatalog.Normalize(p); Cancel(); });
			var style = panel.Get<DropDownButtonWidget>("AI_STYLE");
			style.IsDisabled = () => !editing;
			style.GetText = () => StyleName(draft.Style);
			style.OnClick = () => Show(style, new[] { "defensive", "balanced", "rush" }, StyleName,
				s => s == draft.Style, s => draft.Style = s);

			BindSlider("AI_INTERVAL", 2, 120, () => draft.AttackIntervalSeconds, v => draft.AttackIntervalSeconds = v);
			BindSlider("AI_WAVE", 3, 60, () => draft.WaveSize, v => draft.WaveSize = v);
			BindSlider("AI_EXPANSION", 1, 4, () => draft.Expansion, v => draft.Expansion = v);
			BindSlider("AI_INCOME", 100, 300, () => draft.IncomePercent, v => draft.IncomePercent = v);
			BindSlider("AI_SPEED", 100, 200, () => draft.ProductionSpeedPercent, v => draft.ProductionSpeedPercent = v);
			panel.Get<LabelWidget>("AI_STATUS").GetText = () => status.Length > 0 ? status :
				ProfileStatus(selected, editing);
			panel.Get<LabelWidget>("AI_BONUSES").GetText = () => FluentProvider.GetMessage(Bonuses,
				"income", draft.IncomePercent, "speed", draft.ProductionSpeedPercent);

			Button("AI_CREATE", () =>
			{
				draft = AiDifficultyCatalog.Normalize(selected);
				draft.Id = AiDifficultyCatalog.CreateCustom(selected.BaseDifficulty).Id;
				var prefix = DisplayName(selected);
				if (prefix.Length > 14) prefix = prefix[..14];
				var suffix = FluentProvider.GetMessage(CustomSuffix);
				var index = 1;
				do { draft.Name = $"{prefix} {suffix} {index++}"; }
				while (profiles.Any(p => p.Name == draft.Name));
				editing = true;
				status = "";
				Refresh();
			}, () => editing || profiles.Count >= AiDifficultyPresets.MaximumCount);
			Button("AI_EDIT", () => { editing = true; status = ""; Refresh(); }, () => editing || !IsCustom);
			Button("AI_SAVE", () =>
			{
				draft.Name = name.Text;
				if (!AiDifficultyPresets.Save(profiles, draft)) { status = FluentProvider.GetMessage(InvalidName); return; }
				selected = AiDifficultyPresets.Find(draft.Id, profiles);
				Persist();
				Cancel();
				status = FluentProvider.GetMessage(Saved);
			}, () => !editing);
			Button("AI_CANCEL", Cancel, () => !editing);
			Button("AI_RESET", ResetDraft, () => !editing);
			Button("AI_DELETE", () =>
			{
				ConfirmationDialogs.ButtonPrompt(Game.ModData,
					title: DeleteTitle, text: DeletePrompt,
					onConfirm: () =>
					{
						profiles.RemoveAll(p => p.Id == selected.Id);
						if (Game.Settings.Game.AiDefaultProfile == selected.Id) Game.Settings.Game.AiDefaultProfile = "normal";
						selected = AiDifficultyCatalog.GetOfficial(selected.BaseDifficulty);
						Persist(); Cancel();
					});
			}, () => editing || !IsCustom);
			Button("AI_DEFAULT", () => { Game.Settings.Game.AiDefaultProfile = selected.Id; Persist(); },
				() => editing || Game.Settings.Game.AiDefaultProfile == selected.Id);

			foreach (var id in new[] { "AI_PRESET_ROW", "AI_CREATE_ROW", "AI_EDIT_ROW", "AI_DELETE_ROW", "AI_DEFAULT_ROW" })
				panel.Get(id).IsVisible = () => !IsPhone || !editing;
			foreach (var id in new[] { "AI_NAME_ROW", "AI_STYLE_ROW", "AI_INTERVAL_ROW", "AI_WAVE_ROW", "AI_EXPANSION_ROW", "AI_INCOME_ROW", "AI_SPEED_ROW", "AI_SAVE_ROW", "AI_CANCEL_ROW", "AI_RESET_ROW" })
				panel.Get(id).IsVisible = () => !IsPhone || editing;
			Refresh();
			return () => { Cancel(); return false; };
		}

		void Button(string id, Action click, Func<bool> disabled)
		{
			var button = panel.Get<ButtonWidget>(id);
			button.OnClick = click;
			button.IsDisabled = disabled;
		}

		void BindSlider(string id, int min, int max, Func<int> get, Action<int> set)
		{
			var slider = panel.Get<SliderWidget>(id);
			slider.MinimumValue = min;
			slider.MaximumValue = max;
			slider.TouchStep = 1;
			slider.IsDisabled = () => !editing;
			slider.GetValue = () => get();
			slider.OnChange += value => { if (editing) set((int)Math.Round(value)); };
			panel.Get<LabelWidget>(id + "_VALUE").GetText = () => get().ToStringInvariant();
			refresh.Add(() => slider.Value = get());
		}

		static void Show<T>(DropDownButtonWidget button, IEnumerable<T> items, Func<T, string> text, Func<T, bool> chosen, Action<T> select)
		{
			ScrollItemWidget Setup(T value, ScrollItemWidget template)
			{
				var item = ScrollItemWidget.Setup(template, () => chosen(value), () => select(value));
				item.Get<LabelWidget>("LABEL").GetText = () => text(value);
				return item;
			}
			button.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", 400, items, Setup);
		}

		void Refresh()
		{
			name.Text = editing ? draft.Name : DisplayName(selected);
			foreach (var action in refresh) action();
			scroll.Layout.AdjustChildren();
			scroll.ScrollToTop();
		}

		void Cancel()
		{
			editing = false;
			draft = AiDifficultyCatalog.Normalize(selected);
			status = "";
			Refresh();
		}

		void ResetDraft()
		{
			var original = AiDifficultyCatalog.GetOfficial(draft.BaseDifficulty);
			original.Id = draft.Id;
			original.Name = draft.Name;
			draft = original;
			Refresh();
		}

		void Persist()
		{
			Game.Settings.Game.AiCustomProfiles = AiDifficultyPresets.Encode(profiles);
			Game.Settings.Save();
		}

		Action ResetPanel(Widget _) => () =>
		{
			if (editing) ResetDraft();
			else { Game.Settings.Game.AiDefaultProfile = "normal"; selected = AiDifficultyCatalog.GetOfficial("normal"); Cancel(); Persist(); }
		};
	}
}
