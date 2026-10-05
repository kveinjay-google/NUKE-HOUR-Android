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

using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Mods.Cnc.FileFormats;
using OpenRA.Mods.Cnc.Graphics;

namespace OpenRA.Test
{
	[TestFixture]
	[NonParallelizable]
	public class VoxelLoaderLifetimeTest
	{
		sealed class CountingTexture : ITexture
		{
			public int Disposals;
			public Size Size => new(16, 16);
			public TextureScaleFilter ScaleFilter { get; set; }
			public void Dispose() => Disposals++;
			public byte[] GetData() => new byte[16 * 16 * 4];
			public void SetData(byte[] colors, int width, int height) { }
			public void SetFloatData(float[] data, int width, int height) { }
			public void SetDataFromReadBuffer(Rectangle rect) { }
		}

		[TestCase(1, 200)]
		[TestCase(20, 200)]
		public void EveryAtlasSurvivesUntilLoaderDisposalAndIsReleasedAcrossMapTransitions(int transitions, int models)
		{
			var previousSettings = Game.Settings;
			var previousRenderer = Game.Renderer;
			var textures = new List<CountingTexture>();
			try
			{
				Game.Settings = new Settings(null, new Arguments(System.Array.Empty<string>()));
				Game.Settings.Graphics.SheetSize = 16;
				Game.Renderer = null;
				var limb = new VxlLimb
				{
					Size = new byte[] { 1, 1, 1 },
					VoxelMap = new Dictionary<byte, VxlElement>[1, 1]
				};
				limb.VoxelMap[0, 0] = new Dictionary<byte, VxlElement> { [0] = new(1, 1) };
				for (var transition = 0; transition < transitions; transition++)
				{
					var sheets = new HashSet<Sheet>();
					using (var loader = new VoxelLoader(null))
					{
						for (var model = 0; model < models; model++)
						{
							var data = loader.GenerateRenderData(limb);
							if (sheets.Add(data.Sheet))
							{
								var texture = new CountingTexture();
								textures.Add(texture);
								typeof(Sheet).GetField("texture", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(data.Sheet, texture);
							}
						}

						Assert.That(sheets.Count, Is.GreaterThan(1), "Exercise real voxel atlas overflow.");
						foreach (var sheet in sheets)
							Assert.That(((CountingTexture)sheet.GetTexture()).Disposals, Is.Zero, "Live models still reference every atlas.");
					}
				}

				var released = textures.FindAll(t => t.Disposals == 1).Count;
				TestContext.WriteLine($"transitions={transitions}, modelsPerTransition={models}, atlases={textures.Count}, released={released}, retained={textures.Count - released}");
				Assert.That(released, Is.EqualTo(textures.Count), "All overflow atlas textures must be disposed exactly once.");
			}
			finally
			{
				Game.Settings = previousSettings;
				Game.Renderer = previousRenderer;
			}
		}
	}
}
