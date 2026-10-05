#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version.
 */
#endregion

using System;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class AndroidContentImportContractTest
	{
		static string RepositoryRoot([CallerFilePath] string sourceFile = "")
		{
			var directory = new FileInfo(sourceFile).Directory;
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "android", "OpenRA.Android")))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
		}

		[Test]
		public void PreparationScreenUsesFlexibleResourcesAndFourFixedActions()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "android", "OpenRA.Android", "MainActivity.cs"));

			StringAssert.Contains("ReadyStatusLine", source);
			StringAssert.Contains("resourceStatusScroll", source);
			StringAssert.Contains("CreateActionGrid", source);
			StringAssert.Contains("ShowLicenseAndNotice", source);
			StringAssert.Contains("ShowAcknowledgements", source);
			StringAssert.Contains("ViewLicense", source);
			StringAssert.Contains("Acknowledgements", source);
			StringAssert.DoesNotContain("var bodyText = NoticeText(copy.Body", source);
		}

		[Test]
		public void SteamFolderImportFiltersNoiseAndPublishesToTheMountedContentRoot()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "android", "OpenRA.Android", "MainActivity.cs"));

			StringAssert.Contains("PublicContentSafetyPolicy.IsSupportedDataFileName(displayName)", source);
			StringAssert.Contains("Path.Combine(SupportPath, \"Content\")", source);
			StringAssert.Contains("MigrateLegacyImportRoot", source);
			StringAssert.Contains("RetailContentImporter.NormalizeInstalledContent(ContentRoot)", source);
			StringAssert.DoesNotContain("new RetailImportRequest(files, support)", source);
		}
	}
}
