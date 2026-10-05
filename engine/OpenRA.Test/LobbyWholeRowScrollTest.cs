using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class LobbyWholeRowScrollTest
	{
		[TestCase(266, 48, 12, 228)]
		[TestCase(300, 48, 12, 288)]
		[TestCase(190, 44, 8, 148)]
		public void HeightContainsOnlyCompleteRows(int available, int row, int gap, int expected)
		{
			Assert.That(ScrollPanelWidget.WholeRowHeight(available, row, gap), Is.EqualTo(expected));
		}

		[TestCase(-31, 60, 228, 468, -60)]
		[TestCase(-239, 60, 228, 468, -240)]
		[TestCase(-999, 60, 228, 468, -240)]
		[TestCase(-60, 60, 228, 108, 0)]
		[TestCase(55, 60, 228, 468, 0)]
		public void OffsetNeverExposesHalfARow(float value, int step, int viewport, int content, float expected)
		{
			Assert.That(ScrollPanelWidget.WholeRowOffset(value, step, viewport, content), Is.EqualTo(expected));
		}

		[Test]
		public void EveryViewportAndRosterEndsOnAWholeSlot()
		{
			foreach (var row in new[] { 32, 44, 48, 64 })
				foreach (var gap in new[] { 4, 8, 12 })
					for (var height = row; height < 700; height += 7)
					{
						var viewport = ScrollPanelWidget.WholeRowHeight(height, row, gap);
						Assert.That(viewport, Is.LessThanOrEqualTo(height));
						Assert.That((viewport + gap) % (row + gap), Is.Zero);
						for (var count = 1; count <= 16; count++)
						{
							var content = count * (row + gap) - gap;
							var offset = ScrollPanelWidget.WholeRowOffset(-9999, row + gap, viewport, content);
							Assert.That(-offset % (row + gap), Is.Zero);
							Assert.That(content + offset, Is.LessThanOrEqualTo(viewport));
						}
					}
		}
	}
}
