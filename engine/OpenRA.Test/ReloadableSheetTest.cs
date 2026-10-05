using System;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Primitives;
namespace OpenRA.Test
{
    [TestFixture, NonParallelizable]
    public class ReloadableSheetTest
    {
        sealed class Texture : ITexture
        {
            public int Disposals;
            public Size Size { get; } = new Size(1, 1);
            public TextureScaleFilter ScaleFilter { get; set; }
            public byte[] GetData() => new byte[] { 5, 10, 15, 128 };
            public void Dispose() { Disposals++; }
            public void SetData(byte[] data, int width, int height) { }
            public void SetFloatData(float[] data, int width, int height) { }
            public void SetDataFromReadBuffer(Rectangle rect) { }
        }
        [Test]
        public void EvictionKeepsSpriteIdentityAndReloadsExactlyOnce()
        {
            var first = new Texture(); var second = new Texture(); var loads = 0;
            using var sheet = new Sheet(SheetType.BGRA, first);
            sheet.SetTextureReload(() => { loads++; return second; });
            var sprite = new Sprite(sheet, new Rectangle(0, 0, 1, 1), TextureChannel.RGBA);
            Assert.That(sheet.TryEvictTexture(-1), Is.False);
            Assert.That(sheet.TryEvictTexture(long.MaxValue), Is.True);
            Assert.That(sheet.HasResidentTexture, Is.False);
            Assert.That(first.Disposals, Is.EqualTo(1));
            Assert.That(sheet.TryEvictTexture(long.MaxValue), Is.False);
            Assert.That(sprite.Sheet, Is.SameAs(sheet));
            Assert.That(sprite.Sheet.GetTexture(), Is.SameAs(second));
            Assert.That(sheet.GetTexture(), Is.SameAs(second));
            Assert.That(loads, Is.EqualTo(1));
        }
        [Test]
        public void BufferAccessAfterEvictionReloadsPixelsAndPreventsLosingEdits()
        {
            using var sheet = new Sheet(SheetType.BGRA, new Texture());
            sheet.SetTextureReload(() => new Texture());
            sheet.TryEvictTexture(long.MaxValue);
            Assert.That(sheet.GetData(), Is.EqualTo(new byte[] { 5, 10, 15, 128 }));
            sheet.GetData()[0] = 99;
            sheet.CommitBufferedData();
            Assert.That(sheet.TryEvictTexture(long.MaxValue), Is.False);
            Assert.That(sheet.GetData()[0], Is.EqualTo(99));
        }
        [Test]
        public void DisposedSheetCannotReloadAndDisposalIsIdempotent()
        {
            var texture = new Texture();
            var sheet = new Sheet(SheetType.BGRA, texture);
            sheet.SetTextureReload(() => throw new Exception("must not reload"));
            sheet.Dispose(); sheet.Dispose();
            Assert.That(texture.Disposals, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => sheet.GetTexture());
        }
        [Test]
        public void RepeatedStyleSwitchesReleaseEveryResidentTexture()
        {
            var textures = new System.Collections.Generic.List<Texture>();
            Texture Load() { var texture = new Texture(); textures.Add(texture); return texture; }
            using (var sheet = new Sheet(SheetType.BGRA, Load()))
            {
                sheet.SetTextureReload(Load);
                for (var i = 0; i < 50; i++)
                {
                    Assert.That(sheet.TryEvictTexture(long.MaxValue), Is.True);
                    sheet.GetTexture();
                }
            }
            Assert.That(textures.Count, Is.EqualTo(51));
            Assert.That(textures.TrueForAll(t => t.Disposals == 1), Is.True);
        }

        [Test]
        public void OrdinaryMutableSheetsCannotBeEvicted()
        {
            using var sheet = new Sheet(SheetType.BGRA, new Texture());
            Assert.That(sheet.TryEvictTexture(long.MaxValue), Is.False);
        }
    }
}
