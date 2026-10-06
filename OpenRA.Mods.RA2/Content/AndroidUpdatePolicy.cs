using System;
using System.Text.Json;

namespace OpenRA.Mods.RA2.Content
{
    public sealed record AndroidReleaseInfo(long Build, string Version, string Notes);

    public static class AndroidUpdatePolicy
    {
        public const string FeedUrl = "https://nukehour.com/updates/android.json";
        public const string WebsiteUrl = "https://nukehour.com/downloads.html#android";
        public static bool IsDue(long now, long lastSuccess, long lastAttempt)
        {
            bool Elapsed(long value, long duration) => value <= 0 || value > now || now - value >= duration;
            return Elapsed(lastSuccess, 7 * 86400) && Elapsed(lastAttempt, 86400);
        }

        public static AndroidReleaseInfo Parse(string json, long installedBuild)
        {
            if (string.IsNullOrEmpty(json) || json.Length > 16384) return null;
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.GetProperty("schema").GetInt32() != 1 ||
                    root.GetProperty("packageId").GetString() != "com.openra.android.personal" ||
                    root.GetProperty("channel").GetString() != "stable") return null;
                var build = root.GetProperty("build").GetInt64();
                var version = root.GetProperty("version").GetString();
                if (build <= installedBuild || build <= 0 || version == null || version.Length > 32 || !Version.TryParse(version, out _)) return null;
                var notes = root.TryGetProperty("notes", out var value) ? value.GetString() ?? "" : "";
                return new AndroidReleaseInfo(build, version, notes.Length > 1200 ? notes[..1200] : notes);
            }
            catch (Exception e) when (e is JsonException || e is InvalidOperationException || e is System.Collections.Generic.KeyNotFoundException || e is FormatException || e is OverflowException)
            {
                return null;
            }
        }
    }
}
