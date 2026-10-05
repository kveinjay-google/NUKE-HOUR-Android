using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
		public class ServerNameMarqueeTest
	{
		[Test]
		public void ResponsiveLayoutMustNotTruncateMarqueeSource()
		{
			#pragma warning disable SYSLIB0050
			var label = (LabelWithTooltipWidget)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(LabelWithTooltipWidget));
			#pragma warning restore SYSLIB0050
			label.ScrollOverflow = true;
			System.Func<string> source = () => "完整服务器名称";
			label.GetText = source;
			IosResponsiveText.Configure(label, false);
			Assert.That(label.GetText, Is.SameAs(source));
		}
		[Test]
		public void ShortNamesStayStillAndLongNamesPauseAndLoop()
		{
			Assert.That(LabelWithTooltipWidget.MarqueeOffset(100, 200, 9), Is.Zero);
			Assert.That(LabelWithTooltipWidget.MarqueeOffset(300, 200, 1), Is.Zero);
			Assert.That(LabelWithTooltipWidget.MarqueeOffset(300, 200, 3), Is.EqualTo(42));
			for (var t = 0; t < 100; t++)
				Assert.That(LabelWithTooltipWidget.MarqueeOffset(300, 200, t), Is.InRange(0, 100));
		}
	}
}
