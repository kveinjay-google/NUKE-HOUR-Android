# Architecture

`android/OpenRA.Android` owns the Android activity, startup/import flow, MediaCodec video source, resource extraction and failure reporting. It hosts the OpenRA engine loop on the managed engine thread.

`android/OpenRA.Platforms.Android` supplies SDL/OpenGL ES, audio and haptics while sharing compatible sources with the engine's default platform. `third_party/SDL2-CS` provides the SDL binding, with its original license header.

`engine` is the full current source snapshot based on OpenRA release-20250330. It includes local gameplay, UI, mobile input, diagnostics and performance changes; rebuilding does not depend on replaying a private patch series. `OpenRA.Mods.RA2` adds RA2-specific rules/traits/widgets.

The Android bitmap decoder writes one owned premultiplied BGRA array that Sheet adopts. Immutable chrome sheets preserve Sprite identity across GPU eviction/reload. The Android performance controller schedules cleanup on the engine/render thread and uses temporary thermal rendering limits, separate from deterministic simulation and saved settings.

Only the reviewed runtime inventory in `packaging/public-content-manifest.json` is packaged by the public build. User-imported original game data is stored on device and is never part of the source distribution.
