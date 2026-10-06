using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Android.App;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace OpenRA.Android;

public sealed partial class MainActivity
{
    AlertDialog? lanImportDialog;
    string? lanIntakeDirectory;
    bool lanImportRunning;
    int lanGeneration;

    static string LanString(string method, params string[] values)
    {
        var cls = JNIEnv.FindClass("org/nukehour/LanImportServer");
        var signature = "(" + string.Concat(values.Select(_ => "Ljava/lang/String;")) + ")Ljava/lang/String;";
        var handles = values.Select(JNIEnv.NewString).ToArray();
        try
        {
            var id = JNIEnv.GetStaticMethodID(cls, method, signature);
            var value = JNIEnv.CallStaticObjectMethod(cls, id, handles.Select(h => new JValue(h)).ToArray());
            return JNIEnv.GetString(value, JniHandleOwnership.TransferLocalRef) ?? "";
        }
        finally { foreach (var handle in handles) JNIEnv.DeleteLocalRef(handle); }
    }

    static void LanComplete(string? error)
    {
        var cls = JNIEnv.FindClass("org/nukehour/LanImportServer");
        var value = JNIEnv.NewString(error ?? "");
        try { JNIEnv.CallStaticVoidMethod(cls, JNIEnv.GetStaticMethodID(cls, "complete", "(Ljava/lang/String;)V"), new JValue(value)); }
        finally { JNIEnv.DeleteLocalRef(value); }
    }

    void StopLanImport()
    {
        lanGeneration++;
        var cls = JNIEnv.FindClass("org/nukehour/LanImportServer");
        JNIEnv.CallStaticVoidMethod(cls, JNIEnv.GetStaticMethodID(cls, "stopServer", "()V"));
        if (!lanImportRunning && lanIntakeDirectory is { } path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch (IOException) { }
        }
        lanIntakeDirectory = null;
    }

    void ShowLanImport()
    {
        StopLanImport();
        var chinese = StartupCopy.IsChinese;
        var address = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                (n.Name.StartsWith("wlan", StringComparison.OrdinalIgnoreCase) || n.Name.StartsWith("wifi", StringComparison.OrdinalIgnoreCase)))
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
        if (address == null)
        {
            Toast.MakeText(this, chinese ? "请先连接与电脑相同的 Wi-Fi。" : "Connect to the same Wi-Fi as your computer first.", ToastLength.Long)?.Show();
            return;
        }
        try
        {
            lanIntakeDirectory = Path.Combine(CacheDir!.AbsolutePath, "lan-import-" + Guid.NewGuid().ToString("N"));
            using var reader = new StreamReader(Assets!.Open("lan-import.html"));
            var connection = LanString("startServer", address.ToString(), lanIntakeDirectory, reader.ReadToEnd());
            var details = new TextView(this) { TextSize = 18 };
            details.SetPadding(Dp(24), Dp(16), Dp(24), Dp(16));
            details.SetTextIsSelectable(true);
            details.Text = chinese
                ? $"电脑浏览器打开：\n{connection}\n\n点击“复制网址”，可通过微信发到电脑；在电脑浏览器打开完整链接即可。\n选择文件夹并上传，然后在网页点击“开始导入”。\n请保持此页面打开；空闲 15 分钟后关闭。"
                : $"Open in your computer browser:\n{connection}\n\nCopy the full link and send it to your computer. Open it in a browser.\nUpload files and select Import on the webpage.\nKeep this page open. Expires after 15 idle minutes.";
            var content = new LinearLayout(this) { Orientation = Orientation.Vertical };
            content.AddView(details);
            var copy = new Button(this) { Text = chinese ? "复制网址" : "Copy link" };
            copy.Click += (_, _) =>
            {
                if (GetSystemService(ClipboardService) is global::Android.Content.ClipboardManager clipboard)
                {
                    clipboard.PrimaryClip = global::Android.Content.ClipData.NewPlainText("NUKE HOUR LAN import", connection);
                    Toast.MakeText(this, chinese ? "网址已复制，可发送到电脑浏览器打开。" : "Link copied. Open it in your computer browser.", ToastLength.Short)?.Show();
                }
            };
            content.AddView(copy);
            var scroll = new ScrollView(this);
            scroll.AddView(content);
            Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
            lanImportDialog = new AlertDialog.Builder(this).SetTitle(chinese ? "从电脑导入 · 局域网" : "Import from computer · LAN")
                .SetView(scroll).SetNegativeButton(chinese ? "关闭接收" : "Stop receiving", (_, _) => { }).Show();
            lanImportDialog!.DismissEvent += (_, _) => { if (!lanImportRunning) StopLanImport(); };
            var generation = lanGeneration;
            var connectionText = details.Text;
            Action? poll = null;
            poll = () =>
            {
                if (generation != lanGeneration || IsFinishing) return;
                var intake = LanString("takeImport");
                if (intake == "expired") { lanImportDialog?.Dismiss(); return; }
                if (intake.Length > 0)
                {
                    lanImportRunning = true;
                    lanImportDialog?.Dismiss();
                    BeginImport(staging =>
                    {
                        foreach (var file in Directory.EnumerateFiles(intake)) File.Move(file, Path.Combine(staging, Path.GetFileName(file)));
                    }, error =>
                    {
                        LanComplete(error);
                        lanImportRunning = false;
                        rootLayout?.PostDelayed(() => { if (generation == lanGeneration) StopLanImport(); }, 15000);
                    });
                    return;
                }
                if (long.TryParse(LanString("summary"), out var received))
                    details.Text = connectionText + (chinese ? $"\n\n已接收 {received / 1048576d:F1} MiB" : $"\n\nReceived {received / 1048576d:F1} MiB");
                rootLayout?.PostDelayed(poll!, 500);
            };
            rootLayout?.PostDelayed(poll, 500);
        }
        catch (Exception e)
        {
            StopLanImport();
            ShowImportFailure("LAN: " + e.Message);
        }
    }
}
