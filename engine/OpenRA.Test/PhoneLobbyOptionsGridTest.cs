using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class PhoneLobbyOptionsGridTest
	{
		[Test]
		public void OptionsFlowAcrossTemplateRowsInFourColumns()
		{
			var options = new ContainerWidget();
			for (var i = 0; i < 6; i++)
			{
				var row = new ContainerWidget();
				for (var j = 0; j < 2; j++)
					row.AddChild(BareControl<CheckboxWidget>());
				options.AddChild(row);
			}
			var height = IosTouchMenuLogic.LayoutPhoneOptionGrid(options, 800, 44, 20, 4);
			Assert.That(height, Is.EqualTo(140));
			Assert.That(options.Children[1].Children[1].Bounds.X, Is.EqualTo(603));
			Assert.That(options.Children[2].Children[0].Bounds.Y, Is.EqualTo(48));
			Assert.That(options.Children[5].Children[1].Bounds.Bottom, Is.EqualTo(height));
		}

		static T BareControl<T>() where T : Widget
		{
			#pragma warning disable SYSLIB0050
			var control = (T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(T));
			#pragma warning restore SYSLIB0050
			typeof(Widget).GetField(nameof(Widget.Children)).SetValue(control, new System.Collections.Generic.List<Widget>());
			control.IsVisible = () => true;
			return control;
		}

		[Test]
		public void StandardOptionsFitWithoutClippingLabelsOrControls()
		{
			var options = new ContainerWidget();
			for (var i = 0; i < 4; i++)
			{
				var row = new ContainerWidget();
				for (var j = 0; j < 3; j++)
					row.AddChild(BareControl<CheckboxWidget>());
				options.AddChild(row);
			}
			for (var i = 0; i < 2; i++)
			{
				var row = new ContainerWidget();
				for (var j = 0; j < 3; j++)
				{
					var button = BareControl<DropDownButtonWidget>();
					button.Id = j.ToString();
					row.AddChild(button);
					var label = BareControl<LabelWidget>();
					label.Id = button.Id + "_DESC";
					row.AddChild(label);
				}
				options.AddChild(row);
			}
			var height = IosTouchMenuLogic.LayoutPhoneOptionGrid(options, 800, 44, 16, 4);
			Assert.That(height, Is.LessThanOrEqualTo(280));
			foreach (var row in options.Children)
				foreach (var child in row.Children)
				{
					Assert.That(child.Bounds.Right, Is.LessThanOrEqualTo(800));
					Assert.That(child.Bounds.Bottom, Is.LessThanOrEqualTo(height));
				}
		}

		[Test]
		public void ResetDefaultsOccupiesTheVacantCellBelowTechLevel()
		{
			var options = new ContainerWidget();
			var dropdowns = new System.Collections.Generic.List<DropDownButtonWidget>();
			for (var i = 0; i < 7; i++)
			{
				var row = new ContainerWidget();
				var dropdown = BareControl<DropDownButtonWidget>();
				dropdowns.Add(dropdown);
				row.AddChild(dropdown);
				options.AddChild(row);
			}

			var resetRow = new ContainerWidget();
			var reset = BareControl<ButtonWidget>();
			reset.Id = "RESET_OPTIONS_BUTTON";
			resetRow.AddChild(reset);
			options.AddChild(resetRow);

			var height = IosTouchMenuLogic.LayoutPhoneOptionGrid(options, 800, 44, 16, 4);
			var techLevel = dropdowns[3];
			Assert.Multiple(() =>
			{
				Assert.That(reset.Bounds.X, Is.EqualTo(techLevel.Bounds.X));
				Assert.That(reset.Bounds.Width, Is.EqualTo(techLevel.Bounds.Width));
				Assert.That(reset.Bounds.Y, Is.GreaterThan(techLevel.Bounds.Bottom));
				Assert.That(reset.Bounds.Bottom, Is.EqualTo(height));
			});
		}

		[TestCase(812, 375, 812, 375)]
		[TestCase(1558, 720, 844, 390)]
		[TestCase(1560, 720, 932, 430)]
		public void StandardSkirmishOptionsFitThePhonePanelWithoutScrolling(
			int width, int height, int nativeWidth, int nativeHeight)
		{
			var snapshot = new IosScreenSnapshot(new Size(width, height), new Size(nativeWidth, nativeHeight), default);
			var policy = IosMenuLayoutPolicy.Create(true, snapshot);
			var layout = IosLobbyLayout.Create(width, height, policy, true, true, false, false, false);
			var options = new ContainerWidget();
			for (var i = 0; i < 7; i++)
			{
				var row = new ContainerWidget();
				row.AddChild(BareControl<CheckboxWidget>());
				options.AddChild(row);
			}

			for (var i = 0; i < 7; i++)
			{
				var row = new ContainerWidget();
				row.AddChild(BareControl<DropDownButtonWidget>());
				options.AddChild(row);
			}

			var resetRow = new ContainerWidget();
			var reset = BareControl<ButtonWidget>();
			reset.Id = "RESET_OPTIONS_BUTTON";
			resetRow.AddChild(reset);
			options.AddChild(resetRow);
			var contentHeight = IosTouchMenuLogic.LayoutPhoneOptionGrid(options, layout.Players.Width - 2 * policy.Gap,
				policy.MinimumTarget, policy.MinimumReadableTextHeight, System.Math.Max(1, (policy.Gap - 1) / 2));
			Assert.That(contentHeight, Is.LessThanOrEqualTo(layout.Players.Height));
		}
	}
}
