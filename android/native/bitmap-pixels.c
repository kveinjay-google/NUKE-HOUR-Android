#include <stdint.h>
#include <stddef.h>

// Straight-alpha Android RGBA8888 -> the engine's premultiplied BGRA.
// Validate the entire layout before touching either buffer.
int nh_rgba_to_bgra(const uint8_t *src, size_t source_size, uint32_t width,
    uint32_t height, uint32_t stride, uint8_t *dst, size_t destination_size) {
    if (!src || !dst || !width || !height || width > 8192 || height > 8192 ||
        stride < (uint64_t)width * 4 || (uint64_t)stride * height > source_size ||
        (uint64_t)width * height * 4 > destination_size) return -1;
    for (uint32_t y = 0; y < height; ++y) {
        const uint8_t *row = src + (size_t)y * stride;
        for (uint32_t x = 0; x < width; ++x) {
            const unsigned a = row[3];
            *dst++ = (uint8_t)((row[2] * a + 127) / 255);
            *dst++ = (uint8_t)((row[1] * a + 127) / 255);
            *dst++ = (uint8_t)((row[0] * a + 127) / 255);
            *dst++ = (uint8_t)a;
            row += 4;
        }
    }
    return 0;
}

#ifdef __ANDROID__
#include <android/bitmap.h>
__attribute__((visibility("default")))
int nh_bitmap_copy_bgra(JNIEnv *env, jobject bitmap, uint8_t *dst, int length) {
    AndroidBitmapInfo info;
    void *pixels = NULL;
    if (length <= 0 || AndroidBitmap_getInfo(env, bitmap, &info) != ANDROID_BITMAP_RESULT_SUCCESS ||
        info.format != ANDROID_BITMAP_FORMAT_RGBA_8888 ||
        info.width > 8192 || info.height > 8192 ||
        (uint64_t)info.width * info.height * 4 > (uint64_t)length) return -1;
    if (AndroidBitmap_lockPixels(env, bitmap, &pixels) != ANDROID_BITMAP_RESULT_SUCCESS) return -2;
    int result = nh_rgba_to_bgra(pixels, (size_t)info.stride * info.height,
        info.width, info.height, info.stride, dst, (size_t)length);
    int unlocked = AndroidBitmap_unlockPixels(env, bitmap);
    return unlocked == ANDROID_BITMAP_RESULT_SUCCESS ? result : -3;
}
#endif
