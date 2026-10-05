using System;
using System.Runtime.InteropServices;
namespace OpenRA.Android
{
    // Android image planes may be planar or interleaved, with padded rows and a crop.
    public static class AndroidVideoPixels
    {
        [DllImport("nukehour_video_pixels", EntryPoint = "nh_video_convert420", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ConvertDirect(IntPtr y, IntPtr u, IntPtr v,
            int ys, int us, int vs, int up, int vp, int cx, int cy, int width, int height,
            int bt709, int fullRange, IntPtr bgra);

        public static void Convert420(byte[] y, byte[] u, byte[] v, int yStride, int uStride, int vStride,
            int uPixelStride, int vPixelStride, int cropX, int cropY, int width, int height,
            bool bt709, bool fullRange, byte[] bgra)
        {
            if (width <= 0 || height <= 0 || cropX < 0 || cropY < 0 || yStride <= 0 ||
                uStride <= 0 || vStride <= 0 || uPixelStride <= 0 || vPixelStride <= 0 ||
                bgra.Length < checked(width * height * 4) ||
                y.Length <= (cropY + height - 1) * yStride + cropX + width - 1 ||
                u.Length <= ((cropY + height - 1) / 2) * uStride + ((cropX + width - 1) / 2) * uPixelStride ||
                v.Length <= ((cropY + height - 1) / 2) * vStride + ((cropX + width - 1) / 2) * vPixelStride)
                throw new ArgumentException("Invalid YUV image plane bounds.");
            var yScale = fullRange ? 256 : 298;
            var yOffset = fullRange ? 0 : 16;
            var rv = bt709 ? (fullRange ? 403 : 459) : (fullRange ? 359 : 409);
            var gu = bt709 ? (fullRange ? 48 : 55) : (fullRange ? 88 : 100);
            var gv = bt709 ? (fullRange ? 120 : 136) : (fullRange ? 183 : 208);
            var bu = bt709 ? (fullRange ? 475 : 541) : (fullRange ? 454 : 516);
            var offset = 0;
            for (var row = 0; row < height; row++)
            {
                var yRow = (cropY + row) * yStride;
                var uRow = ((cropY + row) / 2) * uStride;
                var vRow = ((cropY + row) / 2) * vStride;
                for (var col = 0; col < width; col++)
                {
                    var x = cropX + col;
                    var luminance = Math.Max(0, y[yRow + x] - yOffset) * yScale;
                    var cb = u[uRow + x / 2 * uPixelStride] - 128;
                    var cr = v[vRow + x / 2 * vPixelStride] - 128;
                    bgra[offset++] = Clamp((luminance + bu * cb + 128) >> 8);
                    bgra[offset++] = Clamp((luminance - gu * cb - gv * cr + 128) >> 8);
                    bgra[offset++] = Clamp((luminance + rv * cr + 128) >> 8);
                    bgra[offset++] = 255;
                }
            }
        }
        static byte Clamp(int value) => (byte)Math.Min(255, Math.Max(0, value));
    }
}
