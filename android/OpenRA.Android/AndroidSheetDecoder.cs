using System;
using System.IO;
using System.Runtime.InteropServices;
using Android.Graphics;
using Android.Runtime;
using OpenRA.Primitives;

namespace OpenRA.Android;

internal static class AndroidSheetDecoder
{
    [DllImport("nukehour_video_pixels", EntryPoint = "nh_bitmap_copy_bgra", CallingConvention = CallingConvention.Cdecl)]
    static extern unsafe int CopyBitmap(IntPtr env, IntPtr bitmap, byte* destination, int length);

    public static unsafe (byte[] Pixels, Size Size) Decode(Stream stream)
    {
        // Bound image dimensions before asking Android to allocate the native bitmap.
        var header = new byte[24];
        stream.ReadExactly(header);
        var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82 };
        for (var i = 0; i < signature.Length; i++)
            if (header[i] != signature[i]) throw new InvalidDataException("Invalid PNG header.");
        var width = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20, 4));
        if (width == 0 || height == 0 || width > 8192 || height > 8192)
            throw new InvalidDataException("PNG dimensions exceed the supported UI texture bounds.");
        using var input = new PrefixStream(header, stream);
        using var options = new BitmapFactory.Options
        {
            InScaled = false, InPremultiplied = false, InPreferredConfig = Bitmap.Config.Argb8888
        };
        using var bitmap = BitmapFactory.DecodeStream(input, null, options)
            ?? throw new InvalidDataException("Android PNG decoder returned no image.");
        try
        {
            if (bitmap.Width != width || bitmap.Height != height)
                throw new InvalidDataException("PNG decoder dimensions differ from its header.");
            var pixels = new byte[checked((int)(width * height * 4))];
            fixed (byte* destination = pixels)
                if (CopyBitmap(JNIEnv.Handle, bitmap.Handle, destination, pixels.Length) != 0)
                    throw new InvalidDataException("Android bitmap conversion failed.");
            return (pixels, new Size((int)width, (int)height));
        }
        finally { bitmap.Recycle(); }
    }

    // Replays only the small header; does not copy the compressed PNG or own the source.
    sealed class PrefixStream : Stream
    {
        readonly byte[] prefix;
        readonly Stream source;
        int position;
        public PrefixStream(byte[] prefix, Stream source) { this.prefix = prefix; this.source = source; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (position >= prefix.Length) return source.Read(buffer, offset, count);
            var copied = Math.Min(count, prefix.Length - position);
            Array.Copy(prefix, position, buffer, offset, copied);
            position += copied;
            return copied;
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
