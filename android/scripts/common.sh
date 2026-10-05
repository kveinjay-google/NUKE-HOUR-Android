#!/bin/sh
# Shared helpers for the Android POC scripts.
# Must be run from inside the Android working repository; never from the
# read-only source repository. No host absolute paths are hard-coded.
set -u

# Locate repository root by marker files (independent of any user path).
find_repository_root() {
    d=$(pwd)
    while [ "$d" != "/" ]; do
        if [ -f "$d/OpenRA.Mods.RA2.sln" ] && [ -d "$d/android/OpenRA.Android" ]; then
            printf '%s\n' "$d"
            return 0
        fi
        d=$(dirname "$d")
    done
    echo "ERROR: not inside the RA2Android working repository" >&2
    exit 2
}

REPOSITORY_ROOT=$(find_repository_root)

# .NET: prefer the user-local SDK used for the iOS/Android workloads.
if [ -x "$HOME/.dotnet/dotnet" ]; then
    DOTNET="$HOME/.dotnet/dotnet"
    DOTNET_ROOT="$HOME/.dotnet"
    PATH="$HOME/.dotnet:$PATH"
    export DOTNET_ROOT PATH
else
    DOTNET=dotnet
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export MSBUILDDISABLENODEREUSE=1

# Android SDK / JDK (brew layout defaults, overridable via env).
ANDROID_HOME="${ANDROID_HOME:-/opt/homebrew/share/android-commandlinetools}"
export ANDROID_HOME ANDROID_SDK_ROOT="${ANDROID_SDK_ROOT:-$ANDROID_HOME}"
JAVA_HOME="${JAVA_HOME:-/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home}"
export JAVA_HOME
PATH="$JAVA_HOME/bin:$PATH"
export PATH

POC_PACKAGE="com.openra.android.personal"
POC_ACTIVITY="crc648953fb64c9166e17.MainActivity"
POC_APK="$REPOSITORY_ROOT/android/OpenRA.Android/bin/Debug/net8.0-android/android-arm64/$POC_PACKAGE-Signed.apk"
POC_ARTIFACTS="$REPOSITORY_ROOT/artifacts/android-poc"
POC_APP_FILES="/data/user/0/$POC_PACKAGE/files"

require_adb_device() {
    command -v adb >/dev/null 2>&1 || { echo "ERROR: adb not found" >&2; exit 2; }
    if ! adb devices 2>/dev/null | grep -q 'device$'; then
        echo "ERROR: no Android device/emulator attached" >&2
        exit 2
    fi
}

require_apk() {
    if [ ! -f "$POC_APK" ]; then
        echo "ERROR: APK not found: $POC_APK (run 'android-poc.sh build' first)" >&2
        exit 2
    fi
}
