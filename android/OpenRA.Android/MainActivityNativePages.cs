using System;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using OpenRA.Mods.RA2.Content;
using Color = Android.Graphics.Color;

namespace OpenRA.Android;

public sealed partial class MainActivity
{
    static readonly Color Gold = Color.Rgb(246, 202, 92);
    static readonly Color Muted = Color.Rgb(154, 166, 178);
    View? nativeDetailPage;
    Action? refreshNativePage;
    bool engineFailed;
    bool IsNativeTablet => (Resources?.Configuration?.SmallestScreenWidthDp ?? 0) >= 600;

    GradientDrawable PanelBackground(int alpha = 242, float radius = 14, float border = 2)
    {
        var drawable = new GradientDrawable();
        drawable.SetColor(Color.Argb(alpha, 20, 17, 15));
        drawable.SetCornerRadius(Dp(radius));
        drawable.SetStroke(Dp(border), Color.Rgb(151, 55, 39));
        return drawable;
    }

    FrameLayout CreateArtPage(string asset, bool shaded = true)
    {
        var page = new FrameLayout(this) { Clickable = true, Focusable = true };
        page.SetBackgroundColor(Color.Black);
        var art = new ImageView(this);
        art.SetScaleType(ImageView.ScaleType.CenterCrop);
        try
        {
            using var stream = Assets!.Open("runtime/mods/ra2/uibits/" + asset);
            art.SetImageDrawable(Drawable.CreateFromStream(stream, asset));
        }
        catch (Exception error) { global::Android.Util.Log.Warn("OpenRA.NativePages", "Artwork unavailable: " + error.GetType().Name); }
        page.AddView(art, new FrameLayout.LayoutParams(-1, -1));
        if (shaded)
        {
            var shade = new View(this);
            shade.SetBackgroundColor(Color.Argb(142, 9, 6, 5));
            page.AddView(shade, new FrameLayout.LayoutParams(-1, -1));
        }
        return page;
    }

    void ApplySafeMargins(View view, int outerDp)
    {
        if (rootLayout == null || rootLayout.Width <= 0 || rootLayout.Height <= 0) return;
        var safe = IosScreenMetrics.SnapshotFor(new OpenRA.Primitives.Size(rootLayout.Width, rootLayout.Height)).SafeBounds;
        var outer = Dp(outerDp);
        if (view.LayoutParameters is FrameLayout.LayoutParams frame)
        {
            var left = safe.Left + outer;
            var top = safe.Top + outer;
            var right = rootLayout.Width - safe.Right + outer;
            var bottom = rootLayout.Height - safe.Bottom + outer;
            if (frame.LeftMargin != left || frame.TopMargin != top || frame.RightMargin != right || frame.BottomMargin != bottom)
            {
                frame.SetMargins(left, top, right, bottom);
                view.LayoutParameters = frame;
            }
        }
    }

    Button NativeButton(string title, Action action)
    {
        var button = new Button(this) { Text = title, Gravity = GravityFlags.Center };
        button.SetAllCaps(false);
        button.SetTextColor(Color.White);
        button.SetTextSize(ComplexUnitType.Sp, 17);
        button.SetTypeface(Typeface.Create("sans-serif-medium", TypefaceStyle.Normal), TypefaceStyle.Normal);
        button.SetMaxLines(2);
        button.SetPadding(Dp(8), 0, Dp(8), 0);
        button.SetMinHeight(0);
        button.SetMinimumHeight(0);
        var states = new StateListDrawable();
        GradientDrawable Fill(Color color)
        {
            var drawable = new GradientDrawable();
            drawable.SetColor(color);
            drawable.SetCornerRadius(Dp(8));
            drawable.SetStroke(Dp(1), Color.Rgb(157, 67, 46));
            return drawable;
        }
        states.AddState(new[] { -global::Android.Resource.Attribute.StateEnabled }, Fill(Color.Argb(230, 35, 34, 32)));
        states.AddState(new[] { global::Android.Resource.Attribute.StatePressed }, Fill(Color.Argb(255, 91, 47, 35)));
        states.AddState(Array.Empty<int>(), Fill(Color.Argb(242, 61, 32, 26)));
        button.Background = states;
        button.StateListAnimator = null;
        button.Click += (_, _) => action();
        return button;
    }

    void CloseNativePage()
    {
        if (engineFailed)
            return;
        if (nativeDetailPage != null)
            rootLayout?.RemoveView(nativeDetailPage);
        nativeDetailPage = null;
        refreshNativePage = null;
    }

    void RebuildVisibleNativePages()
    {
        // Recreate native TextViews so the current scaledDensity, locale and
        // tablet/phone resource configuration are applied. The SDL surface and
        // running engine remain attached and are never restarted here.
        var rebuildDetails = refreshNativePage;
        if (nativeDetailPage != null)
            rootLayout?.RemoveView(nativeDetailPage);
        nativeDetailPage = null;
        refreshNativePage = null;
        if (startupOverlay != null)
        {
            rootLayout?.RemoveView(startupOverlay);
            startupOverlay = null;
            ShowStartupNotice();
        }
        rebuildDetails?.Invoke();
    }

    void PresentNativePage(View page)
    {
        CloseNativePage();
        nativeDetailPage = page;
        rootLayout?.AddView(page, new FrameLayout.LayoutParams(-1, -1));
    }

    LinearLayout AddDocumentPanel(FrameLayout page, string title, out ScrollView scroll)
    {
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.Background = PanelBackground();
        var padding = Dp(IsNativeTablet ? 24 : 16);
        panel.SetPadding(padding, padding, padding, padding);
        var frame = new FrameLayout.LayoutParams(-1, -1);
        var outer = Dp(IsNativeTablet ? 34 : 12);
        frame.SetMargins(outer, outer, outer, outer);
        page.AddView(panel, frame);
        page.LayoutChange += (_, _) => ApplySafeMargins(panel, IsNativeTablet ? 34 : 12);
        var heading = NoticeText(title, 28, Gold);
        panel.AddView(heading, new LinearLayout.LayoutParams(-1, -2));
        scroll = new ScrollView(this) { FillViewport = true };
        var scrollFrame = new LinearLayout.LayoutParams(-1, 0, 1);
        scrollFrame.SetMargins(0, Dp(12), 0, Dp(12));
        panel.AddView(scroll, scrollFrame);
        return panel;
    }

    void ShowLicensePage()
    {
        RefreshNativeLanguage();
        var copy = StartupCopy.Current;
        var page = CreateArtPage("cc-soviet-subpage-legal.png");
        var panel = AddDocumentPanel(page, copy.ViewLicense, out var scroll);
        scroll.AddView(NoticeText(copy.Body, 17, Color.Rgb(224, 229, 234)), new FrameLayout.LayoutParams(-1, -2));
        var actions = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        AddActionRow(actions, NativeButton(copy.OfficialWebsite, OpenBrandWebsite), NativeButton(copy.Close, CloseNativePage));
        panel.AddView(actions, new LinearLayout.LayoutParams(-1, Dp(IsNativeTablet ? 54 : 48)));
        PresentNativePage(page);
        refreshNativePage = ShowLicensePage;
    }

    void ShowContentDetails()
    {
        var page = CreateArtPage("cc-soviet-subpage-import.png");
        var panel = AddDocumentPanel(page, StartupCopy.IsChinese ? "查看资源详情" : "View resource details", out var scroll);
        var rows = new LinearLayout(this) { Orientation = Orientation.Vertical };
        foreach (var file in retailCatalog.Inspect(RetailContentDir).Files)
        {
            var row = NoticeText($"●  {file.FileName} — {FilePurpose(file.FileName)} · " +
                (file.Imported ? StartupCopy.Current.Imported : StartupCopy.Current.NotImported), 17,
                file.Imported ? Color.Rgb(91, 201, 126) : Muted);
            row.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
            row.SetMinHeight(Dp(IsNativeTablet ? 48 : 42));
            rows.AddView(row, new LinearLayout.LayoutParams(-1, -2));
        }
        scroll.AddView(rows, new FrameLayout.LayoutParams(-1, -2));
        panel.AddView(NativeButton(StartupCopy.Current.Close, CloseNativePage), new LinearLayout.LayoutParams(-1, Dp(48)));
        PresentNativePage(page);
        refreshNativePage = ShowContentDetails;
    }

    void ShowThanksPage()
    {
        RefreshNativeLanguage();
        var chinese = StartupCopy.IsChinese;
        var page = CreateArtPage(IsNativeTablet ? "cc-special-thanks-hall-ios-pad.png" : "cc-special-thanks-hall-ios-phone.png", false);
        var title = NoticeText(chinese ? "特别鸣谢" : "Special Thanks", IsNativeTablet ? 34 : 26, Color.Rgb(242, 213, 160));
        title.SetTypeface(title.Typeface, TypefaceStyle.Bold);
        title.Gravity = GravityFlags.Center;
        var privacy = NoticeText(chinese ? "仅在支持者本人许可后公开展示名称。" : "Display names are published only with the supporter's permission.", IsNativeTablet ? 15 : 12, Color.Rgb(232, 220, 199));
        privacy.Gravity = GravityFlags.Center;
        var scroll = new ScrollView(this) { VerticalScrollBarEnabled = false };
        var names = new LinearLayout(this) { Orientation = Orientation.Vertical };
        foreach (var name in new[] { "美食", "北海", "小杰", "美食4", "姜", "Lonely", "龙华力宏", "许", "Zzz77zzZ", "Yung" })
        {
            var label = NoticeText(name, IsNativeTablet ? 28 : 22, Color.Rgb(242, 213, 160));
            label.Gravity = GravityFlags.Center;
            names.AddView(label, new LinearLayout.LayoutParams(-1, Dp(IsNativeTablet ? 84 : 62)));
        }
        scroll.AddView(names);
        var support = NativeButton(chinese ? "支持一下" : "Support NUKE HOUR", () => OpenOfficialPage("https://nukehour.com/supporters#support"));
        var supporters = NativeButton(chinese ? "官网查看更多" : "More on Official Website", () => OpenOfficialPage("https://nukehour.com/supporters"));
        var close = NativeButton(chinese ? "返回首页" : "Return Home", CloseNativePage);
        foreach (var button in new[] { support, supporters, close })
        {
            button.Background = null;
            button.SetTextColor(Color.Rgb(242, 213, 160));
            button.SetTextSize(ComplexUnitType.Sp, IsNativeTablet ? 18 : 14);
        }
        foreach (var view in new View[] { title, scroll, privacy, support, supporters, close }) page.AddView(view);
        var art = (ImageView)page.GetChildAt(0)!;
        void Place(View view, double x, double y, double w, double h, bool touch = false)
        {
            if (art.Drawable == null || page.Width <= 0 || page.Height <= 0) return;
            // Use the same center-crop transform as the painting. Text and hit
            // targets stay centered on its plates at every screen aspect ratio.
            var artWidth = art.Drawable.IntrinsicWidth;
            var artHeight = art.Drawable.IntrinsicHeight;
            var scale = Math.Max((double)page.Width / artWidth, (double)page.Height / artHeight);
            var width = artWidth * scale;
            var height = artHeight * scale;
            var viewWidth = (int)Math.Round(width * w);
            var paintedHeight = height * h;
            var viewHeight = (int)Math.Round(touch ? Math.Max(Dp(48), paintedHeight) : paintedHeight);
            view.LayoutParameters = new FrameLayout.LayoutParams(viewWidth, viewHeight)
            {
                LeftMargin = (int)Math.Round((page.Width - width) / 2 + width * x),
                TopMargin = (int)Math.Round((page.Height - height) / 2 + height * (y + h / 2) - viewHeight / 2d)
            };
        }
        page.LayoutChange += (_, _) =>
        {
            if (IsNativeTablet)
            {
                Place(title, .365, .145, .27, .055);
                Place(scroll, .295, .25, .41, .29);
                Place(privacy, .25, .66, .50, .055);
                Place(support, .165, .775, .195, .065, true);
                Place(supporters, .402, .775, .196, .065, true);
                Place(close, .64, .775, .195, .065, true);
            }
            else
            {
                Place(title, .365, .085, .27, .075);
                Place(scroll, .285, .215, .43, .345);
                Place(privacy, .25, .67, .50, .055);
                Place(support, .165, .815, .205, .085, true);
                Place(supporters, .402, .815, .196, .085, true);
                Place(close, .63, .815, .205, .085, true);
            }
            // Four complete rows in the plaque; duplicated rows allow a seamless loop.
            var rowHeight = Math.Max(1, scroll.LayoutParameters!.Height / 4);
            for (var i = 0; i < names.ChildCount; i++)
                names.GetChildAt(i)!.LayoutParameters = new LinearLayout.LayoutParams(-1, rowHeight);
        };
        PresentNativePage(page);
        refreshNativePage = ShowThanksPage;
        Action? advance = null;
        advance = () =>
        {
            if (nativeDetailPage != page) return;
            var next = scroll.ScrollY + Math.Max(1, Dp(.9f));
            scroll.ScrollTo(0, next >= names.Height / 2 ? 0 : next);
            page.PostDelayed(advance!, 50);
        };
        page.PostDelayed(advance, 2000);
    }

    internal void ShowEngineFailure(Exception error, AndroidFailureReport? report = null)
    {
        ReleaseLanDiscoveryLock();
        var chinese = StartupCopy.IsChinese;
        var page = CreateArtPage("cc-soviet-subpage-import.png");
        var panel = AddDocumentPanel(page, chinese ? "需要重新启动游戏" : "Game restart required", out var scroll);
        var message = chinese ? "游戏已停止，无法安全继续当前会话。下方列出已记录的故障原因。请关闭并重新打开应用；可以复制或导出诊断日志。" :
            "The game stopped and this session cannot safely continue. The recorded cause is shown below. Close and reopen the app; copy or export the diagnostic report for troubleshooting.";
        report ??= AndroidFailureReport.Capture(error, new AndroidFailureReportContext(Build: ProductVersionText()));
        scroll.AddView(NoticeText(message + "\n\n" + report.Failure.Code + " · " + report.Id + "\n" + report.Reason, 17, Color.Rgb(224, 229, 234)));
        var diagnosticActions = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        AddActionRow(diagnosticActions,
            NativeButton(chinese ? "复制诊断日志" : "Copy diagnostic report", () =>
            {
                if (GetSystemService(ClipboardService) is global::Android.Content.ClipboardManager clipboard)
                    clipboard.PrimaryClip = ClipData.NewPlainText("NUKE HOUR diagnostic", report.Text);
            }),
            NativeButton(chinese ? "导出日志文件…" : "Export report file…", () => ExportFailureReport(report)));
        var diagnosticParams = new LinearLayout.LayoutParams(-1, Dp(48)) { BottomMargin = Dp(12) };
        panel.AddView(diagnosticActions, diagnosticParams);
        panel.AddView(NativeButton(chinese ? "关闭游戏" : "Close game", () =>
        {
            FinishAndRemoveTask();
            global::Android.OS.Process.KillProcess(global::Android.OS.Process.MyPid());
        }), new LinearLayout.LayoutParams(-1, Dp(48)));
        PresentNativePage(page);
        refreshNativePage = () => ShowEngineFailure(error, report);
        engineFailed = true;
    }
}
