// Copyright (c) The OpenRA Developers and Contributors
// This file is part of OpenRA, which is free software. It is made
// available to you under the terms of the GNU General Public License
// as published by the Free Software Foundation, either version 3 of
// the License, or (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Common.Widgets
{
	public enum ScatterFormUpState
	{
		Scatter,
		FormUp,
	}

	public static class ScatterFormUpTogglePolicy
	{
		public static ScatterFormUpState Next(ScatterFormUpState current, bool commandIssued)
		{
			if (!commandIssued)
				return current;

			return current == ScatterFormUpState.Scatter ? ScatterFormUpState.FormUp : ScatterFormUpState.Scatter;
		}
	}

	public readonly struct FormationAssignment<T>
	{
		public readonly T Unit;
		public readonly CPos Cell;

		public FormationAssignment(T unit, CPos cell)
		{
			Unit = unit;
			Cell = cell;
		}
	}

	public static class FormationOrderPolicy
	{
		public static IReadOnlyList<CVec> CreateOffsets(int count)
		{
			if (count <= 0)
				return Array.Empty<CVec>();

			var columns = (int)Math.Ceiling(Math.Sqrt(count));
			var rows = (int)Math.Ceiling((double)count / columns);
			var xCenter = columns / 2;
			var yCenter = rows / 2;

			return Enumerable.Range(0, columns)
				.SelectMany(x => Enumerable.Range(0, rows).Select(y => new CVec(x - xCenter, y - yCenter)))
				.OrderBy(offset => offset.LengthSquared)
				.ThenBy(offset => offset.Y)
				.ThenBy(offset => offset.X)
				.Take(count)
				.ToArray();
		}

		public static IReadOnlyList<FormationAssignment<T>> Assign<T>(
			IEnumerable<T> units,
			Func<T, uint> stableId,
			Func<T, CPos> currentCell,
			CPos center,
			Func<T, CPos, bool> canUseCell,
			int searchRadius = 4)
		{
			if (units == null)
				throw new ArgumentNullException(nameof(units));
			if (stableId == null)
				throw new ArgumentNullException(nameof(stableId));
			if (currentCell == null)
				throw new ArgumentNullException(nameof(currentCell));
			if (canUseCell == null)
				throw new ArgumentNullException(nameof(canUseCell));

			var orderedUnits = units.OrderBy(stableId).ToArray();
			if (orderedUnits.Length == 0)
				return Array.Empty<FormationAssignment<T>>();

			searchRadius = Math.Max(0, searchRadius);
			var ideals = CreateOffsets(orderedUnits.Length).Select(offset => center + offset).ToArray();
			var candidateCells = ideals
				.SelectMany((ideal, idealIndex) => SearchOffsets(searchRadius)
					.Select(delta => new CandidateCell(ideal + delta, idealIndex, delta.LengthSquared)))
				.GroupBy(candidate => candidate.Cell)
				.Select(group => group
					.OrderBy(candidate => candidate.DisplacementSquared)
					.ThenBy(candidate => candidate.IdealIndex)
					.First())
				.ToArray();

			var usedCells = new HashSet<CPos>();
			var assignments = new List<FormationAssignment<T>>(orderedUnits.Length);
			foreach (var unit in orderedUnits)
			{
				var source = currentCell(unit);
				var target = candidateCells
					.Where(candidate => !usedCells.Contains(candidate.Cell) && canUseCell(unit, candidate.Cell))
					.OrderBy(candidate => (candidate.Cell - source).LengthSquared)
					.ThenBy(candidate => candidate.DisplacementSquared)
					.ThenBy(candidate => candidate.IdealIndex)
					.ThenBy(candidate => candidate.Cell.Y)
					.ThenBy(candidate => candidate.Cell.X)
					.Select(candidate => (CPos?)candidate.Cell)
					.FirstOrDefault();

				if (!target.HasValue)
					continue;

				usedCells.Add(target.Value);
				assignments.Add(new FormationAssignment<T>(unit, target.Value));
			}

			return assignments;
		}

		static IEnumerable<CVec> SearchOffsets(int radius)
		{
			for (var y = -radius; y <= radius; y++)
			{
				for (var x = -radius; x <= radius; x++)
					yield return new CVec(x, y);
			}
		}

		readonly struct CandidateCell
		{
			public readonly CPos Cell;
			public readonly int IdealIndex;
			public readonly int DisplacementSquared;

			public CandidateCell(CPos cell, int idealIndex, int displacementSquared)
			{
				Cell = cell;
				IdealIndex = idealIndex;
				DisplacementSquared = displacementSquared;
			}
		}
	}
}
