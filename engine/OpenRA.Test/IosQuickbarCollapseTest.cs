using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class IosQuickbarCollapseTest
	{
		static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
		static T Bare<T>(string id) where T : Widget
		{
			var widget = (T)FormatterServices.GetUninitializedObject(typeof(T));
			widget.Id = id;
			widget.IsVisible = () => true;
			typeof(Widget).GetField("Children").SetValue(widget, new List<Widget>());
			return widget;
		}

		[TestCase(844, 390, 844, 390)]
		[TestCase(1558, 720, 844, 390)]
		[TestCase(1180, 820, 1180, 820)]
		[TestCase(2360, 1640, 1180, 820)]
		public void CollapseHidesAllCommandsAndEditTrayThenRestoresTheOriginalOrder(int width, int height, int pointsWidth, int pointsHeight)
		{
			var bar = new CustomCommandBarWidget();
			var slots = Bare<ContainerWidget>("COMMAND_SLOTS");
			bar.AddChild(slots);
			var toggle = Bare<ButtonWidget>("COLLAPSE_TOGGLE");
			var edit = Bare<ButtonWidget>("EDIT_TOGGLE");
			var hint = Bare<LabelWidget>("EDIT_HINT");
			bar.AddChild(toggle); bar.AddChild(edit); bar.AddChild(hint);
			var buttons = (Dictionary<string, ButtonWidget>)typeof(CustomCommandBarWidget).GetField("buttons", Private).GetValue(bar);
			foreach (var id in CommandBarCatalog.AllIds)
			{
				var button = Bare<ButtonWidget>(id);
				buttons.Add(id, button); slots.AddChild(button);
			}

			var order = new List<string> { "STOP", "REPAIR", "GROUP_02", "AUTO_REPAIR", "BEACON" };
			typeof(CustomCommandBarWidget).GetField("visibleOrder", Private).SetValue(bar, order);
			typeof(CustomCommandBarWidget).GetField("collapseButton", Private).SetValue(bar, toggle);
			typeof(CustomCommandBarWidget).GetField("editButton", Private).SetValue(bar, edit);
			typeof(CustomCommandBarWidget).GetField("editHint", Private).SetValue(bar, hint);
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(pointsWidth, pointsHeight), default);
			var layout = typeof(CustomCommandBarWidget).GetMethod("ApplyTouchLayout", Private);
			var expanded = typeof(CustomCommandBarWidget).GetField("expanded", Private);
			for (var cycle = 0; cycle < 10; cycle++)
			{
				expanded.SetValue(bar, false);
				layout.Invoke(bar, new object[] { snapshot, true, null });
				Assert.Multiple(() =>
				{
					Assert.That(buttons.Values.All(b => !b.IsVisible()), Is.True, "No command buttons remain drawn or clickable after collapse.");
					Assert.That(slots.IsVisible(), Is.False);
					Assert.That(toggle.IsVisible(), Is.True);
					Assert.That(edit.IsVisible() || hint.IsVisible(), Is.False);
					Assert.That(bar.Bounds.Width, Is.EqualTo(toggle.Bounds.Width + 2 * TouchCommandBarSlotPolicy.Metric(snapshot, 6)), "The collapsed bar must not leave the old 360-point strip hitbox.");
					Assert.That(snapshot.SafeBounds.Contains(bar.RenderBounds), Is.True);
				});
				expanded.SetValue(bar, true);
				layout.Invoke(bar, new object[] { snapshot, false, null });
				Assert.That(slots.IsVisible(), Is.True);
				Assert.That(edit.IsVisible(), Is.False, "Editing is reached by long press, without a separate button.");
				Assert.That(buttons.Where(p => p.Value.IsVisible()).Select(p => p.Key), Is.EquivalentTo(order));
				Assert.That(order.Select(id => buttons[id].Bounds.X), Is.Ordered);
			}
		}
	}
}
