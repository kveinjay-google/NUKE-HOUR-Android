using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class PhoneColorSwatchTest
	{
		[Test]
		public void HiddenArrowGivesTheWholeCellToTheColor()
		{
			var hide = typeof(DropDownButtonWidget).GetField("HideArrow");
			Assert.That(hide, Is.Not.Null);
#pragma warning disable SYSLIB0050
			var button = (DropDownButtonWidget)FormatterServices.GetUninitializedObject(typeof(DropDownButtonWidget));
#pragma warning restore SYSLIB0050
			button.Bounds = new WidgetBounds(0, 0, 64, 48);
			hide!.SetValue(button, true);
			Assert.That(button.UsableWidth, Is.EqualTo(64));
		}

		[TestCase(844, 390)]
		[TestCase(956, 440)]
		public void PhoneContentFillsTheSafeAreaWithTwoPointBorder(int width, int height)
		{
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(width, height), default);
			var content = MultiplayerScreenLayout.ContentBounds(snapshot, compactPhone: true);
			Assert.That(content.Y, Is.EqualTo(2));
			Assert.That(height - content.Bottom, Is.EqualTo(2));
		}
	}
}
