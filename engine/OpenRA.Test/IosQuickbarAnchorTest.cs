using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.Common.Widgets.Logic.Ingame;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture, NonParallelizable]
	public sealed class IosQuickbarAnchorTest
	{
		const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
		static T Bare<T>(string id) where T : Widget
		{
			var widget = (T)FormatterServices.GetUninitializedObject(typeof(T));
			widget.Id = id;
			widget.IsVisible = () => true;
			typeof(Widget).GetField("Children").SetValue(widget, new List<Widget>());
			return widget;
		}

		[TestCase(667, 375, 667, 375, 0, 0)]
		[TestCase(2400, 1080, 914, 411, 0, 0)]
		[TestCase(1558, 720, 844, 390, 47, 47)]
		[TestCase(1558, 720, 844, 390, 55, 39)]
		[TestCase(1558, 720, 844, 390, 39, 55)]
		[TestCase(1560, 720, 932, 430, 59, 59)]
		[TestCase(1133, 744, 1133, 744, 0, 0)]
		[TestCase(1180, 820, 1180, 820, 0, 0)]
		[TestCase(2360, 1640, 1180, 820, 0, 0)]
		public void ToggleStaysBelowJoystickAndNeitherMovesWhenQuickbarChangesState(
			int width, int height, int pointsWidth, int pointsHeight, int left, int right)
		{
			var previousRoot = Ui.Root;
			var previousSettings = Game.Settings;
			try
			{
				Game.Settings = (Settings)FormatterServices.GetUninitializedObject(typeof(Settings));
				typeof(Settings).GetField("Game").SetValue(Game.Settings, new GameSettings());
				Ui.Root = new ContainerWidget();
				var bar = new CustomCommandBarWidget { Id = "COMMAND_BAR_BACKGROUND", IsVisible = () => true };
				Ui.Root.AddChild(bar);
				var slots = new ContainerWidget { Id = "COMMAND_SLOTS" };
				bar.AddChild(slots);
				var toggle = Bare<ButtonWidget>("COLLAPSE_TOGGLE");
				var edit = Bare<ButtonWidget>("EDIT_TOGGLE");
				bar.AddChild(toggle); bar.AddChild(edit);
				typeof(CustomCommandBarWidget).GetField("collapseButton", Private).SetValue(bar, toggle);
				typeof(CustomCommandBarWidget).GetField("editButton", Private).SetValue(bar, edit);
				var buttons = (Dictionary<string, ButtonWidget>)typeof(CustomCommandBarWidget).GetField("buttons", Private).GetValue(bar);
				var order = new List<string>(CommandBarLayoutPolicy.NormalizeVisibleOrder(CommandBarCatalog.DefaultVisibleIds(), true, 1));
				foreach (var id in CommandBarCatalog.AllIds) { var button = Bare<ButtonWidget>(id); buttons[id] = button; slots.AddChild(button); }
				typeof(CustomCommandBarWidget).GetField("visibleOrder", Private).SetValue(bar, order);
				var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(pointsWidth, pointsHeight), new IosSafeAreaInsets(left, 0, right, 21));
				var layout = typeof(CustomCommandBarWidget).GetMethod("ApplyTouchLayout", Private);
				var obstacle = typeof(IosViewportActionsLogic).GetMethod("BottomObstacleBounds", BindingFlags.Static | BindingFlags.NonPublic);
				foreach (var joystickPoints in new[] { 112, 128, 144 })
				{
					Game.Settings.Game.IosVirtualJoystickSize = joystickPoints;
					Ui.Root.Bounds = new WidgetBounds(17, 29, width, height);
					Rectangle? originalToggle = null, originalJoystick = null;
					for (var cycle = 0; cycle < 6; cycle++)
					{
						var expanded = cycle % 2 == 0;
						typeof(CustomCommandBarWidget).GetField("expanded", Private).SetValue(bar, expanded);
						layout.Invoke(bar, new object[] { snapshot, false, null });
						var viewport = IosViewportControlsLayout.Create(snapshot, joystickPoints, (Rectangle)obstacle.Invoke(null, null));
						Assert.Multiple(() =>
						{
							Assert.That(Math.Abs(2 * toggle.RenderBounds.X + toggle.Bounds.Width - (2 * viewport.JoystickBounds.X + viewport.JoystickBounds.Width)), Is.LessThanOrEqualTo(1));
							Assert.That(toggle.RenderBounds.Top, Is.GreaterThanOrEqualTo(viewport.JoystickBounds.Bottom + snapshot.LogicalPoints(8)));
							Assert.That(snapshot.SafeBounds.Contains(toggle.RenderBounds), Is.True);
							if (expanded)
							{
								Assert.That(buttons.Values.Where(button => button.IsVisible()).Select(button => button.RenderBounds.Y).Distinct().Count(), Is.EqualTo(1), "All commands must fit one row.");
								Assert.That(buttons["GROUP_05"].IsVisible(), Is.True);
								Assert.That(buttons["CYCLE_BASE"].RenderBounds.X, Is.EqualTo(buttons.Values.Where(button => button.IsVisible()).Max(button => button.RenderBounds.X)));
								Assert.That(edit.IsVisible(), Is.False);
								foreach (var button in buttons.Values.Where(button => button.IsVisible()))
								{
									Assert.That(snapshot.SafeBounds.Contains(button.RenderBounds), Is.True, button.Id);
									Assert.That(button.RenderBounds.Right, Is.LessThanOrEqualTo(IosIngameSidebarLayoutPolicy.Create(snapshot).TopBounds.Left - snapshot.LogicalPoints(8)), button.Id);
									Assert.That(button.RenderBounds.IntersectsWith(viewport.JoystickBounds), Is.False, button.Id);
									Assert.That(viewport.ActionBounds.Any(b => b.IntersectsWith(button.RenderBounds)), Is.False, button.Id);
								}
							}
							if (originalToggle.HasValue) Assert.That(toggle.RenderBounds, Is.EqualTo(originalToggle.Value), "The expand/collapse target must stay in the same place.");
							if (originalJoystick.HasValue) Assert.That(viewport.JoystickBounds, Is.EqualTo(originalJoystick.Value), "Collapsing must not move the joystick.");
						});
						originalToggle = toggle.RenderBounds; originalJoystick = viewport.JoystickBounds;
					}
					typeof(CustomCommandBarWidget).GetField("expanded", Private).SetValue(bar, true);
					Assert.DoesNotThrow(() => layout.Invoke(bar, new object[] { snapshot, true, null }), "The largest joystick must also fit while editing the quickbar.");
					var editingViewport = IosViewportControlsLayout.Create(snapshot, joystickPoints, (Rectangle)obstacle.Invoke(null, null));
					Assert.That(editingViewport.JoystickBounds, Is.EqualTo(originalJoystick.Value));
					Assert.That(toggle.RenderBounds, Is.EqualTo(originalToggle.Value));
					foreach (var button in buttons.Values.Where(button => button.IsVisible()))
					{
						Assert.That(snapshot.SafeBounds.Contains(button.RenderBounds), Is.True, button.Id);
						Assert.That(button.RenderBounds.Right, Is.LessThanOrEqualTo(IosIngameSidebarLayoutPolicy.Create(snapshot).TopBounds.Left - snapshot.LogicalPoints(8)), button.Id);
						Assert.That(button.RenderBounds.IntersectsWith(editingViewport.JoystickBounds), Is.False, button.Id);
						Assert.That(editingViewport.ActionBounds.Any(bounds => bounds.IntersectsWith(button.RenderBounds)), Is.False, button.Id);
					}
					var insert = typeof(CustomCommandBarWidget).GetMethod("InsertIndexFromTouchPoint", Private);
					for (var i = 0; i < order.Count; i++)
					{
						var rect = buttons[order[i]].RenderBounds;
						Assert.That(insert.Invoke(bar, new object[] { new int2(rect.X, rect.Y + rect.Height / 2) }), Is.EqualTo(i), order[i]);
					}

				}
			}
			finally { Ui.Root = previousRoot; Game.Settings = previousSettings; }
		}
	}
}
