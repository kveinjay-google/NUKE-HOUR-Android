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

using System.IO;
using NUnit.Framework;
using OpenRA.Platforms.Default;
using OpenRA.Support;

namespace OpenRA.Test
{
	[TestFixture]
	public sealed class IosPerformancePolicyTest
	{
		[Test]
		public void IosKeepsOpenGlOnTheDisplayLinkDrivenMainThread()
		{
			Assert.That(GraphicsContextThreadingPolicy.UseDedicatedThread(
				PlatformType.iOS, WindowMode.Fullscreen), Is.False);
			Assert.That(GraphicsContextThreadingPolicy.UseDedicatedThread(
				PlatformType.OSX, WindowMode.Fullscreen), Is.True);
			Assert.That(GraphicsContextThreadingPolicy.UseDedicatedThread(
				PlatformType.Windows, WindowMode.Windowed), Is.False);
		}

		[TestCase(WindowMode.Fullscreen)]
		[TestCase(WindowMode.Windowed)]
		public void AndroidKeepsOpenGlOnTheSdlEventPumpThreadForSurfaceRecovery(WindowMode mode)
		{
			Assert.That(GraphicsContextThreadingPolicy.UseDedicatedThread(
				PlatformType.Android, mode), Is.False,
				"SDL Android saves and restores the current EGL context on the event-pump thread.");
		}

		[Test]
		public void SharedExternalLoopPreservesBlockingAndNonBlockingFrameScheduling()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Game.cs"));
			var contextSource = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Platforms.Default", "Sdl2GraphicsContext.cs"));
			var windowSource = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Platforms.Default", "Sdl2PlatformWindow.cs"));
			StringAssert.Contains("public static void InitializeForExternalLoop(string[] args)", source);
			StringAssert.Contains("public static bool RunExternalLoopFrame()", source);
			StringAssert.Contains("public static void AbortExternalLoop()", source);
			StringAssert.Contains("RunLoopIteration(loopState, allowSleep: true)", source,
				"The desktop blocking loop must retain its existing scheduling semantics.");
			StringAssert.Contains("RunLoopIteration(externalLoopState, allowSleep: false)", source,
				"The display callback must advance one frame without sleeping on the UIKit thread.");
			StringAssert.Contains("externalFrameWindow.PrepareExternalFrame()", source,
				"UIKit may replace the current GL context between display-link callbacks.");
			var prepareExternalFrame = contextSource.Substring(
				contextSource.IndexOf("internal void PrepareExternalFrame()", System.StringComparison.Ordinal));
			prepareExternalFrame = prepareExternalFrame.Substring(0,
				prepareExternalFrame.IndexOf("\n\t\t}", System.StringComparison.Ordinal) + 4);
			StringAssert.DoesNotContain("SDL.SDL_GL_MakeCurrent", prepareExternalFrame,
				"SDL's native display-link callback already restores EAGLContext; rebinding it again also " +
				"calls setSDLWindow and can consume an entire ProMotion frame budget.");
			StringAssert.Contains("directContext.PrepareExternalFrame()", windowSource);
		}

		[Test]
		public void IosDisplayLinkAdvancesTheSharedNonBlockingGameLoop()
		{
			var displayLinkSource = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "IosDisplayRefreshController.cs"));
			var platformStubs = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "native", "sdl-platform-stubs.c"));
			var sdlRefreshPatch = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "native", "patches",
				"SDL_uikitviewcontroller-promotion.patch"));
			var sdlReentrancyPatch = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "native", "patches",
				"SDL_uikitevents-displaylink-reentrancy.patch"));
			var sdlPresentPatch = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "native", "patches",
				"SDL_uikitopenglview-promotion-present.patch"));

			StringAssert.Contains("CADisplayLink.Create", displayLinkSource,
				"The system display link must remain owned by the active UIKit run loop.");
			StringAssert.Contains("NSRunLoop.Main", displayLinkSource);
			StringAssert.Contains("NSRunLoopMode.Common", displayLinkSource,
				"The frame pump must continue while UIKit temporarily changes input tracking modes.");
			StringAssert.Contains("OpenRA_iOS_RestoreCurrentContext", displayLinkSource,
				"A managed display link must restore SDL's EAGL context before advancing the engine.");
			StringAssert.Contains("OpenRA_iOS_SetManagedDisplayLinkRunning(1)", displayLinkSource,
				"SDL must be told that its event pump is already inside the managed display-link callback.");
			StringAssert.Contains("OpenRA_iOS_SetManagedDisplayLinkRunning(0)", displayLinkSource,
				"The managed display-link marker must be released after every engine frame.");
			StringAssert.Contains("OpenRA_iOS_SetMaximumFramesPerSecond(screen.MaximumFramesPerSecond)", displayLinkSource,
				"SDL's OpenGL presenter must use the same ProMotion cadence as the managed display link.");
			StringAssert.Contains("UIKit_GL_RestoreCurrentContext", platformStubs,
				"The exported OpenRA bridge must retain SDL's hidden context-restoration implementation.");
			StringAssert.Contains("UIKit_SetAnimationCallbackRunning", platformStubs,
				"The exported OpenRA bridge must reuse SDL's nested-run-loop suppression for managed frames.");
			StringAssert.Contains("OpenRA_iOS_MaximumFramesPerSecond", platformStubs,
				"The managed frame pump must update SDL's native minimum presentation duration.");
			StringAssert.Contains("if (paused || !pumpRunning || frameInProgress)", displayLinkSource,
				"The managed callback still needs a defensive guard against unexpected native re-entry.");
			StringAssert.Contains("NSProcessInfo.ProcessInfo.LowPowerModeEnabled", displayLinkSource,
				"Device logs must identify the system power state when diagnosing a 60 Hz ProMotion cap.");
			StringAssert.Contains("OPENRA_IOS_REFRESH_DIAGNOSTICS", displayLinkSource,
				"Detailed display-link work timing must be opt-in instead of spamming public release logs.");
			StringAssert.Contains("OPENRA_IOS_REFRESH_IDLE_PROBE", displayLinkSource,
				"A hidden device diagnostic must separate UIKit cadence from renderer/present cost.");
			StringAssert.Contains("callback work", displayLinkSource,
				"Opt-in diagnostics must distinguish a slow engine frame from a system-limited display link.");
			StringAssert.Contains("RestartDisplayLinkAfterMissedCadence", displayLinkSource,
				"A long map-load frame can demote ProMotion to 60 Hz; recreate the display link after that stall.");
			StringAssert.Contains("MaximumPromotionRestarts", displayLinkSource,
				"Promotion recovery must be bounded instead of repeatedly recreating the display link.");
			StringAssert.DoesNotContain("DispatchQueue.MainQueue.DispatchAsync", displayLinkSource,
				"Deferring every display-link callback to the main queue coalesces 120 Hz callbacks down to 60 Hz.");
			StringAssert.DoesNotContain("frameScheduled", displayLinkSource,
				"The display link must advance the engine directly instead of keeping a one-frame main-queue backlog.");
			Assert.That(
				displayLinkSource.IndexOf("if (paused || !pumpRunning || frameInProgress)", System.StringComparison.Ordinal),
				Is.LessThan(displayLinkSource.IndexOf("TrackCadence();", System.StringComparison.Ordinal)),
				"Rejected re-entrant callbacks must not inflate the measured display cadence.");
			StringAssert.Contains("finally", displayLinkSource,
				"The iOS frame re-entrancy guard must be released even when the engine frame throws.");
			StringAssert.DoesNotContain("SDL.SDL_iPhoneSetAnimationCallback", displayLinkSource,
				"SDL's private controller-owned display link can be detached when initialization returns to UIKit.");
			StringAssert.Contains("preferredFrameRateRange", sdlRefreshPatch,
				"ProMotion devices need an explicit frame-rate range instead of only preferredFramesPerSecond.");
			StringAssert.Contains("maximumFramesPerSecond", sdlRefreshPatch,
				"The requested range must remain bounded by the active screen's capability.");
			StringAssert.Contains("(float)(maximumFramesPerSecond * 2) / 3", sdlRefreshPatch,
				"Follow SDL's current UIKit policy and keep a 120 Hz display in its 80-120 Hz high-refresh range.");
			Assert.That(sdlRefreshPatch.Split("maximumFramesPerSecond").Length - 1, Is.GreaterThanOrEqualTo(5));
			StringAssert.Contains("UIKit_SetAnimationCallbackRunning", sdlReentrancyPatch,
				"SDL must know when PumpEvents is called from inside its CADisplayLink callback.");
			StringAssert.Contains("if (!UIKit_AnimationCallbackRunning)", sdlReentrancyPatch,
				"SDL must not nest CFRunLoopRunInMode while a CADisplayLink frame is executing.");
			StringAssert.Contains("UIKit_GL_RestoreCurrentContext", sdlReentrancyPatch,
				"Skipping the nested run loop must not skip SDL's OpenGL ES context restoration.");
			StringAssert.Contains("OpenRA_MetalPresent(self)", sdlPresentPatch,
				"The iOS composite must be presented by Metal, which is verified with actual drawable timestamps.");
			StringAssert.DoesNotContain("afterMinimumDuration:", sdlPresentPatch,
				"The EAGL minimum-duration path accepts frames but displays black on the tested ProMotion device.");
			StringAssert.DoesNotContain("atTime:", sdlPresentPatch,
				"60 Hz devices must retain SDL's proven legacy presentation path.");
		}

		static string RepositoryRoot()
		{
			var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
			while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "engine")))
				directory = directory.Parent;

			Assert.That(directory, Is.Not.Null, "Unable to locate the repository root.");
			return directory!.FullName;
		}

		[Test]
		public void MetalPresentationDiagnosticsMeasureDisplayedFramesAndGpuHandoff()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"ios", "OpenRA.iOS", "native", "metal-presenter.m"));
			StringAssert.Contains("shown.presentedTime", source);
			StringAssert.Contains("handoffMilliseconds", source);
			StringAssert.Contains("drawableWaitMilliseconds", source);
			StringAssert.Contains("thermalState", source);
			StringAssert.Contains("OPENRA_IOS_REFRESH_DIAGNOSTICS", source);
		}

		[Test]
		public void IosStreamingVerticesDiscardPreviousGpuStorageButPartialUpdatesDoNot()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Platforms.Default", "VertexBuffer.cs"));
			var streamStart = source.IndexOf("public void SetData(ref T[] data, int length)", System.StringComparison.Ordinal);
			var partialStart = source.IndexOf("public void SetData(T[] data, int offset, int start, int length)", System.StringComparison.Ordinal);
			var stream = source.Substring(streamStart, partialStart - streamStart);
			StringAssert.Contains("#if IOS", stream);
			StringAssert.Contains("OpenGL.glBufferData", stream);
			StringAssert.Contains("bufferCapacityBytes", stream);
			StringAssert.DoesNotContain("OpenGL.glBufferData", source.Substring(partialStart));
		}

		[Test]
		public void RadarUploadsOnlyWhenItsPixelsChangeNotOnEveryRenderFrame()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Mods.Common", "Widgets", "RadarWidget.cs"));
			var drawStart = source.IndexOf("public override void Draw()", System.StringComparison.Ordinal);
			var drawEnd = source.IndexOf("void DrawRadarPings()", drawStart, System.StringComparison.Ordinal);
			StringAssert.DoesNotContain("CommitBufferedData", source.Substring(drawStart, drawEnd - drawStart));
			foreach (var method in new[] { "void UpdateTerrainColor(MPos uv)", "void UpdateShroudCell(PPos puv)", "public override void Tick()" })
			{
				var start = source.IndexOf(method, System.StringComparison.Ordinal);
				var end = source.IndexOf("\n\t\t}", start, System.StringComparison.Ordinal);
				StringAssert.Contains("radarSheet.CommitBufferedData();", source.Substring(start, end - start), method);
			}
		}

		[Test]
		public void ProductionCameosBatchBeforeMultiplicativeMasks()
		{
			var source = File.ReadAllText(Path.Combine(RepositoryRoot(),
				"engine", "OpenRA.Mods.Common", "Widgets", "ProductionPaletteWidget.cs"));
			StringAssert.Contains("DrawCameos();", source);
			StringAssert.Contains("DrawClocksAndMasks();", source);
			Assert.That(source.IndexOf("DrawCameos();", System.StringComparison.Ordinal),
				Is.LessThan(source.IndexOf("DrawClocksAndMasks();", System.StringComparison.Ordinal)));
			StringAssert.Contains("buildableNames.Contains(icon.Name)", source);
			StringAssert.DoesNotContain("buildableItems.Any(a => a.Name == icon.Name)", source);
		}

		[Test]
		public void BenchmarkPolicyCanSuppressFrameDiagnosticsWithoutDisablingWatchdog()
		{
			PerformanceBenchmarkPolicy.IsActive = false;
			Assert.That(PerformanceBenchmarkPolicy.RecordFrameDiagnostics, Is.True);
			Assert.That(PerformanceBenchmarkPolicy.RunWatchdog, Is.True);

			PerformanceBenchmarkPolicy.IsActive = true;
			Assert.That(PerformanceBenchmarkPolicy.RecordFrameDiagnostics, Is.False);
			Assert.That(PerformanceBenchmarkPolicy.RunWatchdog, Is.True);

			PerformanceBenchmarkPolicy.IsActive = false;
		}

		[Test]
		public void BenchmarkCanForceHalfResolutionWorldRendering()
		{
			try
			{
				PerformanceBenchmarkPolicy.IsActive = true;
				System.Environment.SetEnvironmentVariable("OPENRA_IOS_PERF_RENDER_SCALE", "0.5");
				Assert.That(PerformanceBenchmarkPolicy.MinimumWorldDownscaleFactor, Is.EqualTo(2));
			}
			finally
			{
				System.Environment.SetEnvironmentVariable("OPENRA_IOS_PERF_RENDER_SCALE", null);
				PerformanceBenchmarkPolicy.IsActive = false;
			}
		}

		[Test]
		public void RendererConsumesBenchmarkScaleWithoutForcingItOnProductionDevices()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Renderer.cs"));
			var settings = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Settings.cs"));
			StringAssert.Contains(
				"minimumWorldDownscaleFactor, PerformanceBenchmarkPolicy.MinimumWorldDownscaleFactor",
				source,
				"The benchmark render-scale option must affect the world framebuffer, not only the policy value.");
			StringAssert.Contains("public int WorldDownscaleFactor = 1;", settings);
		}

		[Test]
		public void IosHostRestoresFullResolutionWorldRendering()
		{
			var appDelegate = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "AppDelegate.cs"));

			StringAssert.DoesNotContain("Graphics.WorldDownscaleFactor=2", appDelegate,
				"Production ProMotion devices must not blur and checkerboard the battlefield by forcing a half-size world buffer.");
			StringAssert.Contains("Graphics.WorldDownscaleFactor=1", appDelegate,
				"The host must also override half-resolution values persisted by earlier releases.");
		}

		[Test]
		public void IosLeavesTheDrawableUntouchedUntilItsFullScreenComposite()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Renderer.cs"));
			var beginFrame = source.Substring(
				source.IndexOf("void BeginFrame()", System.StringComparison.Ordinal));
			beginFrame = beginFrame.Substring(0,
				beginFrame.IndexOf("\n\t\t}", System.StringComparison.Ordinal) + 4);

			StringAssert.Contains("if (!OperatingSystem.IsIOS())\n\t\t\t\tContext.Clear();", beginFrame,
				"UIKit must not present a cleared rotating drawable before the completed off-screen frame.");
			StringAssert.DoesNotContain("Context.ClearDepthBuffer();", beginFrame,
				"The iOS default framebuffer must remain untouched until the full-screen composite is drawn.");
		}

		[Test]
		public void IosUsesPowerOfTwoFramebuffersCompatibleWithThePostProcessPipeline()
		{
			var renderer = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Renderer.cs"));
			var framebuffer = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Platforms.Default", "FrameBuffer.cs"));
			var texture = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Platforms.Default", "Texture.cs"));

			StringAssert.Contains(
				"var surfaceBufferSize = surfaceSize.NextPowerOf2();",
				renderer,
				"The final iOS framebuffer must retain the post-process pipeline's proven POT texture contract.");
			StringAssert.Contains(
				"if (!Exts.IsPowerOf2(size.Width) || !Exts.IsPowerOf2(size.Height))",
				framebuffer,
				"Framebuffers must reject NPOT sizes before they reach the existing post-process pipeline.");
			StringAssert.Contains(
				"if (!Exts.IsPowerOf2(width) || !Exts.IsPowerOf2(height))",
				texture,
				"Empty framebuffer textures must retain the engine's POT invariant.");
			StringAssert.DoesNotContain("worldBufferSize = Window.SurfaceSize.NextPowerOf2();", renderer,
				"The isometric world must preserve native tile samples before the completed scene is scaled.");
		}

		[Test]
		public void ProMotionPhonesCapTheOpenGlBackingScaleAtTwo()
		{
			var patch = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "native", "patches",
				"SDL_uikitopengles-openra-drawable.patch"));

			StringAssert.Contains("maximumFramesPerSecond > 60", patch,
				"Only ProMotion displays need the render-cost tradeoff.");
			StringAssert.Contains("scale > 2.0", patch);
			StringAssert.Contains("scale = 2.0", patch,
				"A 2x backing store remains Retina-sharp while reducing a 3x iPhone " +
				"drawable's fill workload by more than half.");
		}

		[Test]
		public void BenchmarkSamplesUseResourceSamplerAllocationGcAndThermalValues()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Support", "Benchmark.cs"));

			StringAssert.Contains("resources.TotalAllocatedBytes ?? 0", source);
			StringAssert.Contains("resources.Generation0Collections ?? 0", source);
			StringAssert.Contains("resources.Generation1Collections ?? 0", source);
			StringAssert.Contains("resources.Generation2Collections ?? 0", source);
			StringAssert.Contains("resources.ThermalState", source);
			StringAssert.DoesNotContain("GC.CollectionCount(", source,
				"All resource values in one sample must come from the same guarded sampler snapshot.");
		}

		[Test]
		public void BenchmarkProfilesSimulationAndRenderingCategories()
		{
			var root = RepositoryRoot();
			var world = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Game", "World.cs"));
			var actor = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Game", "Actor.cs"));
			var traits = File.ReadAllText(Path.Combine(root, "engine", "OpenRA.Game", "TraitDictionary.cs"));
			var worldRenderer = File.ReadAllText(Path.Combine(
				root, "engine", "OpenRA.Game", "Graphics", "WorldRenderer.cs"));
			var graphics = File.ReadAllText(Path.Combine(
				root, "engine", "OpenRA.Platforms.Default", "Sdl2GraphicsContext.cs"));

			StringAssert.Contains("new PerfSample(\"tick_traits\")", world);
			StringAssert.Contains("PerformanceCategoryProfiler.Global.Measure(\"Effect\"", world);
			StringAssert.Contains("PerformanceCategoryProfiler.Global.Measure(\"Actor Activity\"", actor);
			StringAssert.Contains("profiler.Measure(text, trait.GetType())", traits);
			foreach (var category in new[]
			{
				"Renderable Prepare", "Renderable Generation", "Overlay Generation",
				"Annotation Generation", "Terrain Render", "Prepared Renderable"
			})
				StringAssert.Contains($"PerformanceCategoryProfiler.Global.Measure(\"{category}\"", worldRenderer);

			StringAssert.Contains("PerformanceCategoryProfiler.Global.Measure(\"Draw Submission\")", graphics);
			StringAssert.Contains("PerformanceCategoryProfiler.Global.Measure(\"Present\")", graphics);
			StringAssert.DoesNotContain("framebufferStatus", graphics,
				"Per-draw framebuffer polling would contaminate the benchmark being measured.");
		}

		[Test]
		public void IosReleaseSkipsSynchronousGlErrorPolling()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Platforms.Default", "OpenGL.cs"));
			var method = source.Substring(source.IndexOf("public static void CheckGLError()", System.StringComparison.Ordinal));
			method = method.Substring(0, method.IndexOf("\n\t\t}", System.StringComparison.Ordinal) + 4);

			StringAssert.Contains("#if IOS && !DEBUG", method);
			Assert.That(method.IndexOf("return;", System.StringComparison.Ordinal),
				Is.LessThan(method.IndexOf("glGetError()", System.StringComparison.Ordinal)));
		}

		[Test]
		public void IosReleaseDoesNotInstallTheSynchronousGlDebugCallback()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Platforms.Default", "OpenGL.cs"));
			var setup = source.Substring(source.IndexOf("// Setup the debug message callback handler",
				System.StringComparison.Ordinal));
			setup = setup.Substring(0, setup.IndexOf("Console.WriteLine(\"OpenGL renderer:",
				System.StringComparison.Ordinal));

			StringAssert.Contains("#if !(IOS && !DEBUG)", setup,
				"Release iOS must not install a synchronous native callback that can throw during rendering.");
		}

		[Test]
		public void HangDiagnosticsUsesBenchmarkFramePolicy()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Support", "HangDiagnostics.cs"));

			StringAssert.Contains("!PerformanceBenchmarkPolicy.RecordFrameDiagnostics", source);
			StringAssert.DoesNotContain("!PerformanceBenchmarkPolicy.RunWatchdog", source);
		}

		[Test]
		public void IosStartsHangDiagnosticsAfterSupportDirectoryIsConfigured()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "AppDelegate.cs"));

			var supportDirectory = source.IndexOf("Directory.CreateDirectory(supportPath);", System.StringComparison.Ordinal);
			var start = source.IndexOf("HangDiagnostics.Start(supportPath);", System.StringComparison.Ordinal);
			var resume = source.IndexOf("HangDiagnostics.Resume();", start, System.StringComparison.Ordinal);
			Assert.That(start, Is.GreaterThan(supportDirectory));
			Assert.That(resume, Is.GreaterThan(start));
		}

		[Test]
		public void IosRegistersDurableUnhandledExceptionReporterBeforeBackgroundServices()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "AppDelegate.cs"));

			var supportDirectory = source.IndexOf("Directory.CreateDirectory(supportPath);",
				System.StringComparison.Ordinal);
			var register = source.IndexOf("RegisterUnhandledExceptionHandler(supportPath);",
				System.StringComparison.Ordinal);
			var runtimeInitialization = source.IndexOf("GeneratedAotObjectRegistryRegistration.RegisterAll();",
				System.StringComparison.Ordinal);
			var diagnostics = source.IndexOf("HangDiagnostics.Start(supportPath);",
				System.StringComparison.Ordinal);

			Assert.That(register, Is.GreaterThan(supportDirectory),
				"The explicit writable support directory must exist before installing the fatal callback.");
			Assert.That(register, Is.LessThan(runtimeInitialization),
				"Install the fatal callback before fallible runtime and audio initialization.");
			Assert.That(register, Is.LessThan(diagnostics),
				"Install the fatal callback before timers or other background services can fail.");
			StringAssert.Contains("UnhandledExceptionEventHandler? unhandledExceptionHandler;", source,
				"AppDomain.UnhandledException requires its dedicated delegate type on iOS/.NET 8.");
			StringAssert.Contains("if (unhandledExceptionHandler != null)", source,
				"Repeated launch callbacks must not add duplicate AppDomain subscribers.");
			StringAssert.Contains("ExceptionHandler.HandleFatalError(args.ExceptionObject, supportPath);", source,
				"Persist the original AppDomain payload synchronously to the already-known support directory.");
			StringAssert.Contains("AppDomain.CurrentDomain.UnhandledException += unhandledExceptionHandler;", source);
			StringAssert.Contains("AppDomain.CurrentDomain.UnhandledException -= unhandledExceptionHandler;", source);

			var willTerminate = source.IndexOf("public override void WillTerminate", System.StringComparison.Ordinal);
			var unregister = source.IndexOf("UnregisterUnhandledExceptionHandler();", willTerminate,
				System.StringComparison.Ordinal);
			var networkingShutdown = source.IndexOf("NearbyGameNetworking.Shutdown();", willTerminate,
				System.StringComparison.Ordinal);
			var diagnosticsStop = source.IndexOf("IosMetricKitDiagnostics.Stop();", willTerminate,
				System.StringComparison.Ordinal);
			Assert.That(unregister, Is.GreaterThan(willTerminate),
				"Detach the process-wide callback when UIKit terminates the delegate lifecycle.");
			Assert.That(unregister, Is.GreaterThan(networkingShutdown));
			Assert.That(unregister, Is.GreaterThan(diagnosticsStop),
				"Keep fatal persistence active until fallible shutdown work has completed.");
		}

		[Test]
		public void DetailedDestructionAuditTracesActorTraitEffectAndFrameEndWork()
		{
			var world = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "World.cs"));
			var traits = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "TraitDictionary.cs"));

			StringAssert.Contains("World.ActorTick.", world);
			StringAssert.Contains("World.EffectTick.", world);
			StringAssert.Contains("World.FrameEndTask.", world);
			StringAssert.Contains("World.TraitTick.", traits);
		}

		[Test]
		public void IosProvidesNativeThermalState()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "AppDelegate.cs"));

			StringAssert.Contains("RuntimeResourceSampler.ThermalStateProvider", source);
			StringAssert.Contains("NSProcessInfo.ProcessInfo.ThermalState", source);
		}

		[Test]
		public void IosCanEnableTimedBenchmarkFromLaunchEnvironment()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "AppDelegate.cs"));

			StringAssert.Contains("OPENRA_IOS_PERF_PREFIX", source);
			StringAssert.Contains("Launch.Benchmark=", source);
		}

		[Test]
		public void IosPreventsIdleLockDuringLongRunningGameplayAndAudits()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "AppDelegate.cs"));

			StringAssert.Contains("application.IdleTimerDisabled = true;", source);
		}

		[Test]
		public void IosPersistsFatalGameLoopExceptionsBeforeShowingTheFailureView()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "ios", "OpenRA.iOS", "AppDelegate.cs"));

			var failureHandler = source.IndexOf("void HandleGameFailure(Exception e)",
				System.StringComparison.Ordinal);
			var persist = source.IndexOf("ExceptionHandler.HandleFatalError(e);", failureHandler,
				System.StringComparison.Ordinal);
			var showFailure = source.IndexOf("rootViewController.ShowStartupFailure(e);", failureHandler,
				System.StringComparison.Ordinal);

			Assert.That(failureHandler, Is.GreaterThanOrEqualTo(0));
			Assert.That(persist, Is.GreaterThan(failureHandler),
				"Manual iOS launches must persist the full exception and stack trace.");
			Assert.That(showFailure, Is.GreaterThan(persist),
				"Persist the exception before replacing the game surface with the failure view.");

		}

		[Test]
		public void SharedFatalErrorHandlerUsesDurablePersistence()
		{
			var handlerSource = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Support", "ExceptionHandler.cs"));
			StringAssert.Contains("WriteFatalErrorReport(path, report);", handlerSource,
				"Fatal exception persistence must bypass the asynchronous logging queue.");
		}

		[Test]
		public void FatalExceptionReportIsDurableWhenPersistenceReturns()
		{
			var path = Path.Combine(Path.GetTempPath(), $"openra-fatal-{System.Guid.NewGuid():N}.log");
			try
			{
				var writer = typeof(ExceptionHandler).GetMethod("WriteFatalErrorReport",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
				Assert.That(writer, Is.Not.Null, "Missing synchronous fatal-report writer.");

				writer!.Invoke(null, new object[] { path, "fatal-stack-marker" });
				Assert.That(File.ReadAllText(path), Does.Contain("fatal-stack-marker"));
				using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
				Assert.That(exclusive.Length, Is.GreaterThan(0));
			}
			finally
			{
				if (File.Exists(path))
					File.Delete(path);
			}
		}

		[Test]
		public void FatalExceptionReportConstructionFallsBackWhenExceptionMetadataThrows()
		{
			var builder = typeof(ExceptionHandler).GetMethod("BuildFatalErrorReportSafely",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			Assert.That(builder, Is.Not.Null, "Missing best-effort fatal-report builder.");

			ThrowingMessageException exception;
			try
			{
				throw new ThrowingMessageException();
			}
			catch (ThrowingMessageException e)
			{
				exception = e;
			}

			var report = (string)builder!.Invoke(null, new object[] { exception })!;
			Assert.That(report, Does.Contain(typeof(ThrowingMessageException).FullName));
			Assert.That(report, Does.Contain("<message unavailable>"));
			Assert.That(report, Does.Contain(nameof(FatalExceptionReportConstructionFallsBackWhenExceptionMetadataThrows)),
				"A broken Message getter must not discard the independently available stack trace.");
		}

		[Test]
		public void FatalReportSnapshotsMutableGameStateAndIsolatesMetadataSections()
		{
			var sectionWriter = typeof(ExceptionHandler).GetMethod("AppendReportSection",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
				null, new[] { typeof(System.Text.StringBuilder), typeof(string), typeof(System.Action) }, null);
			Assert.That(sectionWriter, Is.Not.Null, "Fatal metadata sections need independent failure boundaries.");

			var report = new System.Text.StringBuilder();
			sectionWriter!.Invoke(null, new object[]
			{
				report,
				"racing-world",
				(System.Action)(() => throw new System.InvalidOperationException("world switched")),
			});
			report.AppendLine("original-exception-evidence");
			Assert.That(report.ToString(), Does.Contain("racing-world"));
			Assert.That(report.ToString(), Does.Contain("original-exception-evidence"),
				"One racing metadata read must not abort later exception evidence.");

			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Support", "ExceptionHandler.cs"));
			StringAssert.Contains("var modData = Game.ModData;", source);
			StringAssert.Contains("var manifest = modData?.Manifest;", source);
			StringAssert.Contains("var orderManager = Game.OrderManager;", source);
			StringAssert.Contains("var world = orderManager?.World;", source);
			StringAssert.Contains("var map = world?.Map;", source);
			StringAssert.DoesNotContain("Game.ModData.Manifest", source);
			StringAssert.DoesNotContain("Game.OrderManager.World", source);
		}

		[Test]
		public void FatalExceptionReporterPersistsNonExceptionPayloadToExplicitSupportDirectory()
		{
			var reporter = typeof(ExceptionHandler).GetMethod("HandleFatalError",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
				null, new[] { typeof(object), typeof(string) }, null);
			Assert.That(reporter, Is.Not.Null,
				"The AppDomain callback needs an object overload and must not depend on an initialized Platform.SupportDir.");

			var supportDirectory = Path.Combine(Path.GetTempPath(), $"openra-fatal-{System.Guid.NewGuid():N}");
			try
			{
				Directory.CreateDirectory(supportDirectory);
				reporter!.Invoke(null, new object[] { "native-non-exception-payload", supportDirectory });

				var logsDirectory = Path.Combine(supportDirectory, "Logs");
				var reports = Directory.GetFiles(logsDirectory, "exception-*.log");
				Assert.That(reports, Has.Length.EqualTo(1));
				Assert.That(File.ReadAllText(reports[0]), Does.Contain("native-non-exception-payload"));
			}
			finally
			{
				if (Directory.Exists(supportDirectory))
					Directory.Delete(supportDirectory, recursive: true);
			}
		}

		public sealed class ThrowingMessageException : System.Exception
		{
			public override string Message => throw new System.InvalidOperationException("metadata unavailable");
		}

		[Test]
		public void BenchmarkClockStartsAfterWorldLoadingCompletes()
		{
			var source = File.ReadAllText(Path.Combine(
				RepositoryRoot(), "engine", "OpenRA.Game", "Game.cs"));
			var afterGameStart = source.IndexOf("AfterGameStart();", System.StringComparison.Ordinal);
			var benchmarkReset = source.IndexOf("benchmark?.BeginAutomatic();", afterGameStart, System.StringComparison.Ordinal);

			Assert.That(afterGameStart, Is.GreaterThanOrEqualTo(0));
			Assert.That(benchmarkReset, Is.GreaterThan(afterGameStart));
			StringAssert.Contains("if (benchmark?.Ready == true)", source);
		}
	}
}
