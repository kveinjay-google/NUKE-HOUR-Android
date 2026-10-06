using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Configuration = Android.Content.Res.Configuration;
using Android.Net.Wifi;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Util;
using AndroidUri = Android.Net.Uri;
using ALog = Android.Util.Log;
using Android.Views;
using Android.Widget;
using OpenRA.Mods.RA2.Content;
using OpenRA.Support;
using OpenRA.Network;

namespace OpenRA.Android;

sealed class StartupCopy
{
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string Body { get; init; }
    public required string OfficialWebsite { get; init; }
    public required string ImportFiles { get; init; }
    public required string ViewLicense { get; init; }
    public required string Acknowledgements { get; init; }
    public required string Close { get; init; }
    public required string Supporters { get; init; }
    public required string Imported { get; init; }
    public required string NotImported { get; init; }
    public required string EnterGame { get; init; }
    public required string Ready { get; init; }
    public required string Missing { get; init; }
    public required string MissingMaps { get; init; }
    public required string ImportingTitle { get; init; }
    public required string Importing { get; init; }
    public required string ImportFailedTitle { get; init; }
    public required string ImportFailed { get; init; }
    public required string ImportSucceeded { get; init; }
    public required string WebsiteFailed { get; init; }

    public static string EffectiveLanguage { get; set; } = LanguageSelectionPolicy.English;
    public static bool IsChinese => EffectiveLanguage == LanguageSelectionPolicy.SimplifiedChinese;

    public static StartupCopy Current =>
        StartupCopy.IsChinese
            ? Chinese
            : English;

    static readonly StartupCopy Chinese = new()
    {
        Title = "使用与发行声明",
        Subtitle = "请在继续前阅读以下重要信息",
        Body = "NUKE HOUR 是完全独立开发、完全免费且开源的软件项目。\n\n" +
            "EA 未认可且不支持本产品。本项目与 EA、Apple 或 Google 不存在隶属、赞助、授权或支持关系。\n\n" +
            "本软件不包含任何第三方零售游戏文件。用户必须从自己合法购买并拥有的正版游戏副本中手动导入兼容文件。" +
            "界面中出现的游戏或软件名称仅用于说明可兼容导入文件的来源，不代表关联、授权、认可或支持。\n\n" +
            "本软件完全免费开源。从任何地方付费购买本软件都属于受骗上当；请向作者举报销售者，并向销售渠道申请退款。\n\n" +
            "请只从 NUKE HOUR 官方网站下载官方授权版本。不要从官方网站以外的任何地方下载安装，以防软件被篡改或植入恶意代码。",
        OfficialWebsite = "官方网站",
        ImportFiles = "导入正版游戏文件",
        ViewLicense = "重要声明与许可证",
        Acknowledgements = "鸣谢",
        Close = "关闭",
        Supporters = "支持者\n\n美食\n北海\n小杰\n美食4\n姜\nLonely\n龙华力宏\n许\nZzz77zzZ\nYung\n\n社区贡献\n\nOpenRA Contributors",
        Imported = "已导入",
        NotImported = "未导入",
        EnterGame = "进入游戏",
        Ready = "正版游戏文件已就绪，可以进入游戏。",
        Missing = "尚缺少必需文件：",
        MissingMaps = "尚缺少遭遇战地图。请导入完整游戏目录，并确保包含 multi.mix（红警 2）或 multimd.mix（尤里的复仇）。",
        ImportingTitle = "正在导入",
        Importing = "正在复制并校验正版游戏文件（较大文件可能需要一会儿）…",
        ImportFailedTitle = "导入失败",
        ImportFailed = "导入失败。请选择包含所需正版游戏文件的正确目录后重试。",
        ImportSucceeded = "导入完成。请点击“进入游戏”继续。",
        WebsiteFailed = "无法打开浏览器。请手动访问 https://nukehour.com。",
    };

    static readonly StartupCopy English = new()
    {
        Title = "Use and Distribution Notice",
        Subtitle = "Please read this important information before continuing",
        Body = "NUKE HOUR is a completely independent, free, and open-source software project.\n\n" +
            "EA has not endorsed and does not support this product. This project is not affiliated with, sponsored by, authorized by, or supported by EA, Apple, or Google.\n\n" +
            "This software contains no third-party retail game files. Users must manually import compatible files from a legally purchased and owned copy of the original game. " +
            "Game or software names shown in the interface are plain-text references used only to identify compatible import sources; they do not imply affiliation, authorization, endorsement, or support.\n\n" +
            "This software is entirely free and open source. If anyone charges you for this software, you have been deceived. Please report the seller to the author and request a refund from the marketplace or payment provider.\n\n" +
            "Download only the official authorized version from the NUKE HOUR website. Do not install copies obtained elsewhere, because they may have been modified or contain malicious code.",
        OfficialWebsite = "Official Website",
        ImportFiles = "Import Legally Owned Game Files",
        ViewLicense = "Important Notice & Licenses",
        Acknowledgements = "Acknowledgements",
        Close = "Close",
        Supporters = "SUPPORTERS\n\n美食\n北海\n小杰\n美食4\n姜\nLonely\n龙华力宏\n许\nZzz77zzZ\nYung\n\nCOMMUNITY CONTRIBUTORS\n\nOpenRA Contributors",
        Imported = "Imported",
        NotImported = "Not imported",
        EnterGame = "Enter Game",
        Ready = "Required game files are ready. You can enter the game.",
        Missing = "Required files are still missing: ",
        MissingMaps = "Skirmish maps are missing. Import the full game folder, including multi.mix (RA2) or multimd.mix (Yuri's Revenge).",
        ImportingTitle = "Importing",
        Importing = "Copying and validating legally owned game files. Large files may take a while…",
        ImportFailedTitle = "Import Failed",
        ImportFailed = "Import failed. Select the correct folder containing the required legally owned game files and try again.",
        ImportSucceeded = "Import complete. Select Enter Game to continue.",
        WebsiteFailed = "Unable to open a browser. Visit https://nukehour.com manually.",
    };
}

/// <summary>
/// Android POC launcher (net8.0-android managed host, Model B ownership: the
/// managed activity owns the lifecycle and hosts SDL's Java surface glue, then
/// starts OpenRA's engine host).
///
/// The decision and evidence for this host model are recorded in
/// docs/ADR_ANDROID_HOST_OWNERSHIP.md.
/// </summary>
[Activity(
    Label = "NUKE HOUR",
    MainLauncher = true,
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen",
    ScreenOrientation = ScreenOrientation.Landscape,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.ScreenLayout
        | ConfigChanges.KeyboardHidden
        | ConfigChanges.UiMode
        | ConfigChanges.Density
        | ConfigChanges.FontScale
        | ConfigChanges.Locale
        | ConfigChanges.LayoutDirection
        | ConfigChanges.SmallestScreenSize)]
public sealed partial class MainActivity : Activity
{
    const string Tag = "OpenRA.Android";
    const int ImportTreeRequest = 1101;
    const int ImportFilesRequest = 1102;
    const int ExportFailureRequest = 1103;
    internal AndroidFailureDiagnostics? FailureDiagnostics;
    internal AndroidRuntimePerformanceController? RuntimePerformance;
    string? pendingFailureExport;

    AndroidOpenRaHost? openRaHost;
    bool hostStarted;
    bool externalActivityInProgress;
    bool returnToGame;
    WifiManager.MulticastLock? multicastLock;
    readonly RetailContentCatalog retailCatalog = new();
    AlertDialog? importDialog;
    FrameLayout? rootLayout;
    internal View? GameSurface { get; private set; }
    View? startupOverlay;
    TextView? importStatusLabel;
    ScrollView? resourceStatusScroll;
    LinearLayout? resourceStatusList;
    Button? enterGameButton;

    string SupportPath => Path.Combine(FilesDir?.AbsolutePath ?? string.Empty, "openra");
    string ContentRoot => Path.Combine(SupportPath, "Content");
    string RetailContentDir => Path.Combine(ContentRoot, "ra2");

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RefreshNativeLanguage();
        if (Intent?.GetBooleanExtra("nukehour-keystore-selftest", false) == true)
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try { AndroidRankedCredentialStore.RunIsolatedSelfTest(); }
                catch (Exception error) { ALog.Error("OpenRA.Keystore", "SELFTEST FAIL " + error.GetType().Name); }
            });

        ALog.Info(Tag, "OnCreate: managed host starting (net8.0-android)");
        ALog.Info(Tag, $"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        ALog.Info(Tag, $"Package: {PackageName}");
        ALog.Info(Tag, $"FilesDir: {FilesDir?.AbsolutePath}");
        ALog.Info(Tag, "Device: " + $"{Build.Manufacturer} {Build.Model} (api {Build.VERSION.SdkInt}, abi {Build.SupportedAbis?[0]})");

        RequestedOrientation = ScreenOrientation.Landscape;

        Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        Window?.AddFlags(WindowManagerFlags.Fullscreen);

        // Opaque black window background: any transient strip that the engine
        // does not paint (boot, rotation, resize) must never show wallpaper.
        Window?.SetBackgroundDrawable(
            new global::Android.Graphics.Drawables.ColorDrawable(global::Android.Graphics.Color.Black));

        // Edge-to-edge: draw behind the display cutout and the system bars so
        // the surface spans the whole physical display (no letterboxed strips
        // left/right on punch-hole phones in landscape).
        var attributes = Window?.Attributes;
        if (attributes != null)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.Always;
            else if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
                attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
        }

        Window?.AddFlags(WindowManagerFlags.LayoutNoLimits);

        var decor = Window?.DecorView;
        if (decor != null && Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat)
            decor.SystemUiFlags = SystemUiFlags.ImmersiveSticky | SystemUiFlags.Fullscreen
                | SystemUiFlags.HideNavigation | SystemUiFlags.LayoutStable
                | SystemUiFlags.LayoutFullscreen | SystemUiFlags.LayoutHideNavigation;

        // Create the SDLSurface through the Java bridge. Loading the bridge
        // class runs its static initializer, which loads libSDL2.so from a Java
        // context and mirrors SDLActivity.onCreate's JNI setup.
        var bridgeClass = JNIEnv.FindClass("org/libsdl/app/AndroidHostBridge");
        var createSurfaceId = JNIEnv.GetStaticMethodID(
            bridgeClass, "createSDLSurface", "(Landroid/app/Activity;)Landroid/view/View;");
        var viewHandle = JNIEnv.CallStaticObjectMethod(bridgeClass, createSurfaceId, new JValue(this));
        var sdlView = Java.Lang.Object.GetObject<View>(viewHandle, JniHandleOwnership.TransferLocalRef)
            ?? throw new InvalidOperationException("AndroidHostBridge.createSDLSurface returned null");

        GameSurface = sdlView;
        rootLayout = new FrameLayout(this)
        {
            LayoutParameters = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent)
        };
        rootLayout.AddView(sdlView, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        SetContentView(rootLayout);
        sdlView.LayoutChange += (_, _) => RefreshMobileMetrics();
        rootLayout.SetOnApplyWindowInsetsListener(new MetricsInsetsListener(this));
        rootLayout.RequestApplyInsets();
        ALog.Info(Tag, "SDLSurface added to content view");

        openRaHost = new AndroidOpenRaHost(
            this,
            surfaceReady: IsSdlSurfaceReady,
            surfaceWidth: SdlSurfaceWidth,
            surfaceHeight: SdlSurfaceHeight);

        try
        {
            MigrateLegacyImportRoot();
            RetailContentImporter.NormalizeInstalledContent(ContentRoot);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            ALog.Error(Tag, "Retail content migration failed: " + e.GetType().Name);
        }

        // The APK is plain and contains no retail game files. Every fresh
        // process shows the notice, even when previously imported content is
        // ready. Starting the engine always requires an explicit user action.
        ShowStartupNotice();
    }

    internal void OpenContentManagement()
    {
        RefreshNativeLanguage();
        returnToGame = hostStarted;
        if (startupOverlay == null)
            ShowStartupNotice();
    }

    void RefreshNativeLanguage()
    {
        var preference = LanguageSelectionPolicy.SystemPreference;
        try
        {
            var settingsPath = Path.Combine(SupportPath, "settings.yaml");
            if (File.Exists(settingsPath))
            {
                var root = new MiniYaml(string.Empty, MiniYaml.FromFile(settingsPath));
                preference = root.NodeWithKeyOrDefault("Game")?.Value
                    .NodeWithKeyOrDefault("Language")?.Value.Value ?? preference;
            }
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is YamlException)
        {
            ALog.Warn(Tag, "Unable to read native language preference: " + e.GetType().Name);
        }
        var systemLanguage = Resources?.Configuration?.Locales?.Get(0)?.ToLanguageTag() ?? CultureInfo.CurrentUICulture.Name;
        StartupCopy.EffectiveLanguage = LanguageSelectionPolicy.Resolve(preference, systemLanguage);
    }

    void CloseContentManagement()
    {
        if (startupOverlay != null)
            rootLayout?.RemoveView(startupOverlay);
        startupOverlay = null;
        returnToGame = false;
    }

    void RefreshMobileMetrics()
    {
        var width = SdlSurfaceWidth();
        var height = SdlSurfaceHeight();
        if (width <= 0 || height <= 0)
            return;
        var scale = float.Parse(AndroidOpenRaHost.ComputeUiScale(SupportPath, width, height),
            CultureInfo.InvariantCulture);
        AndroidMobileUiMetricsProvider.UpdateFrom(this, width, height, scale);
    }

    sealed class MetricsInsetsListener : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        readonly MainActivity activity;
        public MetricsInsetsListener(MainActivity activity) => this.activity = activity;
        public WindowInsets OnApplyWindowInsets(View? view, WindowInsets? insets)
        {
            // Read RootWindowInsets after Android commits this dispatch.
            view?.Post(activity.RefreshMobileMetrics);
            return insets!;
        }
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        rootLayout?.Post(() =>
        {
            RefreshNativeLanguage();
            RefreshMobileMetrics();
            RebuildVisibleNativePages();
            rootLayout?.RequestApplyInsets();
        });
    }

    void AcquireLanDiscoveryLock()
    {
        try
        {
            if (multicastLock == null && ApplicationContext?.GetSystemService(WifiService) is WifiManager wifi)
            {
                multicastLock = wifi.CreateMulticastLock("nukehour-lan-discovery");
                multicastLock?.SetReferenceCounted(false);
            }
            if (multicastLock != null && !multicastLock.IsHeld)
                multicastLock.Acquire();
        }
        catch (Exception e) { ALog.Warn(Tag, "Wi-Fi discovery lock unavailable: " + e.GetType().Name); }
    }

    void ReleaseLanDiscoveryLock()
    {
        if (multicastLock?.IsHeld == true)
            multicastLock.Release();
    }

    void StartHost()
    {
        if (hostStarted)
            return;

        hostStarted = true;
        AcquireLanDiscoveryLock();
        CallBridge("startManagedHost");
        openRaHost?.Start();
    }

    int Dp(float value) => (int)TypedValue.ApplyDimension(
        ComplexUnitType.Dip,
        value,
        Resources!.DisplayMetrics);

    TextView NoticeText(string text, float size, global::Android.Graphics.Color color)
    {
        var view = new TextView(this)
        {
            Text = text,
            TextSize = size,
        };
        view.SetTextColor(color);
        return view;
    }

    void ShowStartupNotice()
    {
        var copy = StartupCopy.Current;
        var overlay = CreateArtPage("cc-soviet-subpage-import.png");
        var outline = new global::Android.Graphics.Drawables.GradientDrawable();
        outline.SetColor(global::Android.Graphics.Color.Transparent);
        outline.SetCornerRadius(Dp(14));
        outline.SetStroke(Dp(2), global::Android.Graphics.Color.Rgb(151, 55, 39));
        overlay.Foreground = outline;
        overlay.LayoutChange += (_, _) => ApplySafeMargins(overlay, 12);
        var rootScroll = new ScrollView(this) { FillViewport = true };
        var content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        content.Background = PanelBackground(236, 10, 1);
        content.SetPadding(Dp(18), Dp(18), Dp(18), Dp(18));
        rootScroll.AddView(content, new FrameLayout.LayoutParams(-1, -2));
        var scrollFrame = new FrameLayout.LayoutParams(-1, -1);
        scrollFrame.SetMargins(Dp(18), Dp(18), Dp(18), Dp(48));
        overlay.AddView(rootScroll, scrollFrame);

        importStatusLabel = NoticeText(string.Empty, 22, Gold);
        importStatusLabel.SetTypeface(global::Android.Graphics.Typeface.Create("sans-serif", global::Android.Graphics.TypefaceStyle.Normal),
            global::Android.Graphics.TypefaceStyle.Normal);
        var statusParams = new LinearLayout.LayoutParams(-1, -2);
        statusParams.BottomMargin = Dp(12);
        content.AddView(importStatusLabel, statusParams);

        var fileCard = new LinearLayout(this) { Orientation = Orientation.Vertical };
        fileCard.Background = PanelBackground(236, 10, 1);
        fileCard.SetPadding(Dp(IsNativeTablet ? 12 : 10), 0, Dp(IsNativeTablet ? 12 : 10), Dp(10));
        var details = NativeButton(StartupCopy.IsChinese ? "资源文件状态 · 查看全部" : "Resource Status · View All", ShowContentDetails);
        details.SetTextColor(Gold);
        details.SetTypeface(details.Typeface, global::Android.Graphics.TypefaceStyle.Normal);
        details.SetTextSize(ComplexUnitType.Sp, 15);
        details.Background = null;
        fileCard.AddView(details, new LinearLayout.LayoutParams(-1, Dp(IsNativeTablet ? 42 : 36)));
        resourceStatusList = new LinearLayout(this) { Orientation = Orientation.Vertical };
        resourceStatusScroll = new ScrollView(this) { FillViewport = true };
        resourceStatusScroll.AddView(resourceStatusList, new FrameLayout.LayoutParams(-1, -2));
        fileCard.AddView(resourceStatusScroll, new LinearLayout.LayoutParams(-1, 0, 1));
        var resourceParams = new LinearLayout.LayoutParams(-1, Dp(IsNativeTablet ? 150 : 128));
        resourceParams.BottomMargin = Dp(12);
        content.AddView(fileCard, resourceParams);
        var spacer = new View(this);
        content.AddView(spacer, new LinearLayout.LayoutParams(-1, 0, 1));
        content.AddView(CreateActionGrid(copy), new LinearLayout.LayoutParams(-1, Dp((IsNativeTablet ? 56 : 48) * 2 + 12)));
        rootScroll.LayoutChange += (_, _) =>
        {
            var minimumFileHeight = Dp(IsNativeTablet ? 150 : 82);
            var actionHeight = Dp((IsNativeTablet ? 56 : 48) * 2 + 12);
            var fixedHeight = Dp(36 + 24) + importStatusLabel.MeasuredHeight + actionHeight;
            var cardHeight = Math.Max(rootScroll.Height, fixedHeight + minimumFileHeight);
            content.SetMinimumHeight(cardHeight);
            var desired = Math.Max(minimumFileHeight, cardHeight - fixedHeight);
            if (!IsNativeTablet) desired = Math.Min(Dp(128), desired);
            if (resourceParams.Height != desired)
            {
                resourceParams.Height = desired;
                fileCard.LayoutParameters = resourceParams;
            }
        };

        var versionLabel = NoticeText(ProductVersionText(), 13, Muted);
        versionLabel.Gravity = GravityFlags.End | GravityFlags.CenterVertical;
        var versionParams = new FrameLayout.LayoutParams(-1, Dp(24), GravityFlags.Bottom);
        versionParams.SetMargins(Dp(18), 0, Dp(18), Dp(18));
        overlay.AddView(versionLabel, versionParams);

        startupOverlay = overlay;
        rootLayout?.AddView(overlay, new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent));
        RefreshStartupNotice();
    }

	View CreateActionGrid(StartupCopy copy)
	{
		var grid = new LinearLayout(this) { Orientation = Orientation.Vertical };
		var firstRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
		var secondRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };

		var importButton = NativeButton(copy.ImportFiles, ChooseImportSource);
		var licenseButton = NativeButton(copy.ViewLicense, ShowLicenseAndNotice);
		var acknowledgementsButton = NativeButton(copy.Acknowledgements, ShowAcknowledgements);
		enterGameButton = NativeButton(returnToGame ? (StartupCopy.IsChinese ? "返回游戏" : "Return to game") : copy.EnterGame, EnterGame);

		AddActionRow(firstRow, importButton, licenseButton);
		AddActionRow(secondRow, acknowledgementsButton, enterGameButton);
		grid.AddView(firstRow, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));
		var secondRowParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1);
		secondRowParams.SetMargins(0, Dp(12), 0, 0);
		grid.AddView(secondRow, secondRowParams);
		return grid;
	}

	void AddActionRow(LinearLayout row, Button left, Button right)
	{
		row.AddView(left, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1));
		var rightParams = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MatchParent, 1);
		rightParams.SetMargins(Dp(12), 0, 0, 0);
		row.AddView(right, rightParams);
	}

    void ShowLicenseAndNotice() => ShowLicensePage();
    internal void ShowAcknowledgements() => ShowThanksPage();

    internal string ProductVersionText()
    {
        var version = "0.0.0";
        long build = 0;
        try
        {
            var info = PackageManager?.GetPackageInfo(PackageName!, PackageInfoFlags.Activities);
            version = string.IsNullOrWhiteSpace(info?.VersionName) ? version : info.VersionName;
            if (info != null)
                build = Build.VERSION.SdkInt >= BuildVersionCodes.P ? info.LongVersionCode : info.VersionCode;
        }
        catch (PackageManager.NameNotFoundException)
        {
            // Keep deterministic fallback values for malformed test packages.
        }

        return $"NUKE HOUR {version} (Build {build})";
    }

    void RefreshStartupNotice(string? message = null)
    {
        var status = retailCatalog.Inspect(RetailContentDir);
        var missing = string.Join(", ", status.MissingBaseFiles.Concat(status.MissingAudioFiles));
        if (importStatusLabel != null)
            importStatusLabel.Text = ReadyStatusLine(status, message ?? (status.CanEnterGame
				? StartupCopy.Current.Ready
				: status.CanLaunchBaseGame
					? StartupCopy.Current.MissingMaps
					: StartupCopy.Current.Missing + missing));

		if (resourceStatusList != null)
		{
			resourceStatusList.RemoveAllViews();
			foreach (var file in status.Files)
			{
				var imported = file.Imported;
				var line = NoticeText(
					$"●  {file.FileName} — {FilePurpose(file.FileName)} · " +
					(imported ? StartupCopy.Current.Imported : StartupCopy.Current.NotImported),
					15,
					imported
						? global::Android.Graphics.Color.Rgb(91, 201, 126)
						: global::Android.Graphics.Color.Rgb(154, 166, 178));
				line.SetMinHeight(Dp(IsNativeTablet ? 28 : 24));
                line.SetPadding(0, 0, 0, Dp(5));
				resourceStatusList.AddView(line, new LinearLayout.LayoutParams(
					ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
			}
		}

        if (enterGameButton != null)
            enterGameButton.Enabled = returnToGame || status.CanEnterGame;
    }

	static string ReadyStatusLine(RetailContentStatus status, string message)
	{
		var chinese = StartupCopy.IsChinese;
		var profile = status.Profile switch
		{
			RetailContentProfile.RedAlert2 => chinese ? "红警 2 兼容资源" : "Red Alert 2 compatible",
			RetailContentProfile.CombinedCollection => chinese ? "红警 2 + 尤里的复仇兼容资源" : "RA2 + Yuri's Revenge compatible",
			RetailContentProfile.SteamComplete => chinese ? "Steam 完整合集兼容资源" : "Steam complete collection compatible",
			_ => chinese ? "资源不完整" : "Incomplete content",
		};
		return $"NUKE HOUR  ·  {message}\n{(chinese ? "识别到的内容" : "Detected content")}: {profile}";
	}

	static string FilePurpose(string fileName)
	{
		var chinese = StartupCopy.IsChinese;
		return fileName.ToLowerInvariant() switch
		{
			"ra2.mix" => chinese ? "基础游戏核心资源" : "Red Alert 2 core game assets",
			"language.mix" => chinese ? "基础游戏文字、界面与音频" : "Red Alert 2 text, interface and audio",
			"ra2md.mix" => chinese ? "尤里的复仇游戏资源" : "Yuri's Revenge game assets",
			"langmd.mix" => chinese ? "尤里的复仇文字与音频" : "Yuri's Revenge text and audio",
			"multi.mix" => chinese ? "红警 2 遭遇战地图包" : "Red Alert 2 skirmish maps",
			"multimd.mix" => chinese ? "尤里的复仇遭遇战地图包" : "Yuri's Revenge skirmish maps",
			"maps01.mix" => chinese ? "红警 2 盟军战役任务包" : "Red Alert 2 Allied campaign missions",
			"maps02.mix" => chinese ? "红警 2 苏军战役任务包" : "Red Alert 2 Soviet campaign missions",
			"mapsmd03.mix" => chinese ? "尤里的复仇战役任务包" : "Yuri's Revenge campaign missions",
			"movies01.mix" => chinese ? "盟军战役剧情影片（可选）" : "Allied campaign cinematics (optional)",
			"movies02.mix" => chinese ? "苏军战役剧情影片（可选）" : "Soviet campaign cinematics (optional)",
			"movmd03.mix" => chinese ? "尤里的复仇剧情影片（可选）" : "Yuri's Revenge cinematics (optional)",
			"theme.mix" => chinese ? "红警 2 背景音乐" : "Red Alert 2 music",
			"thememd.mix" => chinese ? "尤里的复仇背景音乐" : "Yuri's Revenge music",
			"wdt.mix" => chinese ? "Steam 版补充资料（可选）" : "Steam supplemental data (optional)",
			_ => fileName,
		};
	}

    void MigrateLegacyImportRoot()
    {
        var legacy = Path.Combine(SupportPath, "ra2");
        if (!Directory.Exists(legacy) || Directory.Exists(RetailContentDir))
            return;

        Directory.CreateDirectory(ContentRoot);
        Directory.Move(legacy, RetailContentDir);
    }

    void EnterGame()
    {
        if (returnToGame)
        {
            CloseContentManagement();
            return;
        }
        var status = retailCatalog.Inspect(RetailContentDir);
		if (!status.CanEnterGame)
        {
            RefreshStartupNotice();
            return;
        }

        if (startupOverlay != null)
            rootLayout?.RemoveView(startupOverlay);
        startupOverlay = null;
        StartHost();
    }

    void OpenBrandWebsite() => OpenOfficialPage("https://nukehour.com");

    internal void OpenOfficialPage(string url)
    {
        try
        {
            externalActivityInProgress = true;
            StartActivity(new Intent(Intent.ActionView, AndroidUri.Parse(url)));
        }
        catch (ActivityNotFoundException)
        {
            externalActivityInProgress = false;
            Toast.MakeText(this, StartupCopy.Current.WebsiteFailed, ToastLength.Long)?.Show();
        }
    }

    void ShowImportFailure(string? detail = null)
    {
        new AlertDialog.Builder(this)
            .SetTitle(StartupCopy.Current.ImportFailedTitle)
            .SetMessage(string.IsNullOrWhiteSpace(detail)
				? StartupCopy.Current.ImportFailed
				: StartupCopy.Current.ImportFailed + "\n\n" + detail)
            .SetPositiveButton("OK", (_, _) => RefreshStartupNotice())
            .Show();
    }

    void ChooseImportSource()
    {
        var chinese = StartupCopy.IsChinese;
        new AlertDialog.Builder(this)
            .SetTitle(StartupCopy.Current.ImportFiles)
            .SetItems(chinese ? new[] { "选择游戏文件夹", "选择资源文件（可多选）", "从电脑导入（同一 Wi-Fi）" } :
                new[] { "Choose game folder", "Choose resource files (multiple)", "Import from computer (same Wi-Fi)" },
                (_, args) => { if (args.Which == 2) ShowLanImport(); else LaunchImportPicker(args.Which == 0); })
            .SetNegativeButton(StartupCopy.Current.Close, (_, _) => { })
            .Show();
    }

    void LaunchImportPicker(bool folder)
    {
        try
        {
            externalActivityInProgress = true;
            var intent = new Intent(folder ? Intent.ActionOpenDocumentTree : Intent.ActionOpenDocument);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantPersistableUriPermission);
            if (!folder)
            {
                intent.SetType("*/*");
                intent.AddCategory(Intent.CategoryOpenable);
                intent.PutExtra(Intent.ExtraAllowMultiple, true);
            }
            StartActivityForResult(intent, folder ? ImportTreeRequest : ImportFilesRequest);
        }
        catch (ActivityNotFoundException)
        {
            externalActivityInProgress = false;
            ShowImportFailure();
        }
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == ExportFailureRequest)
        {
            var text = pendingFailureExport;
            pendingFailureExport = null;
            if (resultCode == Result.Ok && data?.Data is { } destination && text != null)
            {
                try
                {
                    using var output = ContentResolver!.OpenOutputStream(destination, "wt") ?? throw new IOException("Report export unavailable.");
                    using var writer = new StreamWriter(output);
                    writer.Write(text);
                }
                catch (Exception) { Toast.MakeText(this, StartupCopy.IsChinese ? "导出失败，请复制诊断日志。" : "Export failed. Copy the report instead.", ToastLength.Long)?.Show(); }
            }
            return;
        }
        if (requestCode != ImportTreeRequest && requestCode != ImportFilesRequest)
            return;

        externalActivityInProgress = false;

        if (resultCode != Result.Ok || data == null || (data.Data == null && data.ClipData == null))
        {
            RefreshStartupNotice();
            return;
        }

        if (requestCode == ImportTreeRequest && data.Data is { } tree)
            BeginImport(staging => EnumerateTreeRecursive(tree, DocumentsContract.GetTreeDocumentId(tree), staging));
        else
        {
            var selection = new System.Collections.Generic.List<AndroidUri>();
            if (data.ClipData is { } clips)
                for (var i = 0; i < clips.ItemCount; i++)
                    if (clips.GetItemAt(i)?.Uri is { } uri) selection.Add(uri);
            if (selection.Count == 0 && data.Data is { } file) selection.Add(file);
            BeginImport(staging =>
            {
                CopyImportSources(selection.Select(ReadImportSource), staging);
            });
        }
    }

    void BeginImport(Action<string> stageFiles, Action<string?>? completed = null)
    {
        importDialog?.Dismiss();
        ShowImportProgress();

        var contentRoot = ContentRoot;
        var staging = Path.Combine(CacheDir!.AbsolutePath, "ra2-import-" + Guid.NewGuid().ToString("N"));
        var importPhase = "prepare";
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                ALog.Info(Tag, "Retail import: preparing private cache");
                Directory.CreateDirectory(staging);
                importPhase = "read selected files";
                stageFiles(staging);

                var files = Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories).ToArray();
                ALog.Info(Tag, "Retail import: staged " + files.Length + " files");
                importPhase = "validate and publish";
                Directory.CreateDirectory(contentRoot);
                // Android mounts its writable data separately from the read-only root filesystem.
                var availableBytes = new global::Android.OS.StatFs(contentRoot).AvailableBytes;
                ReportImportProgress(StartupCopy.IsChinese ? "2/3  校验并保存资源" : "2/3  Verifying and saving resources", 0, 0);
                var result = new RetailContentImporter().ImportAsync(
                    new RetailImportRequest(files, contentRoot, availableBytes), default, (done, total) =>
                        ReportImportProgress(StartupCopy.IsChinese ? "2/3  校验并保存资源" : "2/3  Verifying and saving resources", done, total))
                    .GetAwaiter().GetResult();
                ALog.Info(Tag, "Retail import result: " + result.Error + "; " + result.Message);

                Directory.Delete(staging, true);

                RunOnUiThread(() =>
                {
                    importDialog?.Dismiss();
                    completed?.Invoke(result.Published ? null : result.Message);
                    if (!result.Published)
                    {
                        ShowImportFailure(result.Message);
                        return;
                    }

                    if (result.Status?.CanEnterGame != true || engineFailed)
                    {
                        RefreshStartupNotice();
                        return;
                    }

                    // First import starts normally. An existing host must unmount old
                    // archives and rebuild its map cache on its own engine thread.
                    CloseContentManagement();
                    if (hostStarted)
                        openRaHost?.ReloadImportedContent();
                    else
                        StartHost();
                });
            }
            catch (Exception e)
            {
                try
                {
                    Directory.Delete(staging, true);
                }
                catch
                {
                    // best effort
                }

                ALog.Error(Tag, "Retail import failed at " + importPhase + ": " + e);
                RunOnUiThread(() =>
                {
                    importDialog?.Dismiss();
                    completed?.Invoke(e.Message);
                    ShowImportFailure(importPhase + ": " + e.GetType().Name + " — " + e.Message);
                });
            }
        });
    }

    void EnumerateTreeRecursive(AndroidUri treeUri, string documentId, string destDirectory)
    {
        var sources = new System.Collections.Generic.List<ImportSource>();
        CollectImportSources(treeUri, documentId, sources);
        CopyImportSources(sources, destDirectory);
    }

    void CollectImportSources(AndroidUri treeUri, string documentId, System.Collections.Generic.List<ImportSource> sources)
    {
        var childrenUri = DocumentsContract.BuildChildDocumentsUriUsingTree(treeUri, documentId);
        using var cursor = ContentResolver?.Query(childrenUri,
            new[] { DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnMimeType,
                DocumentsContract.Document.ColumnDisplayName, DocumentsContract.Document.ColumnSize }, null, null, null);
        if (cursor == null) throw new IOException("Unable to read selected directory.");
        while (cursor.MoveToNext())
        {
            var childId = cursor.GetString(0);
            var name = cursor.GetString(2);
            if (cursor.GetString(1) == DocumentsContract.Document.MimeTypeDir)
                CollectImportSources(treeUri, childId, sources);
            else if (!string.IsNullOrEmpty(name) && PublicContentSafetyPolicy.IsSupportedDataFileName(name))
                sources.Add(new ImportSource(DocumentsContract.BuildDocumentUriUsingTree(treeUri, childId),
                    Path.GetFileName(name).ToLowerInvariant(), cursor.IsNull(3) ? -1 : cursor.GetLong(3)));
        }
    }

    bool IsSdlSurfaceReady() => ReadBridgeBool("isSurfaceReady");
    int SdlSurfaceWidth() => ReadBridgeInt("surfaceWidth");
    int SdlSurfaceHeight() => ReadBridgeInt("surfaceHeight");

    static bool ReadBridgeBool(string method)
    {
        var cls = JNIEnv.FindClass("org/libsdl/app/AndroidHostBridge");
        var id = JNIEnv.GetStaticMethodID(cls, method, "()Z");
        return JNIEnv.CallStaticBooleanMethod(cls, id);
    }

    static int ReadBridgeInt(string method)
    {
        var cls = JNIEnv.FindClass("org/libsdl/app/AndroidHostBridge");
        var id = JNIEnv.GetStaticMethodID(cls, method, "()I");
        return JNIEnv.CallStaticIntMethod(cls, id);
    }

    protected override void OnResume()
    {
        base.OnResume();
        CallBridge("resumeManagedHost");
        HangDiagnostics.Resume();
        NearbyGameNetworking.Resume();
        if (hostStarted)
            AcquireLanDiscoveryLock();
        rootLayout?.Post(RefreshMobileMetrics);
        externalActivityInProgress = false;
        ALog.Info(Tag, "OnResume");
        updateActivityResumed = true;
        _ = CheckForOfficialUpdateAsync();
    }

    // Intercept physical Back before the focused SDL surface consumes it.
    // Gesture/navigation-bar Back enters OnBackPressed directly; both paths
    // therefore share native-overlay precedence and exactly one Escape pair.
    public override bool DispatchKeyEvent(KeyEvent? keyEvent)
    {
        if (keyEvent?.KeyCode == global::Android.Views.Keycode.Back)
        {
            if (keyEvent.Action == KeyEventActions.Up && !keyEvent.IsCanceled)
                OnBackPressed();
            return true;
        }
        return base.DispatchKeyEvent(keyEvent);
    }

    public override void OnBackPressed()
    {
        if (nativeDetailPage != null)
        {
            CloseNativePage();
            return;
        }

        if (returnToGame)
        {
            CloseContentManagement();
            return;
        }
        if (hostStarted && !engineFailed)
        {
            // Native SDL keyboard events are queued and consumed on the engine
            // input thread, including current dropdowns and confirmation dialogs.
            CallBridge("sendEscapeKey");
            return;
        }

        base.OnBackPressed();
    }

    protected override void OnPause()
    {
        base.OnPause();
        updateActivityResumed = false;
        ReleaseLanDiscoveryLock();
        HangDiagnostics.Pause();
        NearbyGameNetworking.Suspend();
        // SDL native pause and surface callbacks release/recreate the EGL drawable.
        // Do not invoke SDLActivity.handleNativeState: that starts SDLMain, while
        // this host owns the managed engine thread.
        CallBridge("pauseManagedHost");
        ALog.Info(Tag, "OnPause: SDL paused; managed session preserved");
    }

    static void CallBridge(string method)
    {
        var cls = JNIEnv.FindClass("org/libsdl/app/AndroidHostBridge");
        var id = JNIEnv.GetStaticMethodID(cls, method, "()V");
        JNIEnv.CallStaticVoidMethod(cls, id);
    }

    public override void OnLowMemory()
    {
        FailureDiagnostics?.RecordMemoryWarning();
        RuntimePerformance?.RequestMemoryTrim();
        base.OnLowMemory();
    }

    public override void OnTrimMemory(TrimMemory level)
    {
        if (level != TrimMemory.UiHidden)
        {
            FailureDiagnostics?.RecordMemoryWarning();
            RuntimePerformance?.RequestMemoryTrim();
        }
        base.OnTrimMemory(level);
    }

    void ExportFailureReport(AndroidFailureReport report)
    {
        pendingFailureExport = report.Text;
        try
        {
            var intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("text/plain");
            intent.PutExtra(Intent.ExtraTitle, "nukehour-diagnostic-" + report.Id + ".txt");
            StartActivityForResult(intent, ExportFailureRequest);
        }
        catch (ActivityNotFoundException)
        {
            pendingFailureExport = null;
            Toast.MakeText(this, StartupCopy.IsChinese ? "无法导出，请复制诊断日志。" : "Export unavailable. Copy the report instead.", ToastLength.Long)?.Show();
        }
    }

    protected override void OnDestroy()
    {
        ALog.Info(Tag, "OnDestroy");
        StopLanImport();
        ReleaseLanDiscoveryLock();
        multicastLock?.Dispose();
        multicastLock = null;
        openRaHost?.Stop();
        openRaHost = null;
        base.OnDestroy();
    }
}
