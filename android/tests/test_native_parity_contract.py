"""Source integration guards; device testing remains required for EGL/network/media."""
from pathlib import Path
import unittest
ROOT = Path(__file__).resolve().parents[1] / 'OpenRA.Android'
class NativeParityContract(unittest.TestCase):
    def test_game_surface_requests_high_refresh_after_recreation(self):
        bridge = (ROOT / 'java/org/libsdl/app/AndroidHostBridge.java').read_text()
        surface = (ROOT / 'java/org/libsdl/app/SDLSurface.java').read_text()
        self.assertIn('requestHighRefreshRate', bridge)
        self.assertIn('surface.setFrameRate', bridge)
        self.assertIn('Surface.FRAME_RATE_COMPATIBILITY_DEFAULT', bridge)
        self.assertIn('AndroidHostBridge.requestHighRefreshRate()', surface)

    def test_metrics_publish_dp_and_observe_layout(self):
        metrics = (ROOT / 'AndroidMobileUiMetricsProvider.cs').read_text()
        activity = (ROOT / 'MainActivity.cs').read_text()
        self.assertIn('IosScreenMetrics.Publish', metrics)
        self.assertIn('surfaceWidth / densityForDp', metrics)
        self.assertIn('OnConfigurationChanged', activity)
        self.assertIn('LayoutChange +=', activity)
        self.assertIn('SetOnApplyWindowInsetsListener', activity)
    def test_native_actions_are_wired(self):
        host = (ROOT / 'AndroidOpenRaHost.cs').read_text()
        for name in ['OpenContentManagement', 'OpenSupportPage', 'OpenSupportersPage']:
            self.assertIn('Game.' + name + ' =', host)
        self.assertIn('returnToGame', (ROOT / 'MainActivity.cs').read_text())
    def test_lan_lock_has_permission_and_lifetime(self):
        self.assertIn('CHANGE_WIFI_MULTICAST_STATE', (ROOT / 'Properties/AndroidManifest.xml').read_text())
        activity = (ROOT / 'MainActivity.cs').read_text()
        self.assertIn('CreateMulticastLock', activity)
        self.assertIn('multicastLock.Release()', activity)
    def test_file_picker_supports_files_and_folders(self):
        activity = (ROOT / 'MainActivity.cs').read_text()
        self.assertIn('Intent.ActionOpenDocument', activity)
        self.assertIn('Intent.ExtraAllowMultiple', activity)
        self.assertIn('CopySelectedFile', activity)
    def test_native_dialogs_resolve_saved_language(self):
        activity = (ROOT / 'MainActivity.cs').read_text()
        self.assertIn('LanguageSelectionPolicy.Resolve', activity)
        self.assertIn('NodeWithKeyOrDefault("Language")', activity)
    def test_android_haptics_preserve_strength_and_have_permission(self):
        platform = ROOT.parent / 'OpenRA.Platforms.Android'
        self.assertIn('new AndroidHapticEngine()', (platform / 'AndroidPlatform.cs').read_text())
        self.assertIn('android.permission.VIBRATE', (ROOT / 'Properties/AndroidManifest.xml').read_text())
        source = (platform / 'AndroidHapticEngine.cs').read_text()
        self.assertIn('Math.Clamp(intensity, 0f, 1f)', source)
        self.assertIn('VibrationEffect.CreateOneShot', source)
        self.assertIn('HasAmplitudeControl', source)
        self.assertIn('vibrator.Cancel()', source)
        self.assertIn('AndroidHapticEngine.cs', (platform / 'OpenRA.Platforms.Android.csproj').read_text())
    def test_native_pages_share_ios_art_and_failures_are_visible(self):
        activity = (ROOT / 'MainActivity.cs').read_text()
        self.assertIn('CreateArtPage("cc-soviet-subpage-import.png")', activity)
        pages = (ROOT / 'MainActivityNativePages.cs').read_text()
        self.assertIn('cc-soviet-subpage-legal.png', pages)
        self.assertIn('cc-special-thanks-hall-ios-phone.png', pages)
        self.assertIn('ShowEngineFailure', (ROOT / 'AndroidOpenRaHost.cs').read_text())
    def test_engine_protocol_version_is_packaged(self):
        project = (ROOT / 'OpenRA.Android.csproj').read_text()
        self.assertIn('Include="../../engine/VERSION"', project)
        self.assertIn('<Link>runtime/engine/VERSION</Link>', project)
    def test_back_routes_through_sdl_escape_after_native_overlays(self):
        activity = (ROOT / 'MainActivity.cs').read_text()
        back = activity.split('public override void OnBackPressed()', 1)[1].split('protected override void OnPause()', 1)[0]
        self.assertIn('CallBridge("sendEscapeKey")', back)
        self.assertLess(back.index('nativeDetailPage'), back.index('CallBridge("sendEscapeKey")'))
        self.assertLess(back.index('returnToGame'), back.index('CallBridge("sendEscapeKey")'))
        self.assertNotIn('PhoneBackState.ModalOpen', back)
        self.assertIn('DispatchKeyEvent', activity)
        bridge = (ROOT / 'java/org/libsdl/app/AndroidHostBridge.java').read_text()
        self.assertIn('SDLActivity.onNativeKeyDown(KeyEvent.KEYCODE_ESCAPE)', bridge)
        self.assertIn('SDLActivity.onNativeKeyUp(KeyEvent.KEYCODE_ESCAPE)', bridge)
    def test_original_runtime_maps_are_installed_before_engine_boot(self):
        host = (ROOT / 'AndroidOpenRaHost.cs').read_text()
        unpack = host.index('EnsureRuntimeContent(supportPath);')
        shell = host.index('RuntimeShellmapInstaller.EnsureInstalled(supportPath);')
        playable = host.index('RuntimeShellmapInstaller.EnsurePlayableMapInstalled(supportPath);')
        boot = host.index('Game.InitializeAndRun(launchArgs.ToArray());')
        self.assertLess(unpack, shell)
        self.assertLess(shell, playable)
        self.assertLess(playable, boot)
    def test_ranked_credentials_use_android_keystore_and_matching_boot_flags(self):
        host = (ROOT / 'AndroidOpenRaHost.cs').read_text()
        self.assertIn('RankedCredentialStoreFactory.RegisterPlatformFactory', host)
        self.assertIn('Game.EnableRanked=true', host)
        source = (ROOT / 'AndroidRankedCredentialStore.cs').read_text()
        self.assertIn('AndroidKeyStore', source)
        self.assertIn('AES/GCM/NoPadding', source)
        self.assertNotIn('PutString(PreferenceKey, JsonSerializer', source)
    def test_failure_reports_and_memory_lifecycle_are_available(self):
        host = (ROOT / 'AndroidOpenRaHost.cs').read_text()
        activity = (ROOT / 'MainActivity.cs').read_text()
        pages = (ROOT / 'MainActivityNativePages.cs').read_text()
        self.assertIn('CaptureFailure', host)
        self.assertIn('OnTrimMemory', activity)
        self.assertIn('HangDiagnostics.Pause()', activity)
        self.assertIn('Copy diagnostic report', pages)
        self.assertIn('Intent.ActionCreateDocument', activity)
    def test_host_never_exports_raw_logs_settings_or_exception_text(self):
        host = (ROOT / 'AndroidOpenRaHost.cs').read_text()
        for raw in ['ExportDebugLogs', 'CopyDirectory', 'MakeWorldReadable', 'File.Copy(settings', 'e.ToString()', 'e.Message', 'TimeSpan.FromSeconds(15)']:
            self.assertNotIn(raw, host)
        self.assertIn('report.Id', host)
        self.assertIn('report.TryPersist', host)
        pages = (ROOT / 'MainActivityNativePages.cs').read_text()
        self.assertIn('Copy diagnostic report', pages)
        self.assertIn('ExportFailureReport(report)', pages)
    def test_decrypted_buffer_is_cleared_even_when_size_is_invalid(self):
        source = (ROOT / 'AndroidRankedCredentialStore.cs').read_text().split('public RankedStoredCredential? Load()', 1)[1].split('public void Save', 1)[0]
        plaintext = source.index('var plaintext =')
        self.assertLess(source.index('try', plaintext), source.index('if (plaintext.Length', plaintext))
        self.assertIn('finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext); }', source)
    def test_configuration_refresh_preserves_one_engine_and_rebuilds_native_views(self):
        activity = (ROOT / 'MainActivity.cs').read_text()
        for flag in ['Density', 'FontScale', 'Locale', 'LayoutDirection', 'SmallestScreenSize']:
            self.assertIn('ConfigChanges.' + flag, activity)
        handler = activity.split('public override void OnConfigurationChanged', 1)[1].split('void AcquireLanDiscoveryLock', 1)[0]
        for refresh in ['RefreshNativeLanguage()', 'RefreshMobileMetrics()', 'RebuildVisibleNativePages()', 'RequestApplyInsets()']:
            self.assertIn(refresh, handler)
        self.assertNotIn('StartHost', handler)
        self.assertNotIn('KillProcess', handler)
        pages = (ROOT / 'MainActivityNativePages.cs').read_text()
        self.assertIn('refreshNativePage = ShowThanksPage', pages)
        self.assertIn('refreshNativePage = ShowLicensePage', pages)
        self.assertIn('refreshNativePage = ShowContentDetails', pages)
    def test_video_is_registered(self):
        self.assertIn('MenuVideoSourceFactory.AndroidOpen =', (ROOT / 'AndroidOpenRaHost.cs').read_text())
        self.assertTrue((ROOT / 'AndroidMenuVideoSource.cs').exists())
if __name__ == '__main__': unittest.main()
