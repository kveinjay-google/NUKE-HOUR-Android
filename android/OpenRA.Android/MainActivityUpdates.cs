using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using OpenRA.Mods.RA2.Content;
using ALog = Android.Util.Log;

namespace OpenRA.Android;

public sealed partial class MainActivity
{
    bool updateCheckRunning;
    bool updateActivityResumed;
    AlertDialog? updateNotice;

    async Task CheckForOfficialUpdateAsync()
    {
        if (updateCheckRunning || hostStarted || IsFinishing) return;
        updateCheckRunning = true;
        try
        {
            var preferences = GetSharedPreferences("official-version-check", FileCreationMode.Private)!;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (AndroidUpdatePolicy.IsDue(now, preferences.GetLong("success", 0), preferences.GetLong("attempt", 0)))
            {
                preferences.Edit()!.PutLong("attempt", now)!.Apply();
                using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(8) };
                using var response = await client.GetAsync(AndroidUpdatePolicy.FeedUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > 16384) return;
                using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var buffer = new MemoryStream();
                var bytes = new byte[4096];
                int read;
                while ((read = await stream.ReadAsync(bytes, timeout.Token)) > 0)
                {
                    if (buffer.Length + read > 16384) return;
                    buffer.Write(bytes, 0, read);
                }
                var json = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
                if (AndroidUpdatePolicy.Parse(json, 0) == null) return;
                preferences.Edit()!.PutString("release", json)!.PutLong("success", now)!.Apply();
                ALog.Info(Tag, "Official update check completed");
            }
            var info = PackageManager!.GetPackageInfo(PackageName!, 0)!;
            var build = global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.P
                ? info.LongVersionCode : info.VersionCode;
            var release = AndroidUpdatePolicy.Parse(preferences.GetString("release", "")!, build);
            if (release == null || preferences.GetLong("prompted", 0) >= preferences.GetLong("success", 0)) return;
            RunOnUiThread(() =>
            {
                // Keep the cached notice for the next startup if import/gameplay has begun.
                if (!updateActivityResumed || hostStarted || engineFailed || IsFinishing || startupOverlay == null ||
                    importDialog?.IsShowing == true || updateNotice?.IsShowing == true) return;
                var chinese = StartupCopy.IsChinese;
                updateNotice = new AlertDialog.Builder(this)
                    .SetTitle(chinese ? "发现新版本" : "Update available")
                    .SetMessage($"NUKE HOUR {release.Version} (Build {release.Build})\n\n{release.Notes}")
                    .SetPositiveButton(chinese ? "前往官网更新" : "Open official website", (_, _) => OpenOfficialPage(AndroidUpdatePolicy.WebsiteUrl))
                    .SetNegativeButton(chinese ? "稍后" : "Later", (_, _) => { })
                    .Show();
                preferences.Edit()!.PutLong("prompted", preferences.GetLong("success", 0))!.Apply();
            });
        }
        catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException || e is IOException || e is OperationCanceledException)
        {
            ALog.Info(Tag, "Official update check deferred: " + e.GetType().Name);
        }
        finally { updateCheckRunning = false; }
    }
}
