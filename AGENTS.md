# Contributor / automation guidance

- This is an Android-only public source snapshot. The current engine source is vendored in `engine/`; do not overwrite it with a fresh upstream download.
- Use .NET 8. Run legacy net6.0 tests with DOTNET_ROLL_FORWARD=Major.
- Limit builds to four concurrent jobs and use the bounded build scripts. Do not commit generated libraries, APKs, caches, private signing keys, tokens or imported original game files.
- All selectable UI styles and faction variants must remain in the public runtime inventory. Check the final APK against the resource manifest.
- Keep player colors restricted to the existing nine presets.
- Performance changes must preserve deterministic simulation timing and multiplayer compatibility.
- Device acceptance must be reported separately from host unit tests and packaging checks.
