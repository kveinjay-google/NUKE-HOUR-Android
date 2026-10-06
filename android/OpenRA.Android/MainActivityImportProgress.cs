using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Android.App;
using Android.Provider;
using Android.Views;
using Android.Widget;
using AndroidUri = Android.Net.Uri;
using OpenRA.Mods.RA2.Content;

namespace OpenRA.Android;

public partial class MainActivity
{
    ProgressBar? importProgressBar;
    TextView? importProgressDetails;
    readonly Stopwatch importPhaseClock = new();
    string progressPhase = string.Empty;
    long lastProgressUpdate;
    sealed record ImportSource(AndroidUri Uri, string Name, long Size);

    void ShowImportProgress()
    {
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(Dp(24), Dp(16), Dp(24), Dp(16));
        importProgressBar = new ProgressBar(this, null, global::Android.Resource.Attribute.ProgressBarStyleHorizontal)
            { Max = 1000, Indeterminate = true };
        importProgressDetails = NoticeText(StartupCopy.IsChinese ? "正在扫描资源文件…" : "Scanning resource files…", 16, Gold);
        panel.AddView(importProgressBar, new LinearLayout.LayoutParams(-1, Dp(24)));
        panel.AddView(importProgressDetails, new LinearLayout.LayoutParams(-1, -2));
        progressPhase = string.Empty;
        lastProgressUpdate = 0;
        importDialog = new AlertDialog.Builder(this).SetTitle(StartupCopy.Current.ImportingTitle)
            .SetView(panel).SetCancelable(false).Show();
    }

    void ReportImportProgress(string phase, long done, long total, string file = "")
    {
        var changed = progressPhase != phase;
        if (changed)
        {
            progressPhase = phase;
            importPhaseClock.Restart();
            lastProgressUpdate = -250;
        }
        var elapsed = importPhaseClock.ElapsedMilliseconds;
        if (!changed && done != total && elapsed - lastProgressUpdate < 250)
            return;
        lastProgressUpdate = elapsed;
        var chinese = StartupCopy.IsChinese;
        var percent = total > 0 ? Math.Clamp(done / (double)total, 0, 1) : 0;
        var remaining = ImportProgressEstimate.RemainingSeconds(done, total, elapsed / 1000d);
        var eta = remaining.HasValue
            ? (chinese ? $"本阶段预计剩余 {Math.Ceiling(remaining.Value):0} 秒" : $"Estimated phase remaining: {Math.Ceiling(remaining.Value):0} s")
            : (chinese ? "本阶段剩余时间：正在估算…" : "Estimating phase time remaining…");
        var sizes = total > 0 ? $"{percent:P0}  ·  {done / 1048576d:F1} / {total / 1048576d:F1} MiB" : "";
        var text = phase + "\n" + sizes + "\n" + eta + (file.Length > 0 ? "\n" + file : "");
        RunOnUiThread(() =>
        {
            if (importProgressBar == null || importProgressDetails == null) return;
            importProgressBar.Indeterminate = total <= 0;
            importProgressBar.Progress = (int)(percent * 1000);
            importProgressDetails.Text = text;
        });
    }

    ImportSource ReadImportSource(AndroidUri uri)
    {
        using var cursor = ContentResolver?.Query(uri, new[] { OpenableColumns.DisplayName, OpenableColumns.Size }, null, null, null);
        if (cursor == null || !cursor.MoveToFirst()) throw new IOException("Unable to read selected file metadata.");
        var name = Path.GetFileName(cursor.GetString(0));
        if (string.IsNullOrEmpty(name) || !PublicContentSafetyPolicy.IsSupportedDataFileName(name))
            throw new IOException("Unsupported resource file: " + name);
        return new ImportSource(uri, name.ToLowerInvariant(), cursor.IsNull(1) ? -1 : cursor.GetLong(1));
    }

    void CopyImportSources(IEnumerable<ImportSource> sources, string staging)
    {
        var files = new List<ImportSource>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        var known = true;
        foreach (var source in sources)
        {
            if (!names.Add(source.Name)) throw new IOException("Duplicate resource filename: " + source.Name);
            files.Add(source);
            known &= source.Size >= 0;
            total = checked(total + Math.Max(0, source.Size));
        }
        if (!known) total = 0;
        long copied = 0;
        var buffer = new byte[65536];
        var phase = StartupCopy.IsChinese ? "1/3  读取手机文件" : "1/3  Reading selected files";
        ReportImportProgress(phase, 0, total);
        foreach (var source in files)
        {
            using var input = ContentResolver?.OpenInputStream(source.Uri) ?? throw new IOException("Unable to read " + source.Name);
            using var output = File.Create(Path.Combine(staging, source.Name));
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
                copied += read;
                ReportImportProgress(phase, copied, total, source.Name);
            }
        }
        ReportImportProgress(phase, copied, total);
    }
}
