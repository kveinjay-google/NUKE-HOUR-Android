using NUnit.Framework;
using OpenRA.Platforms.Default;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosSpriteTextureSizeTest
	{
		[TestCase(1280, 1280, true, true)]
		[TestCase(1280, 1280, false, false)]
		[TestCase(2048, 2048, false, true)]
		[TestCase(0, 1280, true, false)]
		public void SpriteTextureDimensionsFollowThePlatformCapability(
			int width, int height, bool iosGles3, bool expected)
		{
			Assert.That(Texture.SupportsSpriteDimensions(width, height, iosGles3), Is.EqualTo(expected));
		}
	}
}
