using NUnit.Framework;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Test
{
    [TestFixture]
    public sealed class IntroductionPromptPolicyTest
    {
        [TestCase("ra2", 0, false)]
        [TestCase("ra2", 1, false)]
        [TestCase("ra2", 2, false)]
        [TestCase("ra", 0, true)]
        [TestCase("ra", 1, false)]
        [TestCase("ra", 2, false)]
        public void NukeHourAlwaysStartsWithDefaultsAndOtherModsKeepTheirPrompt(string mod, int version, bool expected)
        {
            Assert.That(IntroductionPromptLogic.ShouldShowPrompt(mod, version), Is.EqualTo(expected));
        }
    }
}
