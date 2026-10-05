namespace OpenRA
{
	using System;
	using System.IO;
	using System.Text;
	using OpenRA.Network;

	public static class LoadMemoryPolicy
	{
		// Memory capability contract (refined for Android, see
		// docs/ANDROID_DEVICE_REPORT.md).
		// - Desktop keeps forced stop-the-world collections during loading.
		// - iOS cannot force a collection while SDL's native audio callback is
		//   executing managed code (deadlock risk), and its runtime rejects
		//   explicit LOH compaction.
		// - Android's Mono accepts GC.Collect (verified on the emulator, and
		//   re-verified on hardware during the device phase) but rejects explicit
		//   LOH compaction with PlatformNotSupportedException.
		public static bool ShouldForceCollection(bool isIOS) => !isIOS;

		public static bool SupportsForcedCollection => SupportsForcedCollectionCore(OperatingSystem.IsIOS());
		public static bool SupportsLohCompaction => SupportsLohCompactionCore(OperatingSystem.IsIOS(), Platform.IsAndroid);
		public static bool ShouldWaitForPendingFinalizers => ShouldWaitForPendingFinalizersCore(OperatingSystem.IsIOS());

		// Pure capability cores (testable without depending on the host OS).
		public static bool SupportsForcedCollectionCore(bool isIOS) => !isIOS;
		public static bool SupportsLohCompactionCore(bool isIOS, bool isAndroid) => !isIOS && !isAndroid;
		public static bool ShouldWaitForPendingFinalizersCore(bool isIOS) => !isIOS;

		// Android load-time memory snapshot (map load start/end). Low frequency:
		// once per map load, never per frame.
		public static void LogMemorySnapshot(string stage)
		{
			if (!Platform.IsAndroid)
				return;

			try
			{
				var managed = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
				var g0 = GC.CollectionCount(0);
				var g1 = GC.CollectionCount(1);
				var g2 = GC.CollectionCount(2);
				long rss = -1;
				long peak = -1;
				foreach (var line in System.IO.File.ReadLines("/proc/self/status"))
				{
					if (line.StartsWith("VmRSS:", StringComparison.Ordinal))
						rss = ParseKb(line);
					else if (line.StartsWith("VmHWM:", StringComparison.Ordinal))
						peak = ParseKb(line);
				}

				Console.WriteLine(
					$"[android-mem:{stage}] managedHeapMB={managed:F1} nativeRSSMB={rss / 1024.0:F0} peakRSSMB={peak / 1024.0:F0} gc(0/1/2)={g0}/{g1}/{g2}");
			}
			catch (Exception e)
			{
				Console.WriteLine($"[android-mem:{stage}] snapshot failed: {e.GetType().Name}");
			}
		}

		static long ParseKb(string line)
		{
			var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			return parts.Length >= 2 && long.TryParse(parts[1], out var v) ? v : -1;
		}
	}

	public static class IosDevelopmentLaunch
	{
		public static string BuildSkirmishArgument(string target)
		{
			if (string.IsNullOrWhiteSpace(target))
				return null;

			var trimmed = target.Trim();
			return trimmed.IndexOf(' ') < 0 ?
				$"Launch.Map={trimmed}" :
				$"Game.LaunchInto=skirmish {trimmed}";
		}

		public static bool ShouldLogHeartbeat(string target, int frame, int lastLoggedFrame)
		{
			if (string.IsNullOrWhiteSpace(target) || frame <= 0 || frame == lastLoggedFrame)
				return false;

			return lastLoggedFrame < 0 || frame % 300 == 0;
		}
	}

	public static class IosLoadStageJournal
	{
		public static bool Record(string stage, string detail = null)
		{
			if (!OperatingSystem.IsIOS() || string.IsNullOrEmpty(Platform.SupportDir))
				return false;

			try
			{
				var directory = Path.Combine(Platform.SupportDir, "Logs");
				Directory.CreateDirectory(directory);
				var path = Path.Combine(directory, "ios-load-stage.log");
				using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read,
					4096, FileOptions.WriteThrough);
				using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
				writer.WriteLine($"{DateTime.UtcNow:O} {stage}{(detail == null ? string.Empty : " " + detail)}");
				stream.Flush(true);
				return true;
			}
			catch
			{
				// Diagnostics must never prevent the game from loading.
				return false;
			}
		}
	}

	public sealed class IosSmokeReadinessTracker
	{
		public const long RequiredDurationMilliseconds = 10_000;
		public const int RequiredWorldTicks = 100;
		public const int RequiredRenderFrames = 100;

		bool initialized;
		long initialElapsedMilliseconds;
		int initialWorldTick;
		int initialRenderFrame;
		int lastWorldTick;
		int lastRenderFrame;

		public bool Observe(long elapsedMilliseconds, int worldTick, int renderFrame, bool connected)
		{
			if (!connected)
			{
				initialized = false;
				return false;
			}

			if (!initialized)
			{
				initialized = true;
				initialElapsedMilliseconds = elapsedMilliseconds;
				initialWorldTick = worldTick;
				initialRenderFrame = renderFrame;
				lastWorldTick = worldTick;
				lastRenderFrame = renderFrame;
				return false;
			}

			var worldAdvanced = worldTick > lastWorldTick;
			var renderAdvanced = renderFrame > lastRenderFrame;
			lastWorldTick = worldTick;
			lastRenderFrame = renderFrame;

			var reachedProgressTarget = worldTick - initialWorldTick >= RequiredWorldTicks &&
				renderFrame - initialRenderFrame >= RequiredRenderFrames;
			var reachedDurationTarget = elapsedMilliseconds - initialElapsedMilliseconds >= RequiredDurationMilliseconds &&
				worldAdvanced && renderAdvanced;
			return reachedProgressTarget && reachedDurationTarget;
		}
	}

	public sealed class IosSmokeReadyRecorder
	{
		int state;

		public bool TryRecord(Func<bool> write)
		{
			if (write == null)
				throw new ArgumentNullException(nameof(write));

			if (System.Threading.Interlocked.CompareExchange(ref state, 1, 0) != 0)
				return false;

			var completed = false;
			try
			{
				completed = write();
				return completed;
			}
			finally
			{
				System.Threading.Volatile.Write(ref state, completed ? 2 : 0);
			}
		}
	}

	public static class IosSmokeTestJournal
	{
		static readonly string RunId = Environment.GetEnvironmentVariable("OPENRA_IOS_SMOKE_RUN_ID");
		static readonly string LaunchNonce =
			Environment.GetEnvironmentVariable("OPENRA_IOS_SMOKE_LAUNCH_NONCE");
		static readonly IosSmokeReadyRecorder readyRecorder = new();
		static IosSmokeReadinessTracker readiness = new();
		static World trackedWorld;

		public static void RecordStart()
		{
			if (string.IsNullOrWhiteSpace(RunId) || string.IsNullOrWhiteSpace(LaunchNonce))
				return;

			IosLoadStageJournal.Record("smoke-start", $"run={RunId} launch={LaunchNonce}");
		}

		public static void RecordReady(
			World world, int frame, int worldTick, int renderFrame, long elapsedMilliseconds)
		{
			if (string.IsNullOrWhiteSpace(RunId) || world == null || world.Type != WorldType.Regular || frame <= 0)
				return;

			if (!ReferenceEquals(trackedWorld, world))
			{
				trackedWorld = world;
				readiness = new IosSmokeReadinessTracker();
			}

			var connected = world.OrderManager.Connection is not NetworkConnection connection ||
				connection.ConnectionState == ConnectionState.Connected;
			if (!readiness.Observe(elapsedMilliseconds, worldTick, renderFrame, connected))
				return;

			readyRecorder.TryRecord(() => IosLoadStageJournal.Record(
				"smoke-ready", $"run={RunId} launch={LaunchNonce} criterion=duration-and-progress " +
					$"map={world.Map.Uid} frame={frame} " +
					$"worldTick={worldTick} renderFrame={renderFrame} elapsedMs={elapsedMilliseconds} " +
					"connection=Connected"));
		}

		public static string FormatFailure(string runId, Exception error, string launchNonce = null)
		{
			var message = (error?.Message ?? "unknown error").Replace('\r', ' ').Replace('\n', ' ');
			var launch = string.IsNullOrWhiteSpace(launchNonce) ? string.Empty : $" launch={launchNonce}";
			return $"run={runId}{launch} type={error?.GetType().Name ?? "UnknownException"} message={message}";
		}

		public static void RecordFailure(Exception error)
		{
			if (string.IsNullOrWhiteSpace(RunId))
				return;

			IosLoadStageJournal.Record("smoke-failure", FormatFailure(RunId, error, LaunchNonce));
		}
	}
}
