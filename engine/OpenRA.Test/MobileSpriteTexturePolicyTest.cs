using System;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Platforms.Default;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class MobileSpriteTexturePolicyTest
	{
		[TestCase(PlatformType.Android, GLProfile.Embedded, 96, 32, true)]
		[TestCase(PlatformType.iOS, GLProfile.Embedded, 96, 32, true)]
		[TestCase(PlatformType.Android, GLProfile.Modern, 96, 32, false)]
		[TestCase(PlatformType.iOS, GLProfile.Modern, 96, 32, false)]
		[TestCase(PlatformType.Windows, GLProfile.Embedded, 96, 32, false)]
		[TestCase(PlatformType.Linux, GLProfile.Modern, 96, 32, false)]
		[TestCase(PlatformType.OSX, GLProfile.Modern, 128, 32, true)]
		[TestCase(PlatformType.Android, GLProfile.Embedded, 0, 32, false)]
		[TestCase(PlatformType.Android, GLProfile.Embedded, -96, 32, false)]
		[TestCase(PlatformType.iOS, GLProfile.Embedded, 96, 0, false)]
		public void SpriteUploadsRespectPlatformProfileAndPositiveDimensions(
			PlatformType platform, GLProfile profile, int width, int height, bool expected)
		{
			var policy = typeof(Texture).GetMethod("SupportsSpriteDimensions",
				BindingFlags.Static | BindingFlags.NonPublic, null,
				new[] { typeof(int), typeof(int), typeof(PlatformType), typeof(GLProfile) }, null);
			Assert.That(policy, Is.Not.Null, "Both mobile GLES 3 hosts need an explicit sprite upload policy.");
			Assert.That(policy.Invoke(null, new object[] { width, height, platform, profile }), Is.EqualTo(expected));
		}
	}
}
