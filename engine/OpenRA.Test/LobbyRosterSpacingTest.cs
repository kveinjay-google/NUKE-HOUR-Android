using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Widgets;

namespace OpenRA.Test
{
	[TestFixture]
	public class LobbyRosterSpacingTest
	{
		[Test]
		public void PlayerRosterUsesGestureScrollingWithoutAVisibleArrowColumn()
		{
			var policy = IosMenuLayoutPolicy.Create(true, 1440, 900);
			var layout = IosLobbyLayout.Create(1440, 900, policy, true);
#pragma warning disable SYSLIB0050
			var scroll = (ScrollPanelWidget)FormatterServices.GetUninitializedObject(typeof(ScrollPanelWidget));
#pragma warning restore SYSLIB0050
			typeof(Widget).GetField(nameof(Widget.Children))!.SetValue(scroll, new List<Widget>());
			scroll.Layout = new ListLayout(scroll);
			var configure = typeof(LobbyLogic).GetMethod("ConfigurePlayerScrollPanel", BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(configure, Is.Not.Null);
			configure!.Invoke(null, new object[] { scroll, layout, policy });
			var row = new ContainerWidget { Bounds = layout.PlayerRow };
			scroll.AddChild(row);
			Assert.That(row.Bounds.Y, Is.Zero);
			Assert.That(scroll.ScrollBar, Is.EqualTo(ScrollBar.Hidden));
			Assert.That(scroll.ScrollbarWidth, Is.Zero);
			Assert.That(scroll.EnableContentDragging, Is.True);
			Assert.That(row.Bounds.Width, Is.EqualTo(layout.Players.Width));
			Assert.That(scroll.Bounds.Y, Is.EqualTo(layout.PlayerList.Y + policy.Gap));
		}

		[TestCase(1180, 820)]
		[TestCase(1440, 900)]
		public void RemovedRosterArrowWidthIsAssignedToTheNameColumn(int width, int height)
		{
			var policy = IosMenuLayoutPolicy.Create(true, width, height);
			var layout = IosLobbyLayout.Create(width, height, policy, true);
			var reclaimed = policy.MinimumTarget + layout.Gap;
			Assert.That(layout.PlayerRow.Width, Is.EqualTo(layout.Players.Width));
			Assert.That(layout.PlayerName.Width - layout.PlayerSpawn.Width, Is.EqualTo(reclaimed).Within(1));
			Assert.That(layout.PlayerFaction.Width, Is.EqualTo(layout.PlayerSpawn.Width).Within(1));
			Assert.That(layout.PlayerSpawn.Height, Is.EqualTo(policy.MinimumTarget));
		}
	}
}
