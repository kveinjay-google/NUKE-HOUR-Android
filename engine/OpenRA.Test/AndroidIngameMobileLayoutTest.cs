#region Copyright & License Information
/*
 * This file is part of OpenRA, which is free software. It is made
 * available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.RA2.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class AndroidIngameMobileLayoutTest
	{
		[Test]
		public void PhoneSidebarUniformlyScalesTheCompleteOriginalPanelToScreenHeight()
		{
			var layout = AndroidIngameMobileLayout.Create(new Size(2400, 1080));

			Assert.Multiple(() =>
			{
				Assert.That(layout.TopBarBounds, Is.EqualTo(new Rectangle(2006, 0, 394, 463)));
				Assert.That(layout.SidebarBounds, Is.EqualTo(new Rectangle(2006, 463, 394, 617)));
				Assert.That(layout.PaletteBounds, Is.EqualTo(new Rectangle(40, 3, 309, 504)));
				Assert.That(layout.IconSize, Is.EqualTo(new int2(101, 81)));
				Assert.That(layout.Rows, Is.EqualTo(6));
				Assert.That(layout.TopBarBounds.Width, Is.EqualTo(layout.SidebarBounds.Width));
				Assert.That(layout.TopBarBounds.Bottom, Is.EqualTo(layout.SidebarBounds.Top));
				Assert.That(layout.SidebarBounds.Bottom, Is.EqualTo(1080));
			});
		}

		[TestCase(320, 200)]
		[TestCase(1080, 720)]
		[TestCase(2400, 1080)]
		public void UniformlyScaledPhoneSidebarNeverEscapesTheScreen(int width, int height)
		{
			var layout = AndroidIngameMobileLayout.Create(new Size(width, height));
			var screen = new Rectangle(0, 0, width, height);

			Assert.Multiple(() =>
			{
				Assert.That(screen.Contains(layout.SidebarBounds), Is.True);
				Assert.That(screen.Contains(layout.TopBarBounds), Is.True);
				Assert.That(layout.TopBarBounds.Width, Is.EqualTo(layout.SidebarBounds.Width));
				Assert.That(layout.TopBarBounds.Bottom, Is.EqualTo(layout.SidebarBounds.Top));
				Assert.That(layout.Rows, Is.EqualTo(6));
				Assert.That(layout.IconSize.X, Is.GreaterThan(0));
				Assert.That(layout.IconSize.Y, Is.GreaterThan(0));
			});
		}
	}
}
