using System;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class ButtonActivationIdentityTest
	{
		[Test]
		public void ActivationPreservesOriginalSpriteAndBounds()
		{
			var button = (ButtonWidget)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ButtonWidget));
			button.IsDisabled = () => false;
			var icon = new ImageWidget { Bounds = new WidgetBounds(7, 3, 44, 44) };
			Func<Sprite> original = () => null;
			icon.GetSprite = original;
			var overlay = new ButtonActivationOverlayWidget(button, icon, () => "yuri");
			button.IsHighlighted = () => true;
			Assert.That(overlay.IsVisible(), Is.True);
			Assert.That(icon.GetSprite, Is.SameAs(original));
			Assert.That(icon.Bounds.Width, Is.EqualTo(44));
			Assert.That(icon.Bounds.X, Is.EqualTo(7));
			button.IsDisabled = () => true;
			Assert.That(overlay.IsVisible(), Is.False);
		}

		[TestCase("repair")]
		[TestCase("sell")]
		[TestCase("building")]
		[TestCase("support")]
		[TestCase("infantry")]
		[TestCase("vehicle")]
		public void ClassicFeedbackFollowsWideOriginalButtonNotCircle(string name)
		{
			var rect = HdSidebarWidget.ClassicActivationBounds(name);
			Assert.That(rect.Width, Is.GreaterThan(rect.Height));
			Assert.That(new Rectangle(0, 0, 936, 312).Contains(rect), Is.True);
		}

		[TestCase("chat")]
		[TestCase("controls")]
		[TestCase("menu")]
		public void OriginalTopButtonsAreExcluded(string name)
		{
			Assert.That(HdSidebarWidget.ClassicActivationBounds(name), Is.EqualTo(Rectangle.Empty));
		}
	}
}
