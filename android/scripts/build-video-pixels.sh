#!/bin/sh
set -eu
cd "$(dirname "$0")/../.."
. android/scripts/common.sh
video_ndk="${ANDROID_NDK_HOME:-$ANDROID_HOME/ndk/28.2.13676358}"
case "$(uname -s)" in
    Darwin) video_host=darwin-x86_64 ;;
    Linux) video_host=linux-x86_64 ;;
    *) echo "Unsupported NDK host; use macOS or Linux." >&2; exit 2 ;;
esac
video_cc="$video_ndk/toolchains/llvm/prebuilt/$video_host/bin/aarch64-linux-android24-clang"
mkdir -p android/OpenRA.Android/native/arm64-v8a
"$video_cc" -O3 -fPIC -shared -Wl,-z,max-page-size=16384 -Wl,-soname,libnukehour_video_pixels.so \
    android/native/menu-video-pixels.c android/native/bitmap-pixels.c -ljnigraphics -o android/OpenRA.Android/native/arm64-v8a/libnukehour_video_pixels.so
