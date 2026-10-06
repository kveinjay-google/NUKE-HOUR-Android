# Update verification — 2026-10-06

Published source now includes eight AI difficulty levels and custom profiles, buildable technology expansion, import progress/ETA and automatic loading, weekly official update checks, and browser-to-phone LAN import with a copyable session access link. NanoHTTPD 2.3.1 source and BSD license are vendored with upstream provenance.

- Public checkout Release ARM64 build passed (41.1 seconds); all 843 runtime resources match the manifest, eight APK checks pass, signature and 16 KB alignment verified.
- Focused suite: 165 C# passed, three platform/development-mirror skips, zero failures; 11 Python policy checks passed.
- Real JVM HTTP tests pass for credentials, origin checks, chunking, resume, commit and shutdown; measured 27 JVM threads. Page-script tests cover complete, incomplete and expired links.
- Git-index export scanned with Gitleaks 8.30.1: no leaks found. Source inventory rejects game archives, binaries, signing material and files at least 50 MiB.
- Xiaomi acceptance: previous PIN-based transfer imported 15 files / 1784.7 MiB and automatically reached the main menu. The replacement copy-link build was installed successfully; physical clipboard/WeChat interaction has not yet been verified.
- Desktop APK SHA-256: `cf1e411ab13affe5d55a19751e6160213518cd2c486da140e302428689e82544`.

Historical verification follows.

# Public source verification — 2026-10-05

The Android-only public source tree was built on macOS using .NET SDK 8.0.423, Android SDK 34, Build Tools 35.0.0, NDK 28.2.13676358 and JDK 17.

## Reproducibility checks

- Built SDL 2.30.10 and FreeType 2.13.3 from official upstream archives in a fresh native output directory; no private compiled native libraries were copied into the public source tree.
- Built the signed Release ARM64 APK directly from this repository with `sh packaging/android/build-public-clean.sh`. Then exported only the Git index into a separate directory and repeated the full native/Release APK build and all package gates successfully, ensuring no ignored local source or assets were required.
- The resulting APK passed the exact resource inventory/hash audit, all 6 APK content/style checks, signature verification and 16 KB ZIP alignment.
- All **842 packaged runtime resources** match the current development Release export byte-for-byte. This includes all selected interface styles, all three factions and both original background videos.
- Compiled binaries, native build archives, caches, imported original game data and signing files are ignored by Git and are not part of this source publication.

## Tests

`sh packaging/android/test-public-source.sh` completed with **102 passed, 3 skipped, 0 failed** C# tests and **11 passed** Python source/resource-policy tests.

The three skips are explicit environment boundaries, not failed Android feature tests:

1. Two contracts inspect an iOS-only smoke-test script, which is not part of this Android repository.
2. One checks a duplicate `engine/mods/ra2` development mirror. The public tree keeps only authoritative `mods/ra2`; its corresponding Battle Fortress rule test passes.

The focused suite covers pixel buffer ownership, texture eviction/reload/disposal, thermal policy and scheduling, video pixels, diagnostics, captured technology/production and the fixed nine-color policy. The full historical engine test source is retained, but tests for other platforms or private game-data fixtures may need those separate environments.

## Publication checks

A Gitleaks 8.30.1 scan found no unexcluded credential findings. One upstream Fluent keyboard label (`.kp_comma = Keypad ,`) is an identified false positive; `.gitleaks.toml` limits that exclusion to the exact file and line pattern. Existing license notices and asset provenance are retained.

The source inventory audit verifies all required runtime resources are committed, hashes match, no compiled/game archives or signing files are included, and no individual file reaches 50 MiB. The new public repository has a fresh history, rather than exposing private development commits.

## Limits

This verifies source-build and packaging reproducibility on macOS. Linux APK build support is present but has not received the same end-to-end local validation. The GitHub workflow exercises the focused source tests separately.

No Android device was connected for this publication. Sustained memory/frame-time measurements, live texture switching, background/resume, thermal recovery and long-match stability remain device-acceptance work. No measured FPS or device stability improvement is claimed here.

The final staged publication inventory contains 2,973 files. An independent review identified and corrected an overly broad `Support/` ignore pattern; the root-only `/Support/` rule now preserves the engine Support namespace and nine support-power UI icons. The staged-source audit and independent staged-checkout build both pass after the correction.
