using System.Collections.Generic;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class EmptyTooltipTest
	{
		static T Bare<T>() where T : Widget
		{
#pragma warning disable SYSLIB0050
			var widget = (T)FormatterServices.GetUninitializedObject(typeof(T));
#pragma warning restore SYSLIB0050
			typeof(Widget).GetField(nameof(Widget.Children)).SetValue(widget, new List<Widget>());
			widget.IsVisible = () => true;
			return widget;
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		[TestCase("\n\t")]
		public void EmptyCampaignTitleTooltipNeverDrawsItsZeroWidthFrame(string text)
		{
			var root = CreateTooltip(() => text, out var container);
			Assert.DoesNotThrow(() => container.BeforeRender());
			Assert.That(root.IsVisible(), Is.False);
			Assert.That(root.Children, Is.Empty);
		}

		[Test]
		public void TooltipVisibilityTracksTitleOverflowWithoutReenteringTheRow()
		{
			var text = "";
			var root = CreateTooltip(() => text, out var container);
			Assert.That(root.IsVisible(), Is.False);
			text = "02 危机黎明";
			Assert.That(root.IsVisible(), Is.True, "Truncated titles still expose their full text.");
			text = "";
			Assert.DoesNotThrow(() => container.BeforeRender());
			Assert.That(root.IsVisible(), Is.False, "A widened row must not leave an empty tooltip frame.");
		}

		static Widget CreateTooltip(System.Func<string> getText, out TooltipContainerWidget container)
		{
			var root = new ContainerWidget { Bounds = new WidgetBounds(0, 0, 0, 32) };
			var label = Bare<LabelWidget>();
			label.Id = "LABEL";
			label.Bounds = new WidgetBounds(7, -1, 0, 23);
			root.AddChild(label);
			root.AddChild(new ContainerWidget { Id = "LINE_HEIGHT", Bounds = new WidgetBounds(0, 3, 0, 19) });
			container = Bare<TooltipContainerWidget>();
			new SimpleTooltipLogic(root, container, getText);
			return root;
		}
	}
}
