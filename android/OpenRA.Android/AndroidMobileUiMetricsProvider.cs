using System;
using Android.App;
using Android.Content.Res;
using Android.OS;
using Android.Views;
using OpenRA.MobileUi;
using ALog = Android.Util.Log;

namespace OpenRA.Android;

/// <summary>
/// Captures Android screen metrics once per layout change (never per frame) and
/// feeds <see cref="MobileUiService"/>. Density / font scale / refresh rate are
/// read from resources and display; insets (system bars, cutout) are read from
/// the window root once available, with graceful fallback to full surface.
/// </summary>
public static class AndroidMobileUiMetricsProvider
{
    public static void UpdateFrom(Activity activity, int surfaceWidth, int surfaceHeight, float uiScale)
    {
        try
        {
            var density = activity.Resources?.DisplayMetrics?.Density ?? 1f;
            var fontScale = activity.Resources?.Configuration?.FontScale ?? 1f;
            var refresh = 60f;
            var display = activity.WindowManager?.DefaultDisplay;
            if (display != null)
            {
                try
                {
                    var displayRefresh = display.RefreshRate;
                    if (displayRefresh > 0)
                        refresh = displayRefresh;
                }
                catch
                {
                    // fall back to 60
                }
            }

            var insetsLeft = 0;
            var insetsTop = 0;
            var insetsRight = 0;
            var insetsBottom = 0;

            var decor = activity.Window?.DecorView;
            if (decor != null)
            {
                try
                {
                    var rootInsets = decor.RootWindowInsets;
                    if (rootInsets != null)
                    {
                        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                        {
                            var bars = rootInsets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
                            insetsLeft = bars.Left;
                            insetsTop = bars.Top;
                            insetsRight = bars.Right;
                            insetsBottom = bars.Bottom;
                        }
                        else
                        {
                            insetsLeft = rootInsets.SystemWindowInsetLeft;
                            insetsTop = rootInsets.SystemWindowInsetTop;
                            insetsRight = rootInsets.SystemWindowInsetRight;
                            insetsBottom = rootInsets.SystemWindowInsetBottom;
                            if (Build.VERSION.SdkInt >= BuildVersionCodes.P && rootInsets.DisplayCutout is { } cutout)
                            {
                                insetsLeft = Math.Max(insetsLeft, cutout.SafeInsetLeft);
                                insetsTop = Math.Max(insetsTop, cutout.SafeInsetTop);
                                insetsRight = Math.Max(insetsRight, cutout.SafeInsetRight);
                                insetsBottom = Math.Max(insetsBottom, cutout.SafeInsetBottom);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    ALog.Warn("MobileUi.Metrics", "insets probe failed: " + e.GetType().Name);
                }
            }

            // Insets are relative to the decor window. The SDL view may already
            // exclude a navigation/status bar; apply only the overlap with its
            // actual screen rectangle to avoid subtracting that space twice.
            if (decor != null && activity is MainActivity host && host.GameSurface is { } surface &&
                surface.Width > 0 && surface.Height > 0 && decor.Width > 0 && decor.Height > 0)
            {
                var decorPosition = new int[2];
                var surfacePosition = new int[2];
                decor.GetLocationOnScreen(decorPosition);
                surface.GetLocationOnScreen(surfacePosition);
                var leftMargin = surfacePosition[0] - decorPosition[0];
                var topMargin = surfacePosition[1] - decorPosition[1];
                var rightMargin = decor.Width - leftMargin - surface.Width;
                var bottomMargin = decor.Height - topMargin - surface.Height;
                insetsLeft = Math.Max(0, insetsLeft - leftMargin);
                insetsTop = Math.Max(0, insetsTop - topMargin);
                insetsRight = Math.Max(0, insetsRight - rightMargin);
                insetsBottom = Math.Max(0, insetsBottom - bottomMargin);
            }

            var usableWidthPx = Math.Max(1, surfaceWidth - insetsLeft - insetsRight);
            var usableHeightPx = Math.Max(1, surfaceHeight - insetsTop - insetsBottom);
            var usableWidthDp = usableWidthPx / (density > 0 ? density : 1f);
            var usableHeightDp = usableHeightPx / (density > 0 ? density : 1f);
            var logicalWidth = uiScale > 0 ? (int)Math.Round(surfaceWidth / uiScale) : surfaceWidth;
            var logicalHeight = uiScale > 0 ? (int)Math.Round(surfaceHeight / uiScale) : surfaceHeight;

            var densityForDp = density > 0 ? density : 1f;
            // Native dp dimensions, not scaled renderer dimensions, define phone/tablet.
            IosScreenMetrics.Publish(
                new OpenRA.Primitives.Size((int)Math.Round(surfaceWidth / densityForDp),
                    (int)Math.Round(surfaceHeight / densityForDp)),
                new IosSafeAreaInsets(insetsLeft / densityForDp, insetsTop / densityForDp,
                    insetsRight / densityForDp, insetsBottom / densityForDp));
            MobileUiService.Instance.IsEnabledMobile = true;
            MobileUiService.Instance.Update(
                surfaceWidth, surfaceHeight, surfaceWidth, surfaceHeight,
                logicalWidth, logicalHeight, density, fontScale, refresh,
                usableWidthDp, usableHeightDp, MobileUiSizePreference.Standard100,
                insetsLeft / densityForDp, insetsTop / densityForDp,
                insetsRight / densityForDp, insetsBottom / densityForDp);

            ALog.Info("MobileUi.Metrics",
                $"profile={MobileUiService.Instance.Profile} usable={usableWidthDp:F0}x{usableHeightDp:F0}dp " +
                $"density={density:F2} fontScale={fontScale:F2} insets={insetsLeft},{insetsTop},{insetsRight},{insetsBottom} " +
                $"uiScale={uiScale:F2} logical={logicalWidth}x{logicalHeight}");
        }
        catch (Exception e)
        {
            ALog.Error("MobileUi.Metrics", "metrics update failed: " + e.GetType().Name);
        }
    }
}
