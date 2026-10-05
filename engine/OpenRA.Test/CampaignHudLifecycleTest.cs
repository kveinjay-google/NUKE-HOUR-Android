using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
	[TestFixture]
	public class CampaignHudLifecycleTest
	{
		[TestCase(true, WinState.Lost, false)]
		[TestCase(true, WinState.Won, false)]
		[TestCase(true, WinState.Undefined, false)]
		[TestCase(false, WinState.Lost, true)]
		[TestCase(false, WinState.Won, true)]
		[TestCase(false, WinState.Undefined, false)]
		public void CampaignResultRetainsPlayerHudWhileSkirmishKeepsObserverTransition(bool mission, WinState state, bool expected)
		{
			Assert.That(LoadIngamePlayerOrObserverUILogic.ShouldSwitchToObserver(mission, state), Is.EqualTo(expected));
		}
	}
}
