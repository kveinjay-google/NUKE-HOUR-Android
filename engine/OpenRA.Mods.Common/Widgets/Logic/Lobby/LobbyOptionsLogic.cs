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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public class LobbyOptionsLogic : ChromeLogic
	{
		[FluentReference]
		const string NotAvailable = "label-not-available";

		readonly ScrollPanelWidget panel;
		readonly Widget optionsContainer;
		readonly Widget checkboxRowTemplate;
		readonly Widget dropdownRowTemplate;
		ButtonWidget inlineResetButton;
		readonly int yMargin;

		readonly Func<MapPreview> getMap;
		readonly OrderManager orderManager;
		readonly Func<bool> configurationDisabled;
		MapPreview mapPreview;
		World ingameWorld;
		PopulationLimits ingamePopulationLimits;

		[ObjectCreator.UseCtor]
		internal LobbyOptionsLogic(Widget widget, OrderManager orderManager, Func<MapPreview> getMap,
			Func<bool> configurationDisabled)
		{
			this.getMap = getMap;
			this.orderManager = orderManager;
			this.configurationDisabled = configurationDisabled;

			panel = (ScrollPanelWidget)widget;
			optionsContainer = widget.Get("LOBBY_OPTIONS");
			yMargin = optionsContainer.Bounds.Y;
			checkboxRowTemplate = optionsContainer.Get("CHECKBOX_ROW_TEMPLATE");
			dropdownRowTemplate = optionsContainer.Get("DROPDOWN_ROW_TEMPLATE");

			mapPreview = getMap();
			RebuildOptions();
		}

		internal void AttachIngameWorld(World world)
		{
			ingameWorld = world;
			ingamePopulationLimits = world.WorldActor.TraitOrDefault<PopulationLimits>();
			RebuildOptions();
		}

		internal void AttachInlineReset(ButtonWidget button)
		{
			inlineResetButton = button;
			RebuildOptions();
		}

		public override void Tick()
		{
			var newMapPreview = getMap();
			if (newMapPreview == mapPreview)
				return;

			// We are currently enumerating the widget tree and so can't modify any layout
			// Defer it to the end of tick instead
			Game.RunAfterTick(() =>
			{
				mapPreview = newMapPreview;
				RebuildOptions();
			});
		}

		void RebuildOptions()
		{
			if (mapPreview == null || mapPreview.WorldActorInfo == null)
				return;

			if (inlineResetButton?.Parent != null)
				inlineResetButton.Parent.Children.Remove(inlineResetButton);

			optionsContainer.RemoveChildren();
			optionsContainer.Bounds.Height = 0;
			var allOptions = mapPreview.PlayerActorInfo.TraitInfos<ILobbyOptions>()
					.Concat(mapPreview.WorldActorInfo.TraitInfos<ILobbyOptions>())
					.SelectMany(t => t.LobbyOptions(mapPreview))
					.Where(o => o.IsVisible)
					.OrderBy(o => o.DisplayOrder)
					.ToArray();

			Widget row = null;
			var checkboxColumns = new Queue<CheckboxWidget>();
			var dropdownColumns = new Queue<DropDownButtonWidget>();

			foreach (var option in allOptions.Where(o => o is LobbyBooleanOption))
			{
				if (checkboxColumns.Count == 0)
				{
					row = checkboxRowTemplate.Clone();
					row.Bounds.Y = optionsContainer.Bounds.Height;
					optionsContainer.Bounds.Height += row.Bounds.Height;
					foreach (var child in row.Children)
						if (child is CheckboxWidget childCheckbox)
							checkboxColumns.Enqueue(childCheckbox);

					optionsContainer.AddChild(row);
				}

				var checkbox = checkboxColumns.Dequeue();
				var optionEnabled = new PredictedCachedTransform<Session.Global, bool>(
					gs => gs.OptionOrDefault(option.Id, option.DefaultValue == true.ToString()));

				var optionLocked = new CachedTransform<Session.Global, bool>(
					gs => gs.OptionStateOrDefault(option.Id, option.DefaultValue).IsLocked);

				checkbox.GetText = () => option.Name;
				if (option.Description != null)
				{
					var (text, desc) = LobbyUtils.SplitOnFirstToken(option.Description);
					checkbox.GetTooltipText = () => text;
					checkbox.GetTooltipDesc = () => desc;
				}

				checkbox.IsVisible = () => true;
				checkbox.IsChecked = () => optionEnabled.Update(orderManager.LobbyInfo.GlobalSettings);
				checkbox.IsDisabled = () => configurationDisabled() || optionLocked.Update(orderManager.LobbyInfo.GlobalSettings);
				checkbox.OnClick = () =>
				{
					var state = !optionEnabled.Update(orderManager.LobbyInfo.GlobalSettings);
					orderManager.IssueOrder(Order.Command($"option {option.Id} {state}"));
					optionEnabled.Predict(state);
				};
			}

			foreach (var option in allOptions.Where(o => o is not LobbyBooleanOption))
			{
				if (dropdownColumns.Count == 0)
				{
					row = dropdownRowTemplate.Clone();
					row.Bounds.Y = optionsContainer.Bounds.Height;
					optionsContainer.Bounds.Height += row.Bounds.Height;
					foreach (var child in row.Children)
						if (child is DropDownButtonWidget dropDown)
							dropdownColumns.Enqueue(dropDown);

					optionsContainer.AddChild(row);
				}

				var dropdown = dropdownColumns.Dequeue();
				var optionValue = new CachedTransform<Session.Global, Session.LobbyOptionState>(
					gs => gs.OptionStateOrDefault(option.Id, option.DefaultValue));

				var getOptionLabel = new CachedTransform<string, string>(id =>
				{
					if (id == null || !option.Values.TryGetValue(id, out var value))
						return FluentProvider.GetMessage(NotAvailable);

					return value;
				});

				var runtimeTotal = ingameWorld != null && ingamePopulationLimits != null && option.Id == PopulationLimits.TotalOptionId;
				string CurrentValue() => runtimeTotal ? ingamePopulationLimits.TotalLimit.ToString(CultureInfo.InvariantCulture)
					: optionValue.Update(orderManager.LobbyInfo.GlobalSettings).Value;
				dropdown.GetText = () => getOptionLabel.Update(CurrentValue());
				if (option.Description != null)
				{
					var description = runtimeTotal ? FluentProvider.GetMessage(PopulationMessages.RuntimeDescription) : option.Description;
					var (text, desc) = LobbyUtils.SplitOnFirstToken(description);
					dropdown.GetTooltipText = () => text;
					dropdown.GetTooltipDesc = () => desc;
				}

				dropdown.IsVisible = () => true;
				dropdown.IsDisabled = () => runtimeTotal
					? ingameWorld.IsReplay || !ingamePopulationLimits.CanControlTotal(orderManager.LocalClient?.Index ?? -1)
					: configurationDisabled() || option.IsLocked || optionValue.Update(orderManager.LobbyInfo.GlobalSettings).IsLocked;

				dropdown.OnMouseDown = _ =>
				{
					// Opt in only for the NUKE HOUR lobby, leaving shared mod menus unchanged.
					if (dropdown.Background == "cc-mp-control")
					{
						dropdown.GetPopupSafeBounds = () => IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution).SafeBounds;
						dropdown.PreparePanel = popup => LobbyOptionPickerLayout.Prepare((ScrollPanelWidget)popup,
							IosScreenMetrics.SnapshotFor(Game.Renderer.Resolution), Platform.UsesMobileLayout);
					}

					ScrollItemWidget SetupItem(KeyValuePair<string, string> c, ScrollItemWidget template)
					{
						bool IsSelected() => CurrentValue() == c.Key;
						void OnClick()
						{
							if (dropdown.IsDisabled())
								return;
							if (runtimeTotal)
								ingameWorld.IssueOrder(new Order(PopulationLimits.SetTotalOrder, ingameWorld.WorldActor, false)
								{
									ExtraData = uint.Parse(c.Key, CultureInfo.InvariantCulture)
								});
							else
								orderManager.IssueOrder(Order.Command($"option {option.Id} {c.Key}"));
						}

						var item = ScrollItemWidget.Setup(template, IsSelected, OnClick);
						item.IsDisabled = dropdown.IsDisabled;
						item.Get<LabelWidget>("LABEL").GetText = () => c.Value;
						return item;
					}

					dropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", option.Values.Count * 30, option.Values, SetupItem);
				};

				var label = row.GetOrNull<LabelWidget>(dropdown.Id + "_DESC");
				if (label != null)
				{
					label.GetText = () => option.Name + ":";
					label.IsVisible = () => true;
				}
			}

			if (inlineResetButton != null)
			{
				var resetRow = new ContainerWidget
				{
					Id = "RESET_OPTIONS_ROW",
					Bounds = new WidgetBounds(0, optionsContainer.Bounds.Height,
						dropdownRowTemplate.Bounds.Width, dropdownRowTemplate.Bounds.Height)
				};
				inlineResetButton.Bounds = new WidgetBounds(10, 25,
					Math.Max(0, resetRow.Bounds.Width / 3 - 20), 25);
				resetRow.AddChild(inlineResetButton);
				optionsContainer.AddChild(resetRow);
				optionsContainer.Bounds.Height += resetRow.Bounds.Height;
			}

			panel.ContentHeight = yMargin + optionsContainer.Bounds.Height;
			optionsContainer.Bounds.Y = yMargin;

			panel.ScrollToTop();
		}
	}
}
