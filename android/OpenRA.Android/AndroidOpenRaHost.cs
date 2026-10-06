using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Android.App;
using Android.Runtime;
using Android.Content.Res;
using Android.Util;
using ALog = Android.Util.Log;
using OpenRA;
using OpenRA.Platforms.Default;
using OpenRA.Network;
using OpenRA.Support;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Android;

/// <summary>
/// Runs OpenRA.Game.InitializeAndRun on the managed host thread once the
/// Android surface exists (Phase 7). The engine owns the SDL window and render
/// loop afterwards; the Java glue already performed SDL library loading and
/// SDL_SetupJNI, so SDL_SetMainReady has fired.
/// </summary>
public sealed class AndroidOpenRaHost
{
    const string Tag = "OpenRA.Host";
    const string RuntimeContentVersionFile = ".apk-runtime-last-update";

    readonly Activity activity;
    readonly Func<bool> surfaceReady;
    readonly Func<int> surfaceWidth;
    readonly Func<int> surfaceHeight;

    Thread? thread;
    volatile bool running;

    public AndroidOpenRaHost(Activity activity, Func<bool> surfaceReady, Func<int> surfaceWidth, Func<int> surfaceHeight)
    {
        this.activity = activity;
        this.surfaceReady = surfaceReady;
        this.surfaceWidth = surfaceWidth;
        this.surfaceHeight = surfaceHeight;
    }

    public void Start()
    {
        running = true;
        thread = new Thread(Run) { Name = "OpenRA", IsBackground = true };
        thread.Start();
    }

    public void ReloadImportedContent()
    {
        Game.RunAfterTick(() =>
        {
            ALog.Info(Tag, "Retail reload: rebuilding RA2 resources and maps");
            Game.InitializeMod("ra2", new Arguments());
            ALog.Info(Tag, "Retail reload: main menu ready");
        });
    }

    public void Stop()
    {
        running = false;
        // Game.InitializeAndRun owns its loop; Stop is best-effort for teardown.
        thread?.Join(1000);
    }

    void Run()
    {
        ALog.Info(Tag, "OpenRA thread started: managed id=" + System.Environment.CurrentManagedThreadId);

        // Wait until the Android surface exists (mirrors the SDL POC wait).
        while (running && !surfaceReady())
            Thread.Sleep(100);

        if (!running)
            return;

        var width = Math.Max(1, surfaceWidth());
        var height = Math.Max(1, surfaceHeight());
        ALog.Info(Tag, $"surface ready: {width}x{height}");

        using var failureDiagnostics = new AndroidFailureDiagnostics(
            activity is MainActivity nativeHost ? nativeHost.ProductVersionText() : "NUKE HOUR Android");
        using var performance = new AndroidRuntimePerformanceController(() =>
            global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Q &&
            activity.GetSystemService(global::Android.Content.Context.PowerService) is global::Android.OS.PowerManager power
                ? (int)power.CurrentThermalStatus : -1);
        if (activity is MainActivity performanceHost) performanceHost.RuntimePerformance = performance;
        void CaptureUnhandled(object? sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception error)
                failureDiagnostics.CaptureFailure(error).TryPersist(
                    Path.Combine(activity.FilesDir!.AbsolutePath, "openra", "Diagnostics", "Failures"), out _);
        }
        AppDomain.CurrentDomain.UnhandledException += CaptureUnhandled;
        if (activity is MainActivity diagnosticsHost) diagnosticsHost.FailureDiagnostics = failureDiagnostics;
        try
        {
            // Capture screen metrics once before engine bootstrap (density,
            // font scale, insets, refresh rate) so UI code never queries the
            // platform per frame.
            var supportDirectory = Path.Combine(activity.FilesDir?.AbsolutePath ?? "", "openra");
            var uiScale = ComputeUiScale(supportDirectory, width, height);
            var uiScaleArg = float.Parse(uiScale, System.Globalization.CultureInfo.InvariantCulture);
            AndroidMobileUiMetricsProvider.UpdateFrom(activity, width, height, uiScaleArg);

            var filesDir = activity.FilesDir?.AbsolutePath
                ?? throw new InvalidOperationException("FilesDir unavailable");
            var supportPath = Path.Combine(filesDir, "openra");
            var enginePath = Path.Combine(supportPath, "engine");
            Directory.CreateDirectory(supportPath);
            Directory.CreateDirectory(enginePath);

            // Plain-APK provisioning: unpack the engine/RA2 runtime content
            // embedded as Android assets. An APK upgrade refreshes all bundled
            // files, while saves, settings, maps, and retail content remain
            // outside the bundled tree and are left untouched.
            EnsureRuntimeContent(supportPath);

            // The current mod loads maps exclusively from writable SupportDir.
            // Generate the same original menu world and starter battlefield as
            // iOS before MapCache enumerates those directories during startup.
            RuntimeShellmapInstaller.EnsureInstalled(supportPath);
            RuntimeShellmapInstaller.EnsurePlayableMapInstalled(supportPath);

            // Engine runtime database: explicit status instead of silent
            // all-mixes-missing behaviour. States: missing | unreadable |
            // invalid hash | wrong destination | ready.
            var dbState = EnsureGlobalMixDatabase(activity.Assets, enginePath);
            ALog.Info(Tag, $"[engine-runtime] global mix database.dat: {dbState}");

            // Platform override must precede any Platform.* access.
            Platform.OverrideHostPlatform(PlatformType.Android);
            RankedCredentialStoreFactory.RegisterPlatformFactory(() => new AndroidRankedCredentialStore());
            if (activity is MainActivity versionHost)
                AppDomain.CurrentDomain.SetData(ModMetadata.HostProductVersionKey, versionHost.ProductVersionText());
            HangDiagnostics.Start(supportPath);
            HangDiagnostics.Resume();
            RuntimeResourceSampler.ThermalStateProvider = () =>
                global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.Q &&
                activity.GetSystemService(global::Android.Content.Context.PowerService) is global::Android.OS.PowerManager power ?
                    power.CurrentThermalStatus.ToString() : "unavailable";

            var launchArgs = new List<string>
            {
                $"Engine.EngineDir={enginePath}",
                $"Engine.SupportDir={supportPath}",
                $"Engine.ModSearchPaths={Path.Combine(enginePath, "mods")},{Path.Combine(supportPath, "mods")}",
                "Game.Mod=ra2",
                "Game.EnableRanked=true",
                "Game.RankedLobbyUrl=https://lobby.superaitest.com/",
                "Game.RankedRegion=cn-hangzhou",
                "Game.UseClassicMouseStyle=true",
                "Graphics.Mode=Fullscreen",
                $"Graphics.FullscreenSize={width},{height}",
                $"Graphics.UIScale={uiScale}"
            };

            Game.PlatformFactoryOverride = _ => new AndroidPlatform();
            if (activity is MainActivity nativeActivity)
            {
                Game.OpenContentManagement = () =>
                {
                    if (Game.OrderManager?.World is { Type: not WorldType.Shellmap })
                    {
                        activity.RunOnUiThread(() => global::Android.Widget.Toast.MakeText(activity,
                            StartupCopy.IsChinese ? "请返回主菜单后导入资源。" : "Return to the main menu before importing resources.",
                            global::Android.Widget.ToastLength.Long)?.Show());
                        return;
                    }
                    activity.RunOnUiThread(nativeActivity.OpenContentManagement);
                };
                Game.OpenSpecialThanks = () => activity.RunOnUiThread(nativeActivity.ShowAcknowledgements);
                Game.OpenSupportPage = () => activity.RunOnUiThread(() => nativeActivity.OpenOfficialPage("https://nukehour.com/supporters#support"));
                Game.OpenSupportersPage = () => activity.RunOnUiThread(() => nativeActivity.OpenOfficialPage("https://nukehour.com/supporters"));
            }
            OpenRA.Mods.RA2.Widgets.MenuVideoSourceFactory.AndroidOpen = path => new AndroidMenuVideoSource(path);

            global::OpenRA.Platforms.Default.AndroidPlatform.RequestRenderFrameRate = frameRate =>
            {
                var bridge = JNIEnv.FindClass("org/libsdl/app/AndroidHostBridge");
                var request = JNIEnv.GetStaticMethodID(bridge, "requestRenderFrameRate", "(F)V");
                JNIEnv.CallStaticVoidMethod(bridge, request, new JValue(frameRate));
                // FindClass returns a cached global reference owned by the Android runtime.
            };
            OpenRA.Graphics.Sheet.PlatformBgraDecoder = AndroidSheetDecoder.Decode;
            ALog.Info(Tag, "BEGIN: Game.InitializeAndRun");
            Game.InitializeAndRun(launchArgs.ToArray());
            ALog.Info(Tag, "END: Game.InitializeAndRun returned");

            // The engine loop has ended: finish the process so the last frame
            // never lingers as a frozen screen.
            global::Android.OS.Process.KillProcess(global::Android.OS.Process.MyPid());
        }
        catch (Exception e)
        {
            var report = failureDiagnostics.CaptureFailure(e);
            ALog.Error(Tag, $"OpenRA failure report={report.Id} type={e.GetType().Name}");
            report.TryPersist(Path.Combine(activity.FilesDir!.AbsolutePath, "openra", "Diagnostics", "Failures"), out _);
            // Keep the failure visible: resource loss cannot resume with stale GL objects.
            if (activity is MainActivity nativeActivity)
                activity.RunOnUiThread(() => nativeActivity.ShowEngineFailure(e, report));
        }
        finally
        {
            AppDomain.CurrentDomain.UnhandledException -= CaptureUnhandled;
            if (activity is MainActivity diagnosticsActivity)
            {
                diagnosticsActivity.FailureDiagnostics = null;
                diagnosticsActivity.RuntimePerformance = null;
            }
        }
    }

    void EnsureRuntimeContent(string supportPath)
    {
        try
        {
            var markerPath = Path.Combine(supportPath, RuntimeContentVersionFile);
            var packageInfo = activity.PackageManager?.GetPackageInfo(
                activity.PackageName ?? string.Empty,
                global::Android.Content.PM.PackageInfoFlags.MetaData);
            var packageLastUpdate = (packageInfo?.LastUpdateTime ?? 0L)
                .ToString(CultureInfo.InvariantCulture);
            var refreshExisting = !File.Exists(markerPath)
                || File.ReadAllText(markerPath).Trim() != packageLastUpdate;

            var copied = 0;
            var skipped = 0;
            CopyRuntimeTree("runtime", supportPath, refreshExisting, ref copied, ref skipped);

            if (refreshExisting)
            {
                var markerTmp = markerPath + ".tmp-" + Guid.NewGuid().ToString("N");
                try
                {
                    File.WriteAllText(markerTmp, packageLastUpdate + "\n");
                    File.Move(markerTmp, markerPath, true);
                }
                finally
                {
                    if (File.Exists(markerTmp))
                        File.Delete(markerTmp);
                }
            }

            ALog.Info(Tag, $"[runtime-content] refreshed={refreshExisting} copied={copied} existing={skipped}");
        }
        catch (Exception e)
        {
            ALog.Warn(Tag, "[runtime-content] extraction failed: " + e.GetType().Name);
        }
    }

    void CopyRuntimeTree(
        string assetPath,
        string destDir,
        bool refreshExisting,
        ref int copied,
        ref int skipped)
    {
        var entries = activity.Assets.List(assetPath);
        // Android AssetManager.List returns an empty array for a file.
        if (entries == null || entries.Length == 0)
        {
            if (refreshExisting || !File.Exists(destDir))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destDir));
                var tmp = destDir + ".apk-" + Guid.NewGuid().ToString("N");
                try
                {
                    using var input = activity.Assets.Open(assetPath);
                    using (var output = File.Create(tmp))
                        input.CopyTo(output);

                    File.Move(tmp, destDir, true);
                }
                finally
                {
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }

                copied++;
            }
            else
                skipped++;

            return;
        }

        Directory.CreateDirectory(destDir);
        foreach (var entry in entries)
            CopyRuntimeTree(
                assetPath + "/" + entry,
                Path.Combine(destDir, entry),
                refreshExisting,
                ref copied,
                ref skipped);
    }

    /// POC UI scale: scale up the (desktop-layout) RA2 UI so side bars and
    /// buttons are usable on high-density phone screens. Base ~540 logical
    /// height, override with a ui-scale.txt file in the support dir.
    internal static string ComputeUiScale(string supportPath, int width, int height)
    {
        try
        {
            var overridePath = Path.Combine(supportPath, "ui-scale.txt");
            if (File.Exists(overridePath) && float.TryParse(File.ReadAllText(overridePath).Trim(),
                    System.Globalization.CultureInfo.InvariantCulture, out var overrideScale)
                && overrideScale >= 0.75f && overrideScale <= 4f)
            {
                ALog.Info(Tag, $"[ui] ui-scale.txt override: {overrideScale}");
                return overrideScale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        catch (Exception e)
        {
            ALog.Warn(Tag, $"[ui] ui-scale.txt unreadable: {e.GetType().Name}");
        }

        // In-match UI (side bars, production menus, lobby) is authored for
        // desktop logical pixels; 1.6 makes those controls phone-usable while
        // the re-authored phone main menu stays large. Override via
        // ui-scale.txt when tuning.
        ALog.Info(Tag, $"[ui] default UIScale=1.6 (surface {width}x{height})");
        return "1.6";
    }

    static string EnsureGlobalMixDatabase(AssetManager assets, string enginePath)
    {
        const string assetName = "runtime/global mix database.dat";
        var dest = Path.Combine(enginePath, "global mix database.dat");

        try
        {
            byte[] data;
            using (var stream = assets.Open(assetName))
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                data = ms.ToArray();
            }

            if (data.Length == 0)
                return "unreadable";

            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var want = sha.ComputeHash(data);

                // Fast path: destination already matches the embedded database.
                if (File.Exists(dest))
                {
                    var existing = File.ReadAllBytes(dest);
                    var have = sha.ComputeHash(existing);
                    if (have.AsSpan().SequenceEqual(want))
                        return "ready";
                }

                // Atomic replace: temp file in the same directory + rename.
                var tmp = Path.Combine(enginePath, "global mix database.dat.tmp-" + Guid.NewGuid().ToString("N"));
                try
                {
                    File.WriteAllBytes(tmp, data);
                    var tmpHash = sha.ComputeHash(File.ReadAllBytes(tmp));
                    if (!tmpHash.AsSpan().SequenceEqual(want))
                        return "invalid hash";

                    File.Move(tmp, dest, true);

                    var finalHash = sha.ComputeHash(File.ReadAllBytes(dest));
                    if (!finalHash.AsSpan().SequenceEqual(want))
                        return "wrong destination";
                }
                finally
                {
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }
            }

            return "ready";
        }
        catch (Java.IO.FileNotFoundException)
        {
            return "missing";
        }
        catch (System.IO.IOException e)
        {
            ALog.Warn(Tag, $"[engine-runtime] db copy I/O: {e.GetType().Name}");
            return "unreadable";
        }
        catch (Exception e)
        {
            ALog.Warn(Tag, $"[engine-runtime] db copy failed: {e.GetType().Name} {e.GetType().Name}");
            return "unreadable";
        }
    }

}
