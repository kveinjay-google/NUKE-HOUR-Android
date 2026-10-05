# Third-party source and assets

This distribution retains existing copyright and license notices. The repository license does not replace a file's explicit third-party license.

| Component | Origin / license |
| --- | --- |
| OpenRA engine | https://github.com/OpenRA/OpenRA , baseline release-20250330, modified here; GPL-3.0-or-later; see engine/COPYING and engine/AUTHORS |
| RA2 mod | https://github.com/OpenRA/ra2 , modified here; GPL-3.0-or-later; retain source headers and mods/ra2/AUTHORS |
| SDL 2.30.10 | https://github.com/libsdl-org/SDL ; zlib; built from the official release source archive |
| SDL Java glue | Based on SDL release-2.30.10, with local Android host/input adaptations; notice in android/OpenRA.Android/java/org/libsdl/app/NOTICE-SDL2-GLUE.txt |
| SDL2# binding | Ethan Lee / https://github.com/flibitijibibo/SDL2-CS ; zlib license retained in third_party/SDL2-CS/SDL2.cs |
| FreeType 2.13.3 | https://freetype.org ; FreeType Project License or GPL, as provided by upstream; full license texts retained in third_party/licenses; official source is downloaded when building |
| Noto Sans CJK fonts | SIL Open Font License; see mods/ra2/fonts/OFL.txt |
| Other packaged UI, font and support assets | Per-file classification, license description and SHA-256 retained in packaging/public-content-manifest.json |
| NuGet dependencies | Declared in the individual csproj files; restored from NuGet, with each package's own license |

Original Red Alert 2 and Yuri's Revenge game archives, models, audio and maps are not distributed. The two menu background videos are project-original assets. Original project sources follow the root GPL license; assets with a distinct license description retain their individual terms.

The engine is vendored as source to preserve all mobile modifications. Compiled native libraries and .NET assemblies are generated locally and are excluded from Git.

## Reference-derived UI provenance

The inventory contains 234 UI assets classified as `user-reference-restored`, with the recorded description `User-provided game reference; deterministic NUKE HOUR restoration`. This category is retained explicitly and is not a claim that every visual was created from scratch or that the original reference works have been relicensed. The root GPL license covers project source code; separately described assets retain their recorded terms and underlying rights. These reviewed UI files are distinct from the original retail game archives, which are not distributed.
