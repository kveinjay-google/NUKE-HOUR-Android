#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using NUnit.Framework;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class ProductionIconPresentationTest
	{
		[TestCase("amradr", "hd-production-amradr")]
		[TestCase("ORCA", "hd-production-orca")]
		public void ActorImageUsesActorIdentity(string actor, string expected)
		{
			Assert.That(ProductionIconPresentation.ActorImage(actor), Is.EqualTo(expected));
		}

		[TestCase("amradr", "large-production-amradr")]
		[TestCase("ORCA", "large-production-orca")]
		public void LargeActorImageUsesDedicatedRetinaIdentity(string actor, string expected)
		{
			Assert.That(ProductionIconPresentation.LargeActorImage(actor), Is.EqualTo(expected));
		}

		[TestCase("cairf", "Paradrop.Allies", "hd-support-cairf-paradrop-allies")]
		[TestCase("YAPPET", "Psychic Dominator", "hd-support-yappet-psychic-dominator")]
		public void SupportImageUsesOwnerAndTraitIdentity(string owner, string trait, string expected)
		{
			Assert.That(ProductionIconPresentation.SupportImage(owner, trait), Is.EqualTo(expected));
		}

		[Test]
		public void WideCameoFillsTheExistingSingleColumnInteriorWithoutDistortion()
		{
			var source = new Size(432, 256);
			var cell = new Size(172, 102);
			var scale = ProductionIconPresentation.Scale(source, cell, allowUpscaling: true);
			Assert.That(source.Width * scale, Is.EqualTo(cell.Width).Within(.1));
			Assert.That(source.Height * scale, Is.EqualTo(cell.Height).Within(.1));
			var cover = ProductionIconPresentation.MaskCoverScale(source, cell, new Size(60, 48), true);
			Assert.That(60 * cover, Is.GreaterThanOrEqualTo(source.Width * scale));
			Assert.That(48 * cover, Is.GreaterThanOrEqualTo(source.Height * scale));
			Assert.That(ProductionIconPresentation.WideActorImage("YAREFN"), Is.EqualTo("wide-production-yarefn"));
		}

		[TestCase(320, 256, 60, 48, 0.1875f)]
		[TestCase(60, 48, 320, 256, 1f)]
		[TestCase(320, 256, 64, 64, 0.2f)]
		public void ScaleAspectFitsWithoutUpscaling(
			int sourceWidth, int sourceHeight, int destinationWidth, int destinationHeight, float expected)
		{
			Assert.That(ProductionIconPresentation.Scale(
				new Size(sourceWidth, sourceHeight), new Size(destinationWidth, destinationHeight)),
				Is.EqualTo(expected).Within(0.0001f));
		}

		[Test]
		public void ScaleCanUpscaleClassicCameosForLargeTouchCells()
		{
			Assert.That(ProductionIconPresentation.Scale(
				new Size(60, 48), new Size(180, 144), allowUpscaling: true),
				Is.EqualTo(3f).Within(0.0001f));
		}

		[TestCase(80, 64, false, 1.333333f)]
		[TestCase(180, 144, true, 3f)]
		[TestCase(160, 80, true, 1.666667f)]
		[TestCase(320, 256, false, 5.333333f)]
		public void MaskTracksDisplayedOriginalNotLegacyNativeSize(int w, int h, bool upscale, float expected)
		{
			Assert.That(ProductionIconPresentation.MaskCoverScale(new Size(320, 256), new Size(w, h),
				new Size(60, 48), upscale), Is.EqualTo(expected).Within(.0001));
		}
	}
}
