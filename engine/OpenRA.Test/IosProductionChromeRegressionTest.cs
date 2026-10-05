using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class IosProductionChromeRegressionTest
	{
		[TestCase(0, -58)]
		[TestCase(27, -30)]
		public void FooterMatchesAuthoredSlotRegardlessOfCategoryOrigin(int parentX, int parentY)
		{
			var snapshot = new IosScreenSnapshot(new Size(1564, 720), new Size(956, 440), default);
			var layout = IosIngameSidebarLayoutPolicy.Create(snapshot);
			foreach (var up in new[] { false, true })
			{
				var method = typeof(IosIngameSidebarLayout).GetMethod("ScrollButtonLocalBounds",
					new[] { typeof(int), typeof(bool), typeof(int) });
				Assert.That(method, Is.Not.Null, "Footer conversion must include the category container X offset.");
				var bounds = (WidgetBounds)method.Invoke(layout, new object[] { parentY, up, parentX });
				Assert.That(bounds.X + parentX, Is.EqualTo(up ? 116 : 39));
				Assert.That(bounds.Y + parentY, Is.EqualTo(layout.BottomCapY + 7));
				Assert.That(bounds.Width, Is.EqualTo(77));
				Assert.That(bounds.Height, Is.EqualTo(27));
			}
		}

		[Test]
		public void EnlargedCategoryDrawsOneBakedIconWithoutTilingOrOverlay()
		{
			var button = (ButtonWidget)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ButtonWidget));
			typeof(Widget).GetField(nameof(Widget.Children)).SetValue(button, new System.Collections.Generic.List<Widget>());
			var icon = new ImageWidget { Id = "ICON" };
			button.AddChild(icon);
			var method = typeof(IosProductionCategoryPolicy).GetMethod("ApplySingleImageChrome");
			Assert.That(method, Is.Not.Null);
			method.Invoke(null, new object[] { button });
			Assert.That(button.StretchBackground, Is.True);
			Assert.That(icon.IsVisible(), Is.False);
		}

		[TestCase(234)]
		[TestCase(233)]
		public void FourCategoriesDivideWidthWithoutGapsOrOverlap(int width)
		{
			var bounds = IosProductionCategoryPolicy.ButtonBounds(width, true);
			Assert.That(bounds.Length, Is.EqualTo(4));
			Assert.That(bounds[0].X, Is.Zero);
			for (var i = 1; i < bounds.Length; i++)
				Assert.That(bounds[i].X, Is.EqualTo(bounds[i - 1].Right));
			Assert.That(bounds[3].Right, Is.EqualTo(width));
		}

		[Test]
		public void ClassicAndClassicHdUseFourWideCategoriesAndMergeVehicleQueues()
		{
			Assert.That(IosProductionCategoryPolicy.UsesConsolidatedCategories(InterfaceStyleMode.Classic), Is.True);
			Assert.That(IosProductionCategoryPolicy.UsesConsolidatedCategories(InterfaceStyleMode.ClassicHD), Is.True);
			Assert.That(IosProductionCategoryPolicy.VisibleGroups(false, true),
				Is.EqualTo(new[] { "Building", "Support", "Infantry", "Vehicle" }));
			Assert.That(IosProductionCategoryPolicy.SourceGroups("Vehicle", false, true),
				Is.EqualTo(new[] { "Vehicle", "Aircraft", "Ship" }));
			Assert.That(IosProductionCategoryPolicy.SourceGroups("Aircraft", false, true), Is.Empty);
			Assert.That(IosProductionCategoryPolicy.SourceGroups("Ship", false, true), Is.Empty);
			var bounds = IosProductionCategoryPolicy.ButtonBounds(180, false, true);
			Assert.That(bounds.Select(b => b.Width), Is.EqualTo(new[] { 45, 45, 45, 45 }));
		}

		[Test]
		public void TouchCategoriesUseAtLeastFortyFourPointTargets()
		{
			var bounds = IosProductionCategoryPolicy.TouchButtonBounds(234, false, true);
			Assert.That(bounds, Has.Length.EqualTo(4));
			Assert.That(bounds.All(b => b.Width >= 44 && b.Height >= 44), Is.True);
		}

		[Test]
		public void TabletCategoryVisualsFillIntegratedSlotsWhileTouchTargetsGrow()
		{
			var visuals = IosProductionCategoryPolicy.ButtonBounds(180, false, true);
			var targets = IosProductionCategoryPolicy.TouchButtonBounds(180, false, true);
			Assert.That(visuals.All(b => b.Height == 38), Is.True);
			Assert.That(targets.All(b => b.Height == 44), Is.True);
			Assert.That(targets.Select(b => b.Width), Is.EqualTo(visuals.Select(b => b.Width)));
		}

		[Test]
		public void TallerPhoneCategoriesDoNotCoverTheRepairAndSellRow()
		{
			var snapshot = new IosScreenSnapshot(new Size(1564, 720), new Size(956, 440), default);
			var layout = IosIngameSidebarLayoutPolicy.Create(snapshot);
			var tabs = IosProductionCategoryPolicy.ButtonBounds(layout.ProductionBounds.Width, true);
			Assert.That(layout.ProductionBounds.Top - tabs[0].Height, Is.EqualTo(245));
		}
	}
}
