#include <stdint.h>

static inline uint8_t clamp(int value) {
    return value < 0 ? 0 : value > 255 ? 255 : (uint8_t)value;
}

// Image planes and destination are pinned by the managed caller. No allocations.
__attribute__((visibility("default")))
void nh_video_convert420(const uint8_t *y, const uint8_t *u, const uint8_t *v,
    int ys, int us, int vs, int up, int vp, int cx, int cy, int width, int height,
    int bt709, int full_range, uint8_t *out) {
    const int scale = full_range ? 256 : 298, black = full_range ? 0 : 16;
    const int rv = bt709 ? (full_range ? 403 : 459) : (full_range ? 359 : 409);
    const int gu = bt709 ? (full_range ? 48 : 55) : (full_range ? 88 : 100);
    const int gv = bt709 ? (full_range ? 120 : 136) : (full_range ? 183 : 208);
    const int bu = bt709 ? (full_range ? 475 : 541) : (full_range ? 454 : 516);
    for (int row = 0; row < height; ++row) {
        const uint8_t *yr = y + (cy + row) * ys;
        const uint8_t *ur = u + ((cy + row) / 2) * us;
        const uint8_t *vr = v + ((cy + row) / 2) * vs;
        for (int col = 0; col < width; ++col) {
            int x = cx + col, c = yr[x] - black;
            int yy = (c > 0 ? c : 0) * scale;
            int cb = ur[(x / 2) * up] - 128, cr = vr[(x / 2) * vp] - 128;
            *out++ = clamp((yy + bu * cb + 128) >> 8);
            *out++ = clamp((yy - gu * cb - gv * cr + 128) >> 8);
            *out++ = clamp((yy + rv * cr + 128) >> 8);
            *out++ = 255;
        }
    }
}
