#!/bin/sh
# Build the Android POC APK (net8.0-android, arm64-v8a).
set -eu
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
# shellcheck source=common.sh
. "$SCRIPT_DIR/common.sh"

cd "$REPOSITORY_ROOT"
mkdir -p "$POC_ARTIFACTS"

echo "Building OpenRA.Android (Debug, android-arm64)..."
"$DOTNET" build "$REPOSITORY_ROOT/android/OpenRA.Android/OpenRA.Android.csproj" \
    -c Debug \
    -nologo \
    -m:1 \
    -p:AndroidSdkDirectory="$ANDROID_HOME" \
    -p:JavaSdkDirectory="$JAVA_HOME" \
    > "$POC_ARTIFACTS/build.log" 2>&1

echo "Build log: $POC_ARTIFACTS/build.log"
ls -la "$POC_APK"
