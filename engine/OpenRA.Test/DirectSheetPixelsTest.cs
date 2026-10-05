using System;
using System.IO;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Primitives;
namespace OpenRA.Test
{
    [TestFixture, NonParallelizable]
    public class DirectSheetPixelsTest
    {
        [Test]
        public void AdoptsPremultipliedBufferWithoutCopy()
        {
            var previous = Sheet.PlatformBgraDecoder;
            var pixels = new byte[] { 5, 10, 15, 128 };
            try
            {
                Sheet.PlatformBgraDecoder = _ => (pixels, new Size(1, 1));
                using var sheet = new Sheet(SheetType.BGRA, new MemoryStream());
                Assert.That(sheet.GetData(), Is.SameAs(pixels));
            }
            finally { Sheet.PlatformBgraDecoder = previous; }
        }

        [TestCase(0, 1, 4)]
        [TestCase(-1, 1, 4)]
        [TestCase(int.MaxValue, int.MaxValue, 4)]
        [TestCase(2, 2, 4)]
        public void RejectsInvalidDecodedBuffer(int width, int height, int length)
        {
            var previous = Sheet.PlatformBgraDecoder;
            try
            {
                Sheet.PlatformBgraDecoder = _ => (new byte[length], new Size(width, height));
                Assert.Throws<InvalidDataException>(() => new Sheet(SheetType.BGRA, new MemoryStream()));
            }
            finally { Sheet.PlatformBgraDecoder = previous; }
        }
    }
}
