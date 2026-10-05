#!/bin/sh
set -eu
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
OUT="${NUKEHOUR_ANDROID_EXPORT_DIR:-$ROOT/artifacts/android-public-clean}"
SHARED_MANIFEST="${NUKEHOUR_SHARED_PUBLIC_MANIFEST:-$ROOT/packaging/public-content-manifest.json}"
ANDROID_SDK="${ANDROID_HOME:-/opt/homebrew/share/android-commandlinetools}"
if [ -x "$HOME/.dotnet/dotnet" ]; then
    DOTNET="$HOME/.dotnet/dotnet"
    export DOTNET_ROOT="$HOME/.dotnet"
    PATH="$DOTNET_ROOT:$PATH"
else
    DOTNET="$(command -v dotnet)"
fi
JAVA_HOME="${JAVA_HOME:-/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home}"
export PATH JAVA_HOME
mkdir -p "$OUT"

if [ ! -f "$ROOT/android/OpenRA.Android/native/arm64-v8a/libSDL2.so" ] ||
   [ ! -f "$ROOT/android/OpenRA.Android/native/arm64-v8a/libfreetype6.so" ]; then
    python3 "$SCRIPT_DIR/run_bounded.py" --seconds 1800 --log "$OUT/native-dependencies.log" -- sh "$ROOT/android/scripts/build-native-libs.sh"
fi
python3 "$SCRIPT_DIR/run_bounded.py" --seconds 120 --log "$OUT/native-build.log" -- sh "$ROOT/android/scripts/build-video-pixels.sh"

python3 "$SCRIPT_DIR/clean_runtime_assets.py" prepare --out "$OUT" --shared-manifest "$SHARED_MANIFEST"
python3 "$SCRIPT_DIR/run_bounded.py" --seconds 1800 --log "$OUT/build.log" -- "$DOTNET" build "$ROOT/android/OpenRA.Android/OpenRA.Android.csproj" -c Release -t:SignAndroidPackage -m:4 -nodeReuse:false -p:UseSharedCompilation=false \
    -p:PublicClean=true \
    "-p:NukeHourAndroidAssetsProps=$OUT/runtime-assets.props" \
    "-p:BaseOutputPath=$OUT/build/" \
    "-p:NukeHourEngineOutputPath=$OUT/engine-bin/" \
    '-p:DefaultItemExcludes=**/obj/**' \
    "-p:AndroidSdkDirectory=$ANDROID_SDK/" \
    -p:RunAOTCompilation=false

APK="$OUT/build/Release/net8.0-android/android-arm64/com.openra.android.personal-Signed.apk"
python3 "$SCRIPT_DIR/clean_runtime_assets.py" audit --apk "$APK" --manifest "$OUT/runtime-assets.json" --report "$OUT/apk-audit.json"
NUKEHOUR_ANDROID_AUDIT_APK="$APK" python3 -m unittest discover -s "$ROOT/packaging/tests" -p test_android_clean_ui_apk.py
"$ANDROID_SDK/build-tools/35.0.0/apksigner" verify "$APK"
"$ANDROID_SDK/build-tools/35.0.0/zipalign" -c -P 16 4 "$APK"
shasum -a 256 "$APK" > "$OUT/apk.sha256"
printf 'Audited ARM64 Release APK: %s\n' "$APK"
