using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenRA.Android;

sealed record AndroidFailureReportContext(
	string? Build = null, string? Device = null, string? System = null,
	string? Phase = null, string? Map = null, string? Counts = null,
	string? MemorySamples = null, string? SampledAt = null);

// Deliberately independent of UIKit and engine state. Capture only supplied facts;
// never read logs, settings, chat, environment variables, or Exception.Data.
sealed record AndroidFailureReport(string Id, DateTimeOffset TimestampUtc,
	AndroidStartupFailureInfo Failure, string Reason, string Text, string Phase)
{
	public const int MaximumCharacters = 32768; // UTF-8 remains below 128 KiB.
	public const int RetainedReports = 5;
	const string Unavailable = "unavailable";
	static readonly object PersistenceLock = new();

	internal static string Sanitize(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return Unavailable;

		// Bound work before regular expressions, including pathological exception messages.
		var text = value.Length > 8192 ? value[..8192] + " [truncated]" : value;
		try
		{
			// Redact the remainder of a sensitive line, including quoted values with spaces.
			text = Regex.Replace(text,
				@"(?im)\b(password|passwd|pwd|token|access[_-]?token|refresh[_-]?token|authorization|auth|cookie|set-cookie|secret|api[_-]?key|chat)\b[\s\""']*[:=][^\r\n]*",
				"$1=[redacted]", RegexOptions.None, TimeSpan.FromMilliseconds(100));
			text = Regex.Replace(text, @"(?i)\bBearer\s+\S+", "Bearer [redacted]", RegexOptions.None, TimeSpan.FromMilliseconds(100));
			// Remove complete URIs: userinfo, query strings and fragments can all contain secrets.
			text = Regex.Replace(text, @"(?i)\b[a-z][a-z0-9+.-]*://[^\s<>\""']+", "[uri redacted]", RegexOptions.None, TimeSpan.FromMilliseconds(100));
			// Absolute paths disclose account names, sandbox IDs and user-supplied filenames.
			text = Regex.Replace(text, @"(?<![\w])(?:[a-zA-Z]:[\\/]|/)[^\r\n\t\""<>]*", "[path redacted]", RegexOptions.None, TimeSpan.FromMilliseconds(100));
			return text.Replace("\0", string.Empty);
		}
		catch (RegexMatchTimeoutException)
		{
			return "[redacted: sanitization limit]";
		}
	}

	public static AndroidFailureReport Capture(Exception error, AndroidFailureReportContext context)
	{
		var id = Guid.NewGuid().ToString("N");
		var timestamp = DateTimeOffset.UtcNow;
		var failure = AndroidStartupFailureClassifier.Classify(error);
		var exceptions = AndroidStartupFailureClassifier.Enumerate(error).ToArray();
		var cause = exceptions.LastOrDefault(e => AndroidStartupFailureClassifier.ClassifyNode(e).Category == failure.Category)
			?? exceptions.Last();
		var reason = cause.GetType().Name + ": " + Sanitize(AndroidStartupFailureClassifier.SafeMessage(cause));
		if (reason.Length > 1024)
			reason = reason[..1000] + " [truncated]";

		var body = new StringBuilder();
		void Add(string name, string? value)
		{
			if (body.Length >= MaximumCharacters)
				return;
			var line = name + ": " + Sanitize(value) + "\n";
			body.Append(line.AsSpan(0, Math.Min(line.Length, MaximumCharacters - body.Length)));
		}

		Add("Report version", "1");
		Add("Fault ID", id);
		Add("Captured UTC", timestamp.ToString("O"));
		Add("Code", failure.Code);
		Add("Category", failure.Category.ToString());
		Add("Reason", reason);
		Add("Build", context.Build);
		Add("Device", context.Device);
		Add("System", context.System);
		Add("Phase", context.Phase);
		Add("Map", context.Map);
		Add("Counts", context.Counts);
		Add("Sampled at", context.SampledAt);
		Add("Memory samples", context.MemorySamples);
		Add("Diagnostic limits", "Exception graph capped at 128 nodes; fields capped at 8192 characters; report capped at 32768 characters. Missing observations are unavailable; no diagnosis is inferred from missing samples.");
		for (var i = 0; i < exceptions.Length; i++)
		{
			var current = exceptions[i];
			Add($"Exception {i + 1} type", current.GetType().FullName);
			var children = current is AggregateException group ? group.InnerExceptions.Take(128)
				: current.InnerException != null ? new[] { current.InnerException } : Array.Empty<Exception>();
			Add($"Exception {i + 1} inner nodes", string.Join(", ", children.Select(child =>
			{
				var index = Array.IndexOf(exceptions, child);
				return index < 0 ? "[outside graph limit]" : (index + 1).ToString();
			})));
			// AggregateException.Message concatenates every descendant message and can
			// allocate unbounded memory. Children below preserve their individual causes.
			Add($"Exception {i + 1} message", AndroidStartupFailureClassifier.SafeMessage(current));
			string? stack;
			try { stack = current.StackTrace; }
			catch (Exception) { stack = "[stack unavailable: exception getter failed]"; }
			Add($"Exception {i + 1} stack", stack);
		}

		if (body.Length == MaximumCharacters)
		{
			const string marker = "\n[report truncated]\n";
			body.Length -= marker.Length;
			body.Append(marker);
		}

		return new AndroidFailureReport(id, timestamp, failure, reason, body.ToString(), Sanitize(context.Phase));
	}

	// A failed write or retention cleanup must never replace the original game exception.
	public bool TryPersist(string diagnosticsDirectory, out string? path)
	{
		path = null;
		lock (PersistenceLock)
		{
			try
			{
				Directory.CreateDirectory(diagnosticsDirectory);
				var destination = Path.Combine(diagnosticsDirectory, $"android-failure-{TimestampUtc:yyyyMMddTHHmmssfffffff}-{Id}.txt");
				File.WriteAllText(destination, Text, new UTF8Encoding(false));
				path = destination;
				foreach (var old in new DirectoryInfo(diagnosticsDirectory).GetFiles("android-failure-*.txt")
					.OrderByDescending(file => file.Name, StringComparer.Ordinal).Skip(RetainedReports))
				{
					try { old.Delete(); }
					catch (IOException) { }
					catch (UnauthorizedAccessException) { }
				}

				return true;
			}
			catch (Exception) { return path != null; }
		}
	}
}
