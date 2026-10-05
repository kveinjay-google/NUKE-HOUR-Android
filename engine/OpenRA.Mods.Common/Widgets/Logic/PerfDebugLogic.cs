#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	public sealed class PerformanceUnitCountCache
	{
		readonly Func<int> count;
		long lastSample;
		int current;
		bool sampled;
		public PerformanceUnitCountCache(Func<int> count) { this.count = count; }
		public int Sample(long nowMilliseconds)
		{
			if (!sampled || nowMilliseconds < lastSample || nowMilliseconds - lastSample >= 500)
			{
				current = count();
				lastSample = nowMilliseconds;
				sampled = true;
			}
			return current;
		}
	}

	public class PerfDebugLogic : ChromeLogic
	{
		readonly Stopwatch fpsTimer;
		readonly List<(int Frame, TimeSpan Time)> frameTimings = new(32);
		readonly RuntimeResourceSampler resourceSampler = new();

		public static string FormatPerformanceText(double fps, double? cpuPercent,
			string residentMemory, string managedMemory, int localTick, double tickMilliseconds,
			int renderFrame, double renderMilliseconds, int outputWidth, int outputHeight, int currentUnits)
		{
			var cpu = cpuPercent.HasValue
				? cpuPercent.Value.ToString("0.0", CultureInfo.InvariantCulture) + "%"
				: "暂无";
			var resident = residentMemory == "N/A" ? "暂无" : residentMemory;
			var managed = managedMemory == "N/A" ? "暂无" : managedMemory;
			return $"帧率：{fps.ToString("0", CultureInfo.InvariantCulture)} FPS   处理器：{cpu}\n" +
				$"内存：{resident} 常驻 / {managed} 托管\n" +
				$"逻辑：第 {localTick} 帧 / {tickMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)} ms\n" +
				$"渲染：第 {renderFrame} 帧 / {renderMilliseconds.ToString("0.0", CultureInfo.InvariantCulture)} ms\n" +
				$"输出分辨率：{outputWidth} × {outputHeight}\n" +
				$"当前单位总数：{currentUnits}";
		}

		[ObjectCreator.UseCtor]
		public PerfDebugLogic(Widget widget, WorldRenderer worldRenderer)
		{
			bool SettingsPanelIsClosed() => Ui.CurrentWindow()?.Id != "SETTINGS_PANEL";

			var perfGraph = widget.Get("GRAPH_BG");
			perfGraph.IsVisible = () => Game.Settings.Debug.PerfGraph && SettingsPanelIsClosed();

			var perfText = widget.Get<LabelWidget>("PERF_TEXT");
			perfText.IsVisible = () => Game.Settings.Debug.PerfText && SettingsPanelIsClosed();

			var unitCount = new PerformanceUnitCountCache(() => worldRenderer?.World == null
				? 0 : PopulationLimits.CountCurrentUnits(worldRenderer.World));
			// Six lines, without changing the existing phone/tablet layout anchors.
			perfText.Bounds.Height = Math.Max(perfText.Bounds.Height, 110);
			fpsTimer = Stopwatch.StartNew();
			frameTimings.Add((Game.RenderFrame, TimeSpan.Zero));
			perfText.GetText = () =>
			{
				// Calculate FPS as a rolling average over the last ~1 second of frames.
				frameTimings.Add((Game.RenderFrame, fpsTimer.Elapsed));
				var cutoffTime = frameTimings[^1].Time - TimeSpan.FromSeconds(1);
				var firstIndexPastCutoff = frameTimings.FindIndex(ft => ft.Time >= cutoffTime);
				if (frameTimings.Count - firstIndexPastCutoff >= 2) // Keep at least 2 items for comparing.
					frameTimings.RemoveRange(0, firstIndexPastCutoff);
				var (oldestFrame, oldestTime) = frameTimings[0];
				var (newestFrame, newestTime) = frameTimings[^1];
				var fps = (newestFrame - oldestFrame) / (newestTime - oldestTime).TotalSeconds;

				var outputSize = Game.Renderer.OutputSize;
				var resources = resourceSampler.Sample();
				return FormatPerformanceText(fps, resources.CpuPercent,
					RuntimeResourceSampler.FormatMegabytes(resources.ResidentMemoryBytes),
					RuntimeResourceSampler.FormatMegabytes(resources.ManagedMemoryBytes),
					Game.LocalTick, PerfHistory.Items["tick_time"].Average(Game.Settings.Debug.Samples),
					Game.RenderFrame, PerfHistory.Items["render"].Average(Game.Settings.Debug.Samples),
					outputSize.Width, outputSize.Height, unitCount.Sample(fpsTimer.ElapsedMilliseconds));
			};

			Game.AfterGameStart += OnGameStart;
		}

		void OnGameStart()
		{
			// Reset timings so our average doesn't include loading time.
			frameTimings.Clear();
			frameTimings.Add((Game.RenderFrame, fpsTimer.Elapsed));
		}

		bool disposed;
		protected override void Dispose(bool disposing)
		{
			if (disposing && !disposed)
			{
				disposed = true;
				Game.AfterGameStart -= OnGameStart;
			}

			base.Dispose(disposing);
		}
	}
}
