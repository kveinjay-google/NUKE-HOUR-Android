using System;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class LobbyMusicPlayerLayoutTest
	{
		[TestCase(1200, 550)]
		[TestCase(812, 270)]
		[TestCase(440, 400)]
		public void PlayerControlsStayCenteredInsideTheirOwnCard(int width, int height)
		{
			var type = typeof(LobbyLogic).Assembly.GetType("OpenRA.Mods.Common.Widgets.Logic.LobbyMusicPlayerLayout");
			Assert.That(type, Is.Not.Null);
			var policy = IosMenuLayoutPolicy.Create(true, width, height);
			var layout = Activator.CreateInstance(type!, width, height, policy);
			Assert.That(layout, Is.Not.Null);
			Rectangle Get(string name) => (Rectangle)type!.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)!.GetValue(layout)!;
			var card = Get("PlayerCard");
			var buttons = Get("Buttons");
			var icon = Get("Icon");
			var title = Get("Title");
			var time = Get("Time");
			var slider = Get("VolumeSlider");
			var list = Get("TrackList");
			var content = Get("PlayerContent");
			var screen = new Rectangle(0, 0, width, height);
			Assert.That(screen.Contains(card), Is.True);
			Assert.That(screen.Contains(list), Is.True);
			Assert.That(card.IntersectsWith(list), Is.False);
			Assert.That(buttons.Left + buttons.Right, Is.EqualTo(card.Width));
			Assert.That(new Rectangle(0, 0, policy.MinimumTarget, policy.MinimumTarget).Contains(icon), Is.True);
			Assert.That(icon.Left + icon.Right, Is.EqualTo(policy.MinimumTarget));
			Assert.That(icon.Top + icon.Bottom, Is.EqualTo(policy.MinimumTarget));
			foreach (var bounds in new[] { title, time, buttons, slider })
				Assert.That(content.Contains(bounds), Is.True, bounds.ToString());
			Assert.That(buttons.Height, Is.GreaterThanOrEqualTo(policy.MinimumTarget));
			Assert.That(slider.Height, Is.GreaterThanOrEqualTo(policy.MinimumTarget));
			if (width == 440)
			{
				Assert.That(card.Left + card.Right, Is.EqualTo(width));
				Assert.That(list.Left, Is.EqualTo(card.Left));
				Assert.That(list.Width, Is.EqualTo(card.Width));
			}
		}
	}
}
