#!/bin/sh
# Rebuild the arm64-v8a native libraries used by the POC from official sources
# (SDL release-2.30.10, FreeType 2.13.3). Outputs are placed directly in
# android/OpenRA.Android/native/arm64-v8a/.
#
# Requirements: Android NDK + CMake (from ANDROID_HOME or a supplied NDK dir),
# network access to fetch the official source archives.
set -eu
SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
REPOSITORY_ROOT=$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)
OUT="$REPOSITORY_ROOT/artifacts/android-poc/third_party"
NATIVE="$REPOSITORY_ROOT/android/OpenRA.Android/native/arm64-v8a"
mkdir -p "$OUT" "$NATIVE"

ANDROID_HOME="${ANDROID_HOME:-/opt/homebrew/share/android-commandlinetools}"
NDK_DIR="${NDK_DIR:-${ANDROID_NDK_HOME:-$ANDROID_HOME/ndk/28.2.13676358}}"
if [ -z "${NDK_DIR:-}" ] || [ ! -f "$NDK_DIR/build/cmake/android.toolchain.cmake" ]; then
    echo "ERROR: Android NDK not found under $ANDROID_HOME/ndk" >&2
    exit 2
fi
echo "Using NDK: $NDK_DIR"

SDL_VER=2.30.10
FT_VER=2.13.3
if [ ! -d "$OUT/SDL2-$SDL_VER" ]; then
    curl --fail --location --max-time 180 -o "$OUT/sdl.tar.gz" "https://github.com/libsdl-org/SDL/releases/download/release-$SDL_VER/SDL2-$SDL_VER.tar.gz"
    tar xzf "$OUT/sdl.tar.gz" -C "$OUT"
fi
if [ ! -d "$OUT/freetype-$FT_VER" ]; then
    curl --fail --location --max-time 180 -o "$OUT/ft.tar.gz" "https://download.savannah.gnu.org/releases/freetype/freetype-$FT_VER.tar.gz"
    tar xzf "$OUT/ft.tar.gz" -C "$OUT"
fi

# SDL (joystick/haptic stay enabled; the android core references their JNI paths)
cmake -S "$OUT/SDL2-$SDL_VER" -B "$OUT/sdl-arm64" \
    -DCMAKE_TOOLCHAIN_FILE="$NDK_DIR/build/cmake/android.toolchain.cmake" \
    -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-24 -DANDROID_NDK="$NDK_DIR" \
    -DCMAKE_SHARED_LINKER_FLAGS="-Wl,-z,max-page-size=16384" \
    -DCMAKE_BUILD_TYPE=MinSizeRel -DSDL_SHARED=ON -DSDL_STATIC=OFF -DSDL_TEST=OFF -DSDL_POWER=OFF -DSDL_RENDER=OFF
cmake --build "$OUT/sdl-arm64" -j 4

# FreeType, renamed to libfreetype6.so to match the engine's DllImport("freetype6")
cmake -S "$OUT/freetype-$FT_VER" -B "$OUT/ft-arm64" \
    -DCMAKE_TOOLCHAIN_FILE="$NDK_DIR/build/cmake/android.toolchain.cmake" \
    -DANDROID_ABI=arm64-v8a -DANDROID_PLATFORM=android-24 -DANDROID_NDK="$NDK_DIR" \
    -DCMAKE_SHARED_LINKER_FLAGS="-Wl,-z,max-page-size=16384" \
    -DCMAKE_BUILD_TYPE=MinSizeRel -DBUILD_SHARED_LIBS=ON \
    -DFT_DISABLE_HARFBUZZ=TRUE -DFT_DISABLE_BZIP2=TRUE -DFT_DISABLE_PNG=TRUE -DFT_DISABLE_ZLIB=TRUE
cmake --build "$OUT/ft-arm64" -j 4

cp "$OUT/sdl-arm64/libSDL2.so" "$NATIVE/libSDL2.so"
cp "$OUT/ft-arm64/libfreetype.so" "$NATIVE/libfreetype6.so"
echo "Native libs updated:"
ls -la "$NATIVE"
