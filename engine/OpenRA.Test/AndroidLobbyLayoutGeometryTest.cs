#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public class AndroidLobbyLayoutGeometryTest
	{
		// (usableWidthDp, usableHeightDp) profiles from the phone UI contract.
		static readonly object[] ProfileCases =
		{
			new object[] { 800f, 360f },   // CompactPhoneLandscape
			new object[] { 891f, 411f },   // PhoneLandscape
			new object[] { 960f, 432f },   // PhoneLandscape
			new object[] { 960f, 540f },   // Foldable
			new object[] { 1158f, 411f }   // S10-sized phone (measured)
		};

		static readonly float[] DpToUiFactors = { 1f, 1.6f, 2.1875f, 3f };

		[TestCaseSource(nameof(ProfileCases))]
		public void BarsRespectContract(float usableWidthDp, float usableHeightDp)
		{
			foreach (var factor in DpToUiFactors)
			{
				var layout = AndroidLobbyLayout.ComputeLayout(usableWidthDp, usableHeightDp, factor);

				var headerDp = layout.HeaderHeight / factor;
				var tabDp = layout.TabHeight / factor;
				var footerDp = (layout.ContentHeight - layout.FooterY) / factor;
				Assert.That(headerDp, Is.InRange(48f - 0.02f, 56f + 0.02f), "title bar must be 48-56dp");
				Assert.That(tabDp, Is.InRange(48f - 0.02f, 52f + 0.02f), "tab row must be 48-52dp");
				Assert.That(footerDp, Is.InRange(56f - 0.02f, 64f + 0.02f), "footer must be 56-64dp");
			}
		}

		[TestCaseSource(nameof(ProfileCases))]
		public void PlayersMapSplitStaysInContract(float usableWidthDp, float usableHeightDp)
		{
			foreach (var factor in DpToUiFactors)
			{
				var layout = AndroidLobbyLayout.ComputeLayout(usableWidthDp, usableHeightDp, factor);

				var playerFraction = layout.PlayerWidth / (float)layout.ContentWidth;
				Assert.That(playerFraction, Is.InRange(0.62f - 0.001f, 0.66f + 0.001f),
					"players column must be 62-66% of the content width");
				Assert.That(layout.MapWidth, Is.GreaterThan(0), "map column must exist");
				Assert.That(layout.MapX, Is.GreaterThan(layout.PlayerWidth), "map must start after the players column");
			}
		}

		[TestCaseSource(nameof(ProfileCases))]
		public void FooterIsNotEqualThirds(float usableWidthDp, float usableHeightDp)
		{
			foreach (var factor in DpToUiFactors)
			{
				var layout = AndroidLobbyLayout.ComputeLayout(usableWidthDp, usableHeightDp, factor);
				var backWidth = layout.BackCell.Width;
				var startWidth = layout.StartCell.Width;
				Assert.That(backWidth, Is.GreaterThan(0));
				Assert.That(startWidth, Is.GreaterThan(backWidth),
					"the Start (primary) control must be wider than the Back (secondary) control");
				Assert.That(Math.Abs(backWidth - startWidth), Is.GreaterThan(layout.Gap),
					"footer controls must not look equivalent");
				Assert.That(layout.BackCell.Height, Is.GreaterThanOrEqualTo(layout.FooterHeight - 1));
				Assert.That(layout.StartCell.Height, Is.GreaterThanOrEqualTo(layout.FooterHeight - 1));
			}
		}

		[TestCaseSource(nameof(ProfileCases))]
		public void PlayerCardRowsDoNotOverlap(float usableWidthDp, float usableHeightDp)
		{
			foreach (var factor in DpToUiFactors)
			{
				var layout = AndroidLobbyLayout.ComputeLayout(usableWidthDp, usableHeightDp, factor);
				var rowHeight = layout.CardRowHeight;

				Assert.That(rowHeight / factor, Is.InRange(88f - 0.02f, 104f + 0.02f),
					"card height must be 88-104dp");
				Assert.That(layout.Line1Height, Is.InRange(44f * factor - 1, 48f * factor + 1),
					"line 1 must be ~44-48dp");
				Assert.That(layout.CardRowHeight - layout.Line1Height, Is.InRange(44f * factor - 1, 52f * factor + 1),
					"line 2 must be ~44-52dp");

				// Line 1 cells must tile without overlap: name | color | ready.
				Assert.That(layout.NameCell.Right, Is.LessThanOrEqualTo(layout.ColorCell.X), "name/color overlap");
				Assert.That(layout.ColorCell.Right, Is.LessThanOrEqualTo(layout.ReadyCell.X), "color/ready overlap");
				Assert.That(layout.ReadyCell.Right, Is.LessThanOrEqualTo(layout.CardPadding + (layout.PlayerWidth - 2 * layout.CardPadding) + 1),
					"ready cell must end within the content width");

				// Line 2 cells tile: faction | spawn | team.
				Assert.That(layout.FactionCell.Right, Is.LessThanOrEqualTo(layout.SpawnCell.X), "faction/spawn overlap");
				Assert.That(layout.SpawnCell.Right, Is.LessThanOrEqualTo(layout.TeamCell.X), "spawn/team overlap");

				// All interactive cells must meet the minimum touch target.
				Assert.That(layout.NameCell.Width, Is.GreaterThanOrEqualTo(48f * factor - 1));
				Assert.That(layout.ColorCell.Width, Is.GreaterThanOrEqualTo(48f * factor - 1));
				Assert.That(layout.ReadyCell.Width, Is.GreaterThanOrEqualTo(48f * factor - 1));
				Assert.That(layout.FactionCell.Width, Is.GreaterThanOrEqualTo(48f * factor - 1));
				Assert.That(layout.TeamCell.Width, Is.GreaterThanOrEqualTo(48f * factor - 1));
				Assert.That(layout.SpawnCell.Width, Is.GreaterThanOrEqualTo(48f * factor - 1));
			}
		}

		[Test]
		public void ToolbarSitsAboveMapContentInsideTheMapColumn()
		{
			var layout = AndroidLobbyLayout.ComputeLayout(1158f, 411f, 2.1875f);
			Assert.That(layout.ToolbarY, Is.EqualTo(layout.MainY));
			Assert.That(layout.ToolbarWidth, Is.EqualTo(layout.MapWidth));
			Assert.That(layout.MapContentY, Is.GreaterThan(layout.ToolbarY + layout.ToolbarHeight));
			Assert.That(layout.MapContentHeight, Is.GreaterThan(0));
			Assert.That(layout.ToolbarY + layout.ToolbarHeight, Is.LessThanOrEqualTo(layout.FooterY),
				"toolbar must stay above the footer");
		}
	}
}
