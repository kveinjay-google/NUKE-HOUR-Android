#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is
 * made available under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosContentImportContractTest
	{
		static string RepositoryRoot([CallerFilePath] string sourceFile = "")
		{
			var directory = new FileInfo(sourceFile).Directory;
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "ios", "OpenRA.iOS")))
				directory = directory.Parent;

			return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
		}

		static string IosFile(string name)
		{
			return File.ReadAllText(Path.Combine(RepositoryRoot(), "ios", "OpenRA.iOS", name));
		}

		[Test]
		public void PublicBuildChecksImportedContentBeforeStartingGame()
		{
			var project = IosFile("OpenRA.iOS.csproj");
			var appDelegate = IosFile("AppDelegate.cs");

			StringAssert.Contains("PUBLIC_CLEAN", project);
			StringAssert.Contains("#if PUBLIC_CLEAN", appDelegate);
			StringAssert.Contains("IosContentImportCoordinator", appDelegate);
			StringAssert.Contains("contentImportCoordinator.Start", appDelegate);
		}

		[Test]
		public void PickerBalancesSecurityScopedResourceAccess()
		{
			var picker = IosFile("IosRetailContentPicker.cs");

			StringAssert.Contains("UIDocumentPickerViewController", picker);
			StringAssert.Contains("StartAccessingSecurityScopedResource", picker);
			StringAssert.Contains("finally", picker);
			StringAssert.Contains("StopAccessingSecurityScopedResource", picker);
		}

		[Test]
		public void FolderImportIgnoresUnrecognizedFilesWithoutWeakeningExplicitFileValidation()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");

			StringAssert.Contains("ExpandFolderSelection", coordinator);
			StringAssert.Contains("PublicContentSafetyPolicy.ValidateImportFile(file) == ContentSafetyViolation.None", coordinator);
			StringAssert.Contains("else if (File.Exists(path))", coordinator,
				"Explicitly selected files must still reach the importer and its safety validation.");
		}

		[Test]
		public void ImportFailureAlwaysLeavesCheckingStateAndShowsTheRealInstalledStatus()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var viewController = IosFile("ViewController.cs");

			StringAssert.Contains("result.Status ?? InspectInstalledContent()", coordinator);
			StringAssert.Contains("ImportFailureMessage", coordinator);
			StringAssert.Contains("catch (Exception e)", coordinator,
				"Picker/import exceptions must not strand the native wizard in its busy state.");
			StringAssert.Contains("model.State == IosContentImportState.Checking", viewController,
				"Only an active readiness check may label every file as checking.");
		}

		[Test]
		public void CoordinatorUsesTransactionalImporterAndTreatsCancelAsNavigation()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");

			StringAssert.Contains("RetailContentImporter", coordinator);
			StringAssert.Contains("RetailImportError.Cancelled", coordinator);
			StringAssert.Contains("NeedsBase", coordinator);
			StringAssert.Contains("NeedsYuriOptional", coordinator);
			StringAssert.Contains("StartGameOnce", coordinator);
		}

		[Test]
		public void WizardUsesOneAdaptiveScrollableImportEntryWithoutSliders()
		{
			var viewController = IosFile("ViewController.cs");

			StringAssert.Contains("选择正版游戏文件", viewController);
			StringAssert.Contains("查看许可证", viewController);
			StringAssert.Contains("鸣谢", viewController);
			StringAssert.DoesNotContain("Button(strings.Retry", viewController);
			Assert.That(viewController.Split("Button(strings.ChooseFiles", StringSplitOptions.None).Length - 1,
				Is.EqualTo(1), "The wizard must expose one import entry instead of three duplicate pickers.");
			StringAssert.DoesNotContain("Button(strings.ImportBase", viewController);
			StringAssert.DoesNotContain("Button(strings.ImportYuri", viewController);
			StringAssert.Contains("UIScrollView", viewController,
				"The complete wizard must remain reachable on short landscape phones.");
			StringAssert.Contains("UIUserInterfaceIdiom.Pad", viewController,
				"The wizard must choose a dedicated iPad layout instead of scaling the phone layout.");
			StringAssert.DoesNotContain("UISlider", viewController);
			StringAssert.Contains("Landscape", viewController);
		}

		[Test]
		public void WizardShowsEveryRecognizedArchiveWithPurposeAndPerFileState()
		{
			var viewController = IosFile("ViewController.cs");

			foreach (var fileName in new[]
			{
				"ra2.mix", "language.mix", "ra2md.mix", "langmd.mix", "multi.mix", "multimd.mix",
				"maps01.mix", "maps02.mix", "mapsmd03.mix",
				"movies01.mix", "movies02.mix", "movmd03.mix",
				"theme.mix", "thememd.mix", "wdt.mix",
			})
				StringAssert.Contains(fileName, viewController);

			StringAssert.Contains("基础游戏核心资源", viewController);
			StringAssert.Contains("基础游戏文字、界面与音频", viewController);
			StringAssert.Contains("尤里的复仇游戏资源", viewController);
			StringAssert.Contains("尤里的复仇文字与音频", viewController);
			StringAssert.Contains("红警 2 遭遇战地图包", viewController);
			StringAssert.Contains("尤里的复仇遭遇战地图包", viewController);
			StringAssert.Contains("盟军战役剧情影片（可选）", viewController);
			StringAssert.Contains("苏军战役剧情影片（可选）", viewController);
			StringAssert.Contains("尤里的复仇战役任务包", viewController);
			StringAssert.Contains("尤里的复仇剧情影片（可选）", viewController);
			StringAssert.Contains("Steam 版补充资料（可选）", viewController);
			StringAssert.Contains("ImportedState = \"已导入\"", viewController);
			StringAssert.Contains("MissingState = \"未导入\"", viewController);
			StringAssert.Contains("model.Status?.Files", viewController);
			StringAssert.Contains("fileRows", viewController);
			StringAssert.DoesNotContain("StatusFormat", viewController);
			StringAssert.DoesNotContain("categoryStatus", viewController);
		}

		[Test]
		public void SteamCampaignAndMovieArchivesAreOptionalUserOwnedRuntimePackages()
		{
			var manifest = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "mod.yaml"));

			StringAssert.Contains("~mapsmd03.mix: retailcampaignmd03", manifest);
			StringAssert.Contains("~movmd03.mix: retailmoviesmd03", manifest);
		}

		[Test]
		public void PerFileRowsUseAFlexibleScrollablePreviewAndExpandableFullScreenDetail()
		{
			var viewController = IosFile("ViewController.cs");
			var coordinator = IosFile("IosContentImportCoordinator.cs");

			StringAssert.Contains("readonly UIScrollView fileListScroll", viewController);
			StringAssert.Contains("FlexibleFileListHeight", viewController);
			StringAssert.Contains("fileListScroll.ContentSize", viewController);
			StringAssert.Contains("Button(strings.ViewResourceDetails, showContentDetails)", viewController);
			StringAssert.Contains("sealed class IosContentDetailsViewController", viewController);
			StringAssert.Contains("ShowContentDetails", coordinator);
			StringAssert.Contains("new IosContentDetailsViewController", coordinator);
			StringAssert.Contains("UIModalPresentationStyle.OverFullScreen", coordinator);
		}

		[Test]
		public void NativeWizardKeepsFourActionsOnTheFirstScreen()
		{
			var viewController = IosFile("ViewController.cs");

			StringAssert.Contains("ReadyStatusLine", viewController);
			StringAssert.Contains("FlexibleFileListHeight", viewController);
			StringAssert.Contains("LayoutActionGrid", viewController);
			StringAssert.Contains("managementMode ? strings.ReturnHome : strings.ContinueGame", viewController);
			StringAssert.Contains("managementMode || model.State is", viewController);
			StringAssert.DoesNotContain("rootScroll.AddSubviews(summaryCard, noticeCard)", viewController);
			StringAssert.DoesNotContain("noticeCard.AddSubviews(noticeTitle, noticeScroll, website)", viewController);
		}

		[Test]
		public void AcknowledgementsUsesAnAdaptiveProjectOwnedSupporterWall()
		{
			var viewController = IosFile("ViewController.cs");
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var acknowledgementsPath = Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "IosAcknowledgementsViewController.cs");
			Assert.That(File.Exists(acknowledgementsPath), Is.True,
				"The supporter wall must be an isolated native surface instead of expanding the import view.");
			if (!File.Exists(acknowledgementsPath))
				return;

			var acknowledgements = File.ReadAllText(acknowledgementsPath);

			StringAssert.Contains("Button(strings.Acknowledgements, showAcknowledgements)", viewController);
			StringAssert.Contains("ShowAcknowledgements", coordinator);
			StringAssert.Contains("IosAcknowledgementsViewController", coordinator);
			StringAssert.Contains("CAGradientLayer", acknowledgements);
			StringAssert.Contains("UIScrollView", acknowledgements);
			StringAssert.Contains("UIUserInterfaceIdiom.Pad", acknowledgements);
			StringAssert.Contains("Supporters", acknowledgements);
			StringAssert.Contains("OpenRA Contributors", acknowledgements);
			StringAssert.Contains("UIModalPresentationStyle.OverFullScreen", coordinator);
		}

		[Test]
		public void WizardLayoutUsesRuntimeFramesAfterLandscapeGeometrySettles()
		{
			var viewController = IosFile("ViewController.cs");

			StringAssert.Contains("sealed class IosContentWizardView", viewController);
			StringAssert.Contains("public override void LayoutSubviews()", viewController);
			StringAssert.Contains("rootScroll.ContentSize", viewController);
			StringAssert.Contains("contentWizard.Frame = WizardFrame()", viewController);
			StringAssert.DoesNotContain("columns.HeightAnchor.ConstraintEqualTo(scroll.FrameLayoutGuide.HeightAnchor", viewController);
		}

		[Test]
		public void LicenseUsesAnAdaptiveScrollableSurfaceInsteadOfANarrowSystemAlert()
		{
			var viewController = IosFile("ViewController.cs");
			var coordinator = IosFile("IosContentImportCoordinator.cs");

			StringAssert.Contains("sealed class IosLicenseViewController", viewController);
			StringAssert.Contains("UIUserInterfaceIdiom.Pad", viewController);
			StringAssert.Contains("licenseScroll.ContentSize", viewController);
			StringAssert.Contains("UIModalPresentationStyle.OverFullScreen", coordinator);
			StringAssert.DoesNotContain("UIAlertController.Create", coordinator);
		}

		[Test]
		public void StartupNoticeContainsApprovedBilingualDistributionWarnings()
		{
			var viewController = IosFile("ViewController.cs");

			StringAssert.Contains("EA has not endorsed and does not support this product.", viewController);
			StringAssert.Contains("free and open-source", viewController);
			StringAssert.Contains("legally purchased and owned copy", viewController);
			StringAssert.Contains("officially authorized NUKE HOUR edition", viewController);
			StringAssert.Contains("malicious code", viewController);
			StringAssert.Contains("EA 未认可且不支持本产品", viewController);
			StringAssert.Contains("免费开源", viewController);
			StringAssert.Contains("合法购买并拥有的正版游戏副本", viewController);
			StringAssert.Contains("官方授权版本", viewController);
			StringAssert.Contains("恶意代码", viewController);
		}

		[Test]
		public void LicenseCombinesNoticeWebsiteAndResponsiveScrollableCopy()
		{
			var viewController = IosFile("ViewController.cs");

			StringAssert.Contains("https://nukehour.com", viewController);
			StringAssert.Contains("UIApplication.SharedApplication.OpenUrl", viewController);
			StringAssert.Contains("NSBundle.MainBundle", viewController);
			StringAssert.Contains("new UIScrollView", viewController);
			StringAssert.Contains("licenseScroll.AddSubview(body)", viewController);
			StringAssert.Contains("licenseScroll.ContentSize", viewController);
			StringAssert.Contains("version.Frame", viewController);
			StringAssert.Contains("website.Frame", viewController);
			StringAssert.Contains("new IosLicenseViewController(strings, viewController.OpenOfficialWebsite)",
				IosFile("IosContentImportCoordinator.cs"));
			StringAssert.DoesNotContain("NUKE HOUR RA2", viewController);
		}

		[Test]
		public void ReadyContentRequiresAnExplicitEnterGameAction()
		{
			var viewController = IosFile("ViewController.cs");
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var readyBranchStart = coordinator.IndexOf(
				"Show(IosContentImportState.Ready, strings.ReadyMessage, status);", StringComparison.Ordinal);
			var readyBranchEnd = coordinator.IndexOf("void ChooseFiles", readyBranchStart, StringComparison.Ordinal);
			var readyBranch = coordinator.Substring(readyBranchStart, readyBranchEnd - readyBranchStart);

			StringAssert.Contains("model.State is IosContentImportState.NeedsYuriOptional or IosContentImportState.Ready", viewController);
			StringAssert.DoesNotContain("StartGameOnce", readyBranch);
		}

		[Test]
		public void AutomatedSkirmishLaunchBypassesTheNativeReadyScreenOnlyForSmokeTests()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");

			StringAssert.Contains("OPENRA_IOS_AUTOTEST_SKIRMISH", coordinator);
			StringAssert.Contains("StartGameOnce();", coordinator);
			StringAssert.Contains("Show(IosContentImportState.Ready, strings.ReadyMessage, status);", coordinator);
		}

		[Test]
		public void GameLaunchWaitsForTheNativePresentationToFinish()
		{
			var viewController = IosFile("ViewController.cs");
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var handoffStart = viewController.IndexOf(
				"public void BeginGameLaunch(Action startGame)", StringComparison.Ordinal);
			var handoffEnd = viewController.IndexOf("static string BundleDisplayVersion", handoffStart,
				StringComparison.Ordinal);
			Assert.That(handoffStart, Is.GreaterThanOrEqualTo(0));
			Assert.That(handoffEnd, Is.GreaterThan(handoffStart));
			var handoff = viewController.Substring(handoffStart, handoffEnd - handoffStart);

			StringAssert.Contains("PresentedViewController", handoff);
			StringAssert.Contains("DismissViewController", handoff);
			StringAssert.Contains("contentWizard?.RemoveFromSuperview()", handoff);
			StringAssert.Contains("View.LayoutIfNeeded()", handoff);
			StringAssert.Contains("BeginInvokeOnMainThread(startGame)", handoff);
			Assert.That(handoff.IndexOf("contentWizard?.RemoveFromSuperview()", StringComparison.Ordinal),
				Is.LessThan(handoff.IndexOf("BeginInvokeOnMainThread(startGame)", StringComparison.Ordinal)));
			StringAssert.Contains("viewController.BeginGameLaunch(startGame)", coordinator);
		}

		[Test]
		public void StartupFailureRestoresAVisibleNativeSurface()
		{
			var viewController = IosFile("ViewController.cs");
			var appDelegate = IosFile("AppDelegate.cs");

			StringAssert.Contains("contentWizard?.RemoveFromSuperview()", viewController);
			StringAssert.Contains("status.Hidden = false", viewController);
			StringAssert.Contains("View.BringSubviewToFront(status)", viewController);
			StringAssert.Contains("Window?.MakeKeyAndVisible()", appDelegate);
		}

		[Test]
		public void StartupPreflightRunsBeforeTheEngine()
		{
			var appDelegate = IosFile("AppDelegate.cs");
			var preflight = IosFile("IosLaunchPreflight.cs");
			var check = appDelegate.IndexOf("IosLaunchPreflight.EnsureComplete", StringComparison.Ordinal);
			var run = appDelegate.IndexOf("Game.InitializeForExternalLoop", StringComparison.Ordinal);

			Assert.That(check, Is.GreaterThanOrEqualTo(0));
			Assert.That(run, Is.GreaterThan(check));
			StringAssert.Contains("glsl/combined.vert", preflight);
			StringAssert.Contains("mods/ra2/fonts/NotoSansCJKsc-Regular.otf", preflight);
			StringAssert.Contains("mods/ra2/uibits/menubuttons.png", preflight);
			StringAssert.Contains("mods/ra2/uibits/menupanel.png", preflight);
			StringAssert.Contains("mods/ra2/uibits/NUCLEAR-CRISIS-BG-06.png", preflight);
		}

		[Test]
		public void ExistingRetailArchiveNamesAreNormalizedBeforeReadinessAndEngineStartup()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var normalize = coordinator.IndexOf(
				"RetailContentImporter.NormalizeInstalledContent(contentRoot)", StringComparison.Ordinal);
			var readiness = coordinator.IndexOf("CheckReadiness();", StringComparison.Ordinal);

			Assert.That(normalize, Is.GreaterThanOrEqualTo(0));
			Assert.That(readiness, Is.GreaterThan(normalize));
		}

		[Test]
		public void PublicCleanStartupDoesNotRequireStrippedDeveloperModPackages()
		{
			var manifest = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "mod.yaml"));

			StringAssert.DoesNotContain("\t$ts: ts", manifest,
				"The removed asset browser must not keep TS as a mandatory runtime mod.");
			StringAssert.DoesNotContain("\tts|chrome/assetbrowser.yaml", manifest);
			StringAssert.Contains("\t\t~ra2|bits/cameos", manifest);
			StringAssert.Contains("\t\t~ra2|bits/structures", manifest);
			StringAssert.Contains("\t\t~ra2|bits/projectiles", manifest);
			StringAssert.DoesNotContain("\tra2|maps: System", manifest);
			StringAssert.DoesNotContain("\t~ra2|maps: System", manifest,
				"Legacy checkout maps must stay out of the runtime catalog when they are not compatible.");
			StringAssert.Contains("\t~^SupportDir|maps/ra2/nukehour-storage-v1: User", manifest,
				"Imported user maps must remain available in the runtime catalog.");
		}

		[Test]
		public void PublicCleanCreatesItsShellSurfaceOutsideTheApplicationBundle()
		{
			var appDelegate = IosFile("AppDelegate.cs");
			var project = IosFile("OpenRA.iOS.csproj");

			StringAssert.Contains("RuntimeShellmapInstaller.EnsureInstalled(supportPath)", appDelegate);
			StringAssert.Contains("#if PUBLIC_CLEAN", appDelegate);
			StringAssert.DoesNotContain("../../mods/ra2/maps/", project,
				"No map payload may be restored to the public IPA to provide the native menu surface.");
			StringAssert.Contains("../../mods/ra2/bits/**/*", project,
				"The complete, individually hash-locked support-art inventory must remain in the public IPA.");
			StringAssert.DoesNotContain("../../mods/ra2/bits/animations/nc-impact-*.png", project,
				"PublicClean must not regress to the incomplete impact-overlay-only bundle.");
		}

		[Test]
		public void ReadyRetailContentCreatesAPlayableProjectOwnedMapOutsideTheBundle()
		{
			var coordinator = IosFile("IosContentImportCoordinator.cs");
			var project = IosFile("OpenRA.iOS.csproj");

			StringAssert.Contains("RuntimeShellmapInstaller.EnsurePlayableMapInstalled(supportPath)", coordinator);
			StringAssert.Contains("Path.Combine(supportPath, \"maps\", \"ra2\", \"nukehour-storage-v1\")", coordinator);
			StringAssert.DoesNotContain("../../mods/ra2/maps/", project,
				"The generated starter map must remain user data and must not enter the IPA.");
		}

		[Test]
		public void PublicCleanCursorsUseFramesFromTheImportedRetailCursorSheet()
		{
			var cursors = File.ReadAllText(Path.Combine(RepositoryRoot(), "mods", "ra2", "cursors.yaml"));

			StringAssert.DoesNotContain("undeploy.shp:", cursors,
				"PublicClean excludes mod-provided SHP artwork, so startup cursors must not require it.");
			StringAssert.DoesNotContain("assaultmove.shp:", cursors,
				"PublicClean excludes mod-provided SHP artwork, so startup cursors must not require it.");
			StringAssert.Contains("\t\tundeploy:", cursors);
			StringAssert.Contains("\t\tassaultmove:", cursors);
		}
	}
}
