using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class CampaignBrowserLayoutTest
	{
		[Test]
		public void MissionNumberAppearsInSharedTitleFormatter()
		{
			var method = typeof(CampaignBrowserLayout).GetMethod("NumberedTitle");
			Assert.That(method, Is.Not.Null);
			Assert.That(method.Invoke(null, new object[] { 1, "孤独守卫" }), Is.EqualTo("01 孤独守卫"));
			Assert.That(method.Invoke(null, new object[] { 12, "Polar Storm" }), Is.EqualTo("12 Polar Storm"));
			Assert.That(method.Invoke(null, new object[] { 0, "Custom" }), Is.EqualTo("Custom"));
		}
		[TestCase("Hail to the Chief")]
		[TestCase("The Fox and the Hound")]
		[TestCase("尤里超时空防御战")]
		public void MissionTitlesRefitAfterResizeWithoutLosingTheirOriginalText(string title)
		{
			var width = 136;
			var getText = CampaignBrowserLayout.CreateTitleGetter(() => title, () => width, value => value.Length * 20);
			Assert.That(getText().Length * 20, Is.LessThanOrEqualTo(width));
			Assert.That(getText(), Is.Not.EqualTo(title));
			width = 600;
			Assert.That(getText(), Is.EqualTo(title));
			width = 80;
			Assert.That(getText().Length * 20, Is.LessThanOrEqualTo(width));
		}

		[Test]
		public void MissionStatusIsPlacedAfterTheVisibleTitleWithoutOverlap()
		{
			var bounds = CampaignBrowserLayout.MissionRowTextBounds(
				rowWidth: 320, rowHeight: 64, gap: 12, statusWidth: 48, measuredTitleWidth: 180);
			Assert.That(bounds.Status.X, Is.GreaterThanOrEqualTo(bounds.Title.Right + 12));
			Assert.That(bounds.Status.Right, Is.LessThanOrEqualTo(320 - 12));
			Assert.That(bounds.Title.Width, Is.EqualTo(180));
		}

		[Test]
		public void LongMissionTitleReservesSpaceForLocalizedStatus()
		{
			var bounds = CampaignBrowserLayout.MissionRowTextBounds(
				rowWidth: 240, rowHeight: 64, gap: 10, statusWidth: 60, measuredTitleWidth: 400);
			Assert.That(bounds.Status.X, Is.GreaterThanOrEqualTo(bounds.Title.Right + 10));
			Assert.That(bounds.Status.Right, Is.LessThanOrEqualTo(230));
			Assert.That(bounds.Title.Width, Is.EqualTo(150));
		}

		[TestCase(1440, 900)]
		[TestCase(1024, 768)]
		[TestCase(1180, 820)]
		[TestCase(852, 393)]
		[TestCase(812, 375)]
		public void CampaignModulesStaySeparatedWithinTheFrame(int width, int height)
		{
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(width, height), default);
			var layout = new CampaignBrowserLayout(snapshot);
			Assert.That(layout.List.Right, Is.LessThan(layout.Info.X));
			Assert.That(layout.List.Bottom, Is.LessThan(layout.Start.Y));
			Assert.That(layout.Start.Bottom, Is.LessThanOrEqualTo(layout.Content.Bottom));
			Assert.That(layout.Back.Right, Is.LessThanOrEqualTo(layout.Content.Right));
			Assert.That(layout.Start.Height, Is.GreaterThanOrEqualTo(48));
			Assert.That(layout.Summary.Bottom, Is.LessThan(layout.List.Y));
			Assert.That(layout.Summary.X, Is.EqualTo(layout.List.X));
			Assert.That(layout.Intel.Y, Is.Zero);
			Assert.That(layout.Intel.Height, Is.EqualTo(layout.Info.Height));
			Assert.That(layout.Map.Width, Is.LessThan(layout.Summary.Width / 2));
			Assert.That(layout.Intel.Height, Is.GreaterThan(layout.Map.Height));
			Assert.That(layout.RowHeight, Is.GreaterThanOrEqualTo(56));
		}
	}
}
