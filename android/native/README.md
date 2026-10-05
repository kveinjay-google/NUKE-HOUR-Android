# Android video pixel conversion

`menu-video-pixels.c` converts MediaCodec YUV420 image planes directly into the pinned BGRA frame consumed by the existing renderer. The image/output buffer stays owned until conversion finishes. Plane row/pixel strides and cropping are validated in AndroidMenuVideoSource before calling native code.

Rebuild the small ARM64 library after modifying the C source:

```sh
sh android/scripts/build-video-pixels.sh
```

The script uses the project Android environment and NDK 28.2.13676358, or `ANDROID_NDK_HOME` when supplied. The resulting `android/OpenRA.Android/native/arm64-v8a/libnukehour_video_pixels.so` is included by the Android project. The library uses 16 KiB ELF alignment. Native color/stride/crop checks run via `python3 -m unittest discover -s packaging/tests -p test_android_video_native_pixels.py`.
