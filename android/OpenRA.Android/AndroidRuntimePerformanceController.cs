using System;
using System.Threading;
using OpenRA.Graphics;
using OpenRA.Support;
using ALog = Android.Util.Log;

namespace OpenRA.Android;

// Owned by the engine host. All texture access happens on the engine/render thread.
internal sealed class AndroidRuntimePerformanceController : IDisposable
{
    const long ChromeBudgetBytes = 128L * 1024 * 1024;
    readonly AndroidThermalFramePolicy thermal = new();
    readonly Func<int> readThermalStatus;
    int pendingTrim;
    int generation;
    float lastDisplayRate = -1;
    volatile bool disposed;

    public AndroidRuntimePerformanceController(Func<int> readThermalStatus)
    {
        this.readThermalStatus = readThermalStatus;
        Game.OnShellmapLoaded += Start;
        Game.AfterGameStart += Start;
    }

    void Start()
    {
        var epoch = ++generation;
        void Poll()
        {
            if (disposed || epoch != generation) return;
            Update();
            Game.RunAfterDelay(5000, Poll);
        }
        Poll();
    }

    // Coalesce bursts of UI-thread callbacks. If suspended, leave the request for resume.
    public void RequestMemoryTrim()
    {
        if (disposed || Interlocked.Exchange(ref pendingTrim, 1) != 0) return;
        Game.RunAfterTick(TrimIfReady);
    }

    void Update()
    {
        if (disposed || Game.Renderer == null || Game.Renderer.WindowIsSuspended) return;
        int status;
        try { status = readThermalStatus(); }
        catch { status = -1; }
        var cap = thermal.Update(status, Game.RunTime);
        if (Game.RuntimeRenderFrameRateLimit != cap)
        {
            Game.RuntimeRenderFrameRateLimit = cap;
            ALog.Info("OpenRA.Performance", $"thermal={status} temporaryRenderCap={cap}");
        }

        var graphics = Game.Settings.Graphics;
        var configured = graphics.CapFramerate ? Math.Clamp(graphics.MaxFramerate, 1, 1000) : 0;
        var displayRate = cap == 0 ? configured : configured == 0 ? cap : Math.Min(cap, configured);
        if (displayRate != lastDisplayRate)
        {
            global::OpenRA.Platforms.Default.AndroidPlatform.RequestRenderFrameRate?.Invoke(displayRate);
            lastDisplayRate = displayRate;
        }
        TrimIfReady();
    }

    void TrimIfReady()
    {
        if (disposed || Game.Renderer == null || Game.Renderer.WindowIsSuspended) return;
        var pressure = Interlocked.Exchange(ref pendingTrim, 0) != 0;
        if (!pressure && ChromeProvider.GetCacheSnapshot().ColorPixelEstimateBytes <= ChromeBudgetBytes) return;
        Game.Renderer.Flush();
        var released = ChromeProvider.TrimUnusedTextures(Game.RunTime - (pressure ? 2000 : 30000));
        if (released > 0)
            ALog.Info("OpenRA.Performance", $"chromeReleasedBytes={released} memoryPressure={pressure}");
    }

    public void Dispose()
    {
        disposed = true;
        generation++;
        Game.OnShellmapLoaded -= Start;
        Game.AfterGameStart -= Start;
        Game.RuntimeRenderFrameRateLimit = 0;
    }
}
