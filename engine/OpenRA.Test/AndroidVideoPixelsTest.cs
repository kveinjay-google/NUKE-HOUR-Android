using NUnit.Framework;
using OpenRA.Android;
namespace OpenRA.Test
{
[TestFixture]
public class AndroidVideoPixelsTest
{
    [Test]
    public void LimitedRangeBlackAndWhiteHaveOpaqueAlpha()
    {
        var output = new byte[8];
        AndroidVideoPixels.Convert420(new byte[] {16, 235}, new byte[] {128}, new byte[] {128},
            2, 1, 1, 1, 1, 0, 0, 2, 1, true, false, output);
        Assert.That(output, Is.EqualTo(new byte[] {0,0,0,255,255,255,255,255}));
    }
    [Test]
    public void InterleavedChromaStridesAndCropSkipPadding()
    {
        var output = new byte[8];
        AndroidVideoPixels.Convert420(new byte[] {16,16,16,16,16,16,63,63},
            new byte[] {128,99,102}, new byte[] {128,99,240},
            4, 4, 4, 2, 2, 2, 1, 2, 1, true, false, output);
        Assert.That(output, Is.EqualTo(new byte[] {0,1,255,255,0,1,255,255}));
    }
    [Test]
    public void FullRangeWhiteDoesNotUseLimitedRangeOffset()
    {
        var output = new byte[4];
        AndroidVideoPixels.Convert420(new byte[] {255}, new byte[] {128}, new byte[] {128},
            1, 1, 1, 1, 1, 0, 0, 1, 1, false, true, output);
        Assert.That(output, Is.EqualTo(new byte[] {255,255,255,255}));
    }
}

}
