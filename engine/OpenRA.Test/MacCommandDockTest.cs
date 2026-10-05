using System;
using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public class MacCommandDockTest
	{
		[Test]
		public void MacDockHasDedicatedPolicy()
		{
			Assert.That(typeof(CustomCommandBarWidget).Assembly.GetType("OpenRA.Mods.RA2.Widgets.MacCommandDockLayout"),
				Is.Not.Null, "Mac must not use the overcrowded shared desktop layout.");
		}

		[Test]
		public void EveryDesktopCommandRemainsAvailableExactlyOnce()
		{
			var all = MacCommandDockLayout.Primary.Concat(MacCommandDockLayout.Pages.SelectMany(p => p)).ToArray();
			Assert.That(MacCommandDockLayout.Primary.Length, Is.EqualTo(7));
			Assert.That(all.Distinct().Count(), Is.EqualTo(all.Length));
			Assert.That(all, Is.EquivalentTo(CommandBarLayoutPolicy.AvailableIds(CommandBarCatalog.AllIds, false)));
			Assert.That(MacCommandDockLayout.Pages.All(p => p.Length <= 10), Is.True);
			Assert.That(all.Count(id => id == "AUTO_REPAIR"), Is.EqualTo(1));
		}

		[TestCase(360)]
		[TestCase(540)]
		[TestCase(1024)]
		[TestCase(1920)]
		public void DockAndAllHitTargetsFitBattlefield(int width)
		{
			var available = new Rectangle(12, 0, width, 720);
			foreach (var open in new[] { false, true })
				for (var page = 0; page < 3; page++)
				{
					var layout = new MacCommandDockLayout(available, open, page);
					Assert.That(available.Contains(layout.Bounds), Is.True);
					var local = new Rectangle(0, 0, layout.Bounds.Width, layout.Bounds.Height);
					for (var i = 0; i < 8; i++) Assert.That(local.Contains(layout.PrimarySlot(i)), Is.True);
					if (open)
						for (var i = 0; i < MacCommandDockLayout.Pages[page].Length; i++)
						{
							Assert.That(local.Contains(layout.DrawerSlot(i)), Is.True);
							Assert.That(layout.DrawerSlot(i).Bottom, Is.LessThan(layout.MainY));
						}
				}
		}

		[Test]
		public void MobileAndOtherPlatformsNeverUseMacDock()
		{
			foreach (PlatformType platform in Enum.GetValues(typeof(PlatformType)))
				Assert.That(MacCommandDockLayout.Enabled(platform), Is.EqualTo(platform == PlatformType.OSX));
		}
	}
}
