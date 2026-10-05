using System.Linq;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public class PhoneRosterColumnsTest
	{
		[Test]
		public void PhoneSkirmishHasRoomForFiveWholeRowsWithoutUtilityStrip()
		{
			var policy = IosMenuLayoutPolicy.Create(true, 956, 440);
			var layout = IosLobbyLayout.Create(904, 388, policy, true,
				showHeader: false, hidePlayerUtility: true);
			Assert.That(layout.Chat.Height, Is.Zero);
			Assert.That(layout.Position.Height, Is.Zero);
			Assert.That(layout.PlayerRow.Height, Is.EqualTo(44));
			Assert.That(layout.PlayerName.Height, Is.EqualTo(layout.PlayerRow.Height));
			Assert.That(layout.PlayerFaction.Height, Is.EqualTo(layout.PlayerRow.Height));
			Assert.That(layout.PlayerList.Height, Is.GreaterThanOrEqualTo(5 * layout.PlayerRow.Height + 4 * layout.PlayerRowSpacing));
			Assert.That(layout.Players.Bottom, Is.LessThan(layout.Footer.Y));
		}

		[TestCase(956, 440, false)]
		[TestCase(1180, 820, true)]
		public void OtherRoomLayoutsRetainUtility(int width, int height, bool skirmish)
		{
			var policy = IosMenuLayoutPolicy.Create(true, width, height);
			var layout = IosLobbyLayout.Create(width, height, policy, skirmish, hidePlayerUtility: true);
			Assert.That(layout.HidePlayerUtility, Is.False);
			Assert.That(layout.Chat.Height, Is.GreaterThan(0));
			Assert.That(layout.Position.Height, Is.GreaterThan(0));
		}

		[TestCase(true)]
		[TestCase(false)]
		public void PhoneUsesMeasuredContentWithoutInflatingName(bool skirmish)
		{
			var policy = IosMenuLayoutPolicy.Create(true, 956, 440);
			var preferred = new[] { 160, 48, 110, 48, 72, 48 };
			var layout = IosLobbyLayout.Create(956, 440, policy, skirmish,
				false, true, true, false, preferred, true);
			Assert.That(layout.PlayerScrollbarWidth, Is.Zero);
			Assert.That(layout.PlayerSpawn.Width, Is.EqualTo(72));
			Assert.That(layout.PlayerName.Width, skirmish ? Is.GreaterThanOrEqualTo(160) : Is.EqualTo(160));
			Assert.That(layout.PlayerFaction.Width, skirmish ? Is.GreaterThanOrEqualTo(110) : Is.EqualTo(110));
			Assert.That(layout.PlayerReady.Right, Is.EqualTo(layout.PlayerRow.Width));
			if (skirmish)
				Assert.That(layout.PlayerReady.Width, Is.Zero);
		}

		[Test]
		public void PhoneSkirmishPrioritizesRosterWithoutInflatingOnlyName()
		{
			var policy = IosMenuLayoutPolicy.Create(true, 956, 440);
			var layout = IosLobbyLayout.Create(956, 440, policy, true,
				false, true, true, false, new[] { 160, 48, 110, 48, 72, 48 }, true);
			Assert.That(layout.Players.Width, Is.GreaterThanOrEqualTo((956 - layout.Gap) * 3 / 4));
			Assert.That(layout.Map.Width, Is.LessThanOrEqualTo(956 / 4 + 1));
			Assert.That(layout.PlayerName.Width, Is.LessThan(layout.PlayerRow.Width / 2));
			Assert.That(layout.PlayerColor.Width, Is.EqualTo(48));
			Assert.That(layout.PlayerTeam.Width, Is.EqualTo(48));
			Assert.That(layout.PlayerSpawn.Width, Is.EqualTo(72));
			Assert.That(layout.PlayerFaction.Width, Is.GreaterThan(110));
		}

		[Test]
		public void PhoneBorrowsMapWidthBeforeCompressingMeasuredContent()
		{
			var policy = IosMenuLayoutPolicy.Create(true, 956, 440);
			var preferred = new[] { 300, 48, 160, 60, 80, 48 };
			var layout = IosLobbyLayout.Create(956, 440, policy, false,
				false, true, true, false, preferred, true);
			Assert.That(layout.PlayerRow.Width, Is.GreaterThanOrEqualTo(preferred.Sum()));
			Assert.That(layout.PlayerName.Width, Is.GreaterThanOrEqualTo(preferred[0]));
			Assert.That(layout.PlayerSpawn.Width, Is.EqualTo(preferred[4]));
		}

		[Test]
		public void PhoneDoesNotSacrificeNameBeforeOtherColumnsWhenConstrained()
		{
			var policy = IosMenuLayoutPolicy.Create(true, 844, 390);
			var preferred = new[] { 260, 48, 180, 48, 90, 48 };
			var layout = IosLobbyLayout.Create(844, 390, policy, false,
				false, true, true, false, preferred, true);
			Assert.That(layout.PlayerName.Width, Is.GreaterThan(layout.PlayerFaction.Width));
			Assert.That(layout.PlayerName.Width, Is.GreaterThan(2 * policy.MinimumTarget));
			var columns = new[] { layout.PlayerName, layout.PlayerColor, layout.PlayerFaction,
				layout.PlayerTeam, layout.PlayerSpawn, layout.PlayerReady };
			Assert.That(columns.All(c => c.Width >= policy.MinimumTarget), Is.True);
			for (var i = 1; i < columns.Length; i++)
				Assert.That(columns[i].X, Is.EqualTo(columns[i - 1].Right));
		}
	}
}
