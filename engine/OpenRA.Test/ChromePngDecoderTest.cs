using System.IO;
using NUnit.Framework;
using OpenRA.FileFormats;
using OpenRA.Graphics;
namespace OpenRA.Test
{
    [TestFixture]
    public class ChromePngDecoderTest
    {
        [Test]
        public void SheetUsesPlatformDecoderWithoutChangingPixelChannels()
        {
            var previous = Sheet.PlatformPngDecoder;
            try
            {
                var called = false;
                Sheet.PlatformPngDecoder = stream => { called = true; return new Png(new byte[] { 30, 20, 10, 128 }, SpriteFrameType.Rgba32, 1, 1); };
                using var sheet = new Sheet(SheetType.BGRA, new MemoryStream());
                Assert.That(called, Is.True);
                Assert.That(sheet.GetData(), Is.EqualTo(new byte[] { 5, 10, 15, 128 }));
            }
            finally { Sheet.PlatformPngDecoder = previous; }
        }
    }
}
