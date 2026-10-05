#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software under the GNU General Public License.
 */
#endregion

using System.IO;
using System.Linq;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class AndroidTextReadabilityPolicyTest
	{
		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[Test]
		public void AndroidUsesTheSameReadableFontRolesAsIos()
		{
			var manifest = MiniYaml.FromString(File.ReadAllText(Path.Combine(RepositoryRoot(), "mods/ra2/mod.yaml")), "mod.yaml");
			var fonts = manifest.Single(node => node.Key == "Fonts").Value;
			int FontSize(string role) => int.Parse(fonts.NodeWithKey(role).Value.NodeWithKey("Size").Value.Value);
			Assert.That(FontSize("IosRegular"), Is.GreaterThanOrEqualTo(24));
			Assert.That(FontSize("IosBold"), Is.GreaterThanOrEqualTo(FontSize("IosRegular")));
			Assert.That(FontSize("IosTitle"), Is.GreaterThan(FontSize("IosBold")));
			var dropdown = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine/OpenRA.Mods.Common/Widgets/DropDownButtonWidget.cs"));
			Assert.That(dropdown, Does.Contain("Platform.UsesMobileLayout"));
			Assert.That(dropdown, Does.Contain("var font = header ? \"IosBold\" : \"IosRegular\";"));
		}

		[Test]
		public void RendererUsesSharedManifestFontsWithoutAndroidOnlyEnlargement()
		{
			var root = RepositoryRoot();
			var renderer = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Game", "Renderer.cs"));
			var perf = File.ReadAllText(Path.Combine(root, "engine", "mods", "common", "chrome", "ingame-perf.yaml"));

			StringAssert.Contains("x.Value.Size, x.Value.Ascender, Window.EffectiveWindowScale", renderer);
			StringAssert.DoesNotContain("MobileFontSizePolicy.Resolve", renderer);
			StringAssert.Contains("Width: 310", perf);
			StringAssert.Contains("Height: 90", perf);
		}
	}
}
