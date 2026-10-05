// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class FormationOrderPolicyTest
	{
		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		[TestCase(5)]
		[TestCase(9)]
		public void CreatesCompactUniqueDeterministicOffsets(int count)
		{
			var first = FormationOrderPolicy.CreateOffsets(count);
			var second = FormationOrderPolicy.CreateOffsets(count);

			Assert.That(first, Has.Count.EqualTo(count));
			Assert.That(first.Distinct().Count(), Is.EqualTo(count));
			Assert.That(second, Is.EqualTo(first));

			if (count == 0)
				return;

			var width = first.Max(offset => offset.X) - first.Min(offset => offset.X) + 1;
			var height = first.Max(offset => offset.Y) - first.Min(offset => offset.Y) + 1;
			Assert.That(System.Math.Abs(width - height), Is.LessThanOrEqualTo(1));
			Assert.That(first.Min(offset => offset.LengthSquared), Is.EqualTo(0));
		}

		[Test]
		public void AssignsNearestUniqueLegalCellsInStableUnitOrder()
		{
			var units = new[]
			{
				new TestUnit(30, new CPos(12, 10)),
				new TestUnit(10, new CPos(8, 10)),
				new TestUnit(20, new CPos(10, 10)),
			};

			var blocked = new HashSet<CPos> { new(10, 10) };
			var assignments = FormationOrderPolicy.Assign(
				units,
				unit => unit.Id,
				unit => unit.Cell,
				new CPos(10, 10),
				(_, cell) => !blocked.Contains(cell),
				searchRadius: 2);

			Assert.That(assignments.Select(assignment => assignment.Unit.Id), Is.EqualTo(new uint[] { 10, 20, 30 }));
			Assert.That(assignments.Select(assignment => assignment.Cell).Distinct().Count(), Is.EqualTo(3));
			Assert.That(assignments.All(assignment => !blocked.Contains(assignment.Cell)), Is.True);
			Assert.That(assignments, Is.EqualTo(FormationOrderPolicy.Assign(
				units,
				unit => unit.Id,
				unit => unit.Cell,
				new CPos(10, 10),
				(_, cell) => !blocked.Contains(cell),
				searchRadius: 2)));
		}

		[Test]
		public void OmitsUnitWhenNoLegalDestinationExists()
		{
			var unit = new TestUnit(1, new CPos(4, 4));
			var assignments = FormationOrderPolicy.Assign(
				new[] { unit },
				candidate => candidate.Id,
				candidate => candidate.Cell,
				unit.Cell,
				(_, _) => false,
				searchRadius: 1);

			Assert.That(assignments, Is.Empty);
		}

		[Test]
		public void ScatterAndFormUpAlternateOnlyAfterSuccessfulCommands()
		{
			var state = ScatterFormUpState.Scatter;
			state = ScatterFormUpTogglePolicy.Next(state, commandIssued: false);
			Assert.That(state, Is.EqualTo(ScatterFormUpState.Scatter));

			state = ScatterFormUpTogglePolicy.Next(state, commandIssued: true);
			Assert.That(state, Is.EqualTo(ScatterFormUpState.FormUp));

			state = ScatterFormUpTogglePolicy.Next(state, commandIssued: true);
			Assert.That(state, Is.EqualTo(ScatterFormUpState.Scatter));
		}

		[Test]
		public void DynamicButtonIconNameSurvivesFactionSkinRebind()
		{
			var formUp = false;
			var button = (ButtonWidget)FormatterServices.GetUninitializedObject(typeof(ButtonWidget));
			typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(button, new List<Widget>());
			var icon = new ImageWidget { Id = "ICON", ImageName = "scatter" };
			button.AddChild(icon);

			WidgetUtils.BindButtonIcon(button, () => formUp ? "form-up" : "scatter");
			Assert.That(icon.GetImageName(), Is.EqualTo("scatter"));

			formUp = true;
			WidgetUtils.BindButtonIcon(button);
			Assert.That(icon.GetImageName(), Is.EqualTo("form-up"),
				"Refreshing the faction icon collection must not replace the dynamic Scatter/Form Up provider.");
		}

		sealed class TestUnit
		{
			public readonly uint Id;
			public readonly CPos Cell;

			public TestUnit(uint id, CPos cell)
			{
				Id = id;
				Cell = cell;
			}
		}
	}
}
