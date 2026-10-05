using System.Globalization;
using System.Runtime.InteropServices;
using OpenRA;
using OpenRA.Graphics;
using OpenRA.Mods.Cnc.Traits;
using OpenRA.Mods.Common.Traits;
using OpenRA.Support;


namespace OpenRA.Android;

// Sample only on the game thread, at a low fixed frequency. Keep values,
// never World/Actor references, so diagnostics cannot retain a disposed match.
sealed class AndroidFailureDiagnostics : IDisposable
{
	const int Capacity = 30;
	readonly AndroidFailureReport emergencyReport = new(
		Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow,
		new AndroidStartupFailureInfo("NH-ANDROID-START-0001", AndroidStartupFailureCategory.General),
		"Diagnostic capture failed / 无法生成完整诊断。原始错误原因未能保存。",
		"Emergency fallback report. Original exception, occurrence timestamp, phase and memory are unavailable. " +
		"The report ID and timestamp were reserved at application launch. This is not evidence of an out-of-memory failure.", "unavailable");
	// Outer throttle owns the 2-second cadence; forced warning samples must be fresh.
	readonly RuntimeResourceSampler sampler = new(1);
	readonly string?[] recent = new string?[Capacity];
    readonly string build;
    readonly string device = global::Android.OS.Build.Manufacturer + " " + global::Android.OS.Build.Model;
    readonly string system = "Android " + global::Android.OS.Build.VERSION.Release;
    readonly object sync = new();
    int generation;
    bool disposed;

    public AndroidFailureDiagnostics(string build)
    {
        this.build = build;
        Observe(force: true);
        Game.OnShellmapLoaded += StartObserving;
        Game.AfterGameStart += StartObserving;
    }

    void StartObserving()
    {
        var epoch = ++generation;
        void SampleNext()
        {
            if (disposed || epoch != generation) return;
            Observe();
            Game.RunAfterDelay(2000, SampleNext);
        }
        SampleNext();
    }

    public void Dispose()
    {
        disposed = true;
        generation++;
        Game.OnShellmapLoaded -= StartObserving;
        Game.AfterGameStart -= StartObserving;
    }
	int next;
	int count;
	long nextSample;
	long? peakResident;
	long? peakManaged;
	int memoryWarnings;
	string phase = "startup";
	string? map;
	string? counts;
	string? sampledAt;

	public void Observe(bool force = false)
	{
        lock (sync) ObserveCore(force);
    }

    void ObserveCore(bool force)
    {
		var now = Environment.TickCount64;
		try
		{
			var world = Game.OrderManager?.World;
			phase = world == null ? "startup" : world.Type != WorldType.Regular ? "menu" :
				world.WorldTick <= 0 || world.IsLoadingGameSave ? "loading" : "gameplay";
			if (!force && now < nextSample)
				return;
			nextSample = now + 2000;
			map = world?.Map == null ? null : world.Map.Title + " [" + world.Map.Uid + "]";
			counts = null;
			if (world != null && world.Type == WorldType.Regular)
			{
				var players = 0;
				var bots = 0;
				foreach (var player in world.Players)
					if (player.Playable && !player.NonCombatant)
					{
						players++;
						if (player.IsBot)
							bots++;
					}

				var units = 0;
				var buildings = 0;
				// Trait registry includes cargo occupants even while absent from World.Actors.
				foreach (var pair in world.ActorsWithTrait<Mobile>())
					if (!pair.Actor.IsDead && !pair.Actor.Disposed)
						units++;
				foreach (var pair in world.ActorsWithTrait<Aircraft>())
					if (!pair.Actor.IsDead && !pair.Actor.Disposed)
						units++;
				foreach (var pair in world.ActorsWithTrait<Building>())
					if (!pair.Actor.IsDead && !pair.Actor.Disposed)
						buildings++;
				counts = $"players={players}; AI={bots}; units={units}; buildings={buildings}; " +
					$"tick={world.WorldTick}; simulationSeconds={world.WorldTick * (long)world.Timestep / 1000}";
			}

			var chrome = ChromeProvider.GetCacheSnapshot();
			var models = world?.WorldActor?.TraitOrDefault<ModelRenderer>();
			var model = models == null ? default : models.GetCacheSnapshot();
			// These are color-pixel estimates, not total GPU RSS: depth/driver overhead is excluded.
			counts = $"{counts} chromeSheets={chrome.Sheets}; chromeSprites={chrome.Sprites}; " +
				$"chromePanelSprites={chrome.PanelSprites}; chromeCollectionAliases={chrome.CollectionAliases}; " +
				$"chrome.colorPixelEstimateBytes={chrome.ColorPixelEstimateBytes}; " +
				$"modelMappedBuffers={model.MappedBuffers}; modelUnmappedBuffers={model.UnmappedBuffers}; " +
				$"modelQueuedRenders={model.QueuedRenders}; model.colorPixelEstimateBytes={model.ColorPixelEstimateBytes}";

			var sample = sampler.Sample();
			peakResident = Peak(peakResident, sample.ResidentMemoryBytes);
			peakManaged = Peak(peakManaged, sample.ManagedMemoryBytes);
			sampledAt = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
			recent[next] = $"{sampledAt} phase={phase} resident={Bytes(sample.ResidentMemoryBytes)} " +
				$"managed={Bytes(sample.ManagedMemoryBytes)} allocated={Bytes(sample.TotalAllocatedBytes)} " +
				$"thermal={sample.ThermalState ?? "unavailable"} {counts}";
			next = (next + 1) % Capacity;
			count = Math.Min(count + 1, Capacity);
		}
		catch
		{
			// Partial startup/destruction must not turn optional diagnostics into another failure.
		}
	}

	public void RecordMemoryWarning()
	{
        System.Threading.Interlocked.Increment(ref memoryWarnings);
        // World traversal must remain on the engine thread, including OS warnings.
        Game.RunAfterTick(() => { if (!disposed) Observe(force: true); });
	}

	public AndroidFailureReport CaptureFailure(Exception error)
	{
		try { lock (sync) return CaptureReport(error); }
		catch { return emergencyReport; }
	}

	AndroidFailureReport CaptureReport(Exception error)
	{
		// Do not query the damaged world on failure; use the bounded recent snapshot.
		var samples = new System.Text.StringBuilder(8192);
		samples.AppendLine($"sessionPeakResident={Bytes(peakResident)}; sessionPeakManaged={Bytes(peakManaged)}; memoryWarnings={memoryWarnings}");
		for (var i = 0; i < count; i++)
			samples.AppendLine(recent[(next - 1 - i + Capacity) % Capacity]);
		return AndroidFailureReport.Capture(error, new AndroidFailureReportContext(
			Build: build, Device: device, System: system, Phase: phase, Map: map,
			Counts: counts, MemorySamples: samples.ToString(), SampledAt: sampledAt));
	}

	static long? Peak(long? previous, long? value) => value.HasValue ? Math.Max(previous ?? 0, value.Value) : previous;
	static string Bytes(long? value) => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) + " bytes" : "unavailable";
}
