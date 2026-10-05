using System;
using System.Diagnostics;
using System.IO;
using Android.Media;
using OpenRA.Mods.RA2.Widgets;

namespace OpenRA.Android;

/// <summary>A persistent stream decoder, owned exclusively by the serial video worker.</summary>
public sealed class AndroidMenuVideoSource : IMenuVideoSource
{
    readonly MediaExtractor extractor = new();
    readonly MediaCodec.BufferInfo info = new();
    MediaCodec? codec;
    bool inputEnded, disposed;
    int lastIndex = -1;
    bool bt709, fullRange;
    long decodedFrames, totalReadTicks, firstFrameTicks;
    public int Width { get; }
    public int Height { get; }
    public double Fps { get; }
    public double Duration { get; }

    public AndroidMenuVideoSource(string path)
    {
        try
        {
            extractor.SetDataSource(path);
            MediaFormat? format = null;
            string? mime = null;
            for (var track = 0; track < extractor.TrackCount; track++)
            {
                var candidate = extractor.GetTrackFormat(track);
                var type = candidate.GetString(MediaFormat.KeyMime);
                if (type?.StartsWith("video/", StringComparison.Ordinal) == true)
                {
                    format = candidate;
                    mime = type;
                    extractor.SelectTrack(track);
                    break;
                }
                candidate.Dispose();
            }
            if (format == null || mime == null)
                throw new InvalidDataException("No menu video track.");
            using (format)
            {
                Width = format.GetInteger(MediaFormat.KeyWidth);
                Height = format.GetInteger(MediaFormat.KeyHeight);
                Fps = format.ContainsKey(MediaFormat.KeyFrameRate) ? format.GetInteger(MediaFormat.KeyFrameRate) : 24;
                Duration = format.GetLong(MediaFormat.KeyDuration) / 1000000d;
                if (Width <= 0 || Height <= 0 || Width > 1920 || Height > 1080 || Fps <= 0 || Duration <= 0)
                    throw new InvalidDataException("Unsupported Android menu video.");
                // COLOR_FormatYUV420Flexible: image planes expose vendor padding/strides.
                format.SetInteger(MediaFormat.KeyColorFormat, 0x7f420888);
                ReadColorFormat(format);
                codec = MediaCodec.CreateDecoderByType(mime) ?? throw new InvalidDataException("No video decoder.");
                codec.Configure(format, null, (MediaCrypto?)null, 0);
                codec.Start();
                Console.WriteLine($"NUKE HOUR Android video decoder: {codec.Name}; {Width}x{Height} at {Fps} fps.");
            }
        }
        catch { Dispose(); throw; }
    }

    void ReadColorFormat(MediaFormat format)
    {
        if (format.ContainsKey(MediaFormat.KeyColorStandard))
            bt709 = format.GetInteger(MediaFormat.KeyColorStandard) == (int)ColorStandard.Bt709;
        else
            bt709 = Height >= 720;
        fullRange = format.ContainsKey(MediaFormat.KeyColorRange) &&
            format.GetInteger(MediaFormat.KeyColorRange) == (int)ColorRange.Full;
    }

    public unsafe bool ReadFrame(int index, byte[] bgraPixels)
    {
        if (disposed || codec == null || index < 0 || bgraPixels.Length < checked(Width * Height * 4)) return false;
        if (index == lastIndex) return true;
        var started = Stopwatch.GetTimestamp();
        if (index < lastIndex)
        {
            codec.Flush();
            extractor.SeekTo(0, MediaExtractorSeekTo.ClosestSync);
            inputEnded = false;
        }
        var targetUs = (long)(index * 1000000d / Fps);
        while (Stopwatch.GetElapsedTime(started).TotalSeconds < 5)
        {
            if (!inputEnded)
            {
                var input = codec.DequeueInputBuffer(0);
                if (input >= 0)
                {
                    using var buffer = codec.GetInputBuffer(input)!;
                    buffer.Clear();
                    var count = extractor.ReadSampleData(buffer, 0);
                    if (count < 0)
                    {
                        codec.QueueInputBuffer(input, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
                        inputEnded = true;
                    }
                    else
                    {
                        codec.QueueInputBuffer(input, 0, count, extractor.SampleTime, 0);
                        extractor.Advance();
                    }
                }
            }
            var output = codec.DequeueOutputBuffer(info, 1000);
            if (output == (int)MediaCodecInfoState.OutputFormatChanged)
            {
                using var format = codec.OutputFormat;
                ReadColorFormat(format);
                continue;
            }
            if (output < 0) continue;
            try
            {
                if (info.Size > 0 && info.PresentationTimeUs + 1000 >= targetUs)
                {
                    using var image = codec.GetOutputImage(output) ?? throw new InvalidDataException("Video image unavailable.");
                    var planes = image.GetPlanes()!;
                    try
                    {
                        using var crop = image.CropRect!;
                        var yp = PlaneAddress(planes[0], crop.Left + Width - 1, crop.Top + Height - 1);
                        var up = PlaneAddress(planes[1], (crop.Left + Width - 1) / 2, (crop.Top + Height - 1) / 2);
                        var vp = PlaneAddress(planes[2], (crop.Left + Width - 1) / 2, (crop.Top + Height - 1) / 2);
                        if (planes[0].PixelStride != 1) throw new InvalidDataException("Unsupported luminance stride.");
                        // Image owns these direct buffers until ReleaseOutputBuffer.
                        // Avoid Java byte-array marshalling and intermediate plane copies.
                        fixed (byte* destination = bgraPixels)
                            AndroidVideoPixels.ConvertDirect(yp, up, vp,
                                planes[0].RowStride, planes[1].RowStride, planes[2].RowStride,
                                planes[1].PixelStride, planes[2].PixelStride, crop.Left, crop.Top,
                                Width, Height, bt709 ? 1 : 0, fullRange ? 1 : 0, (IntPtr)destination);
                    }
                    finally { foreach (var plane in planes) plane.Dispose(); }
                    lastIndex = index;
                    totalReadTicks += Stopwatch.GetTimestamp() - started;
                    if (decodedFrames == 0) firstFrameTicks = Stopwatch.GetTimestamp();
                    if (++decodedFrames % 120 == 0)
                        Console.WriteLine($"NUKE HOUR Android video read: {decodedFrames} frames, mean {totalReadTicks * 1000d / Stopwatch.Frequency / decodedFrames:F1} ms/frame, delivered {(decodedFrames - 1) * (double)Stopwatch.Frequency / (Stopwatch.GetTimestamp() - firstFrameTicks):F1} fps.");
                    return true;
                }
                if ((info.Flags & MediaCodecBufferFlags.EndOfStream) != 0) return false;
            }
            finally { codec.ReleaseOutputBuffer(output, false); }
        }
        throw new TimeoutException("Android video decoder timed out.");
    }

    static IntPtr PlaneAddress(Image.Plane plane, int lastX, int lastY)
    {
        using var buffer = plane.Buffer!;
        var required = checked(lastY * plane.RowStride + lastX * plane.PixelStride + 1);
        var address = buffer.GetDirectBufferAddress();
        if (address == IntPtr.Zero || buffer.Remaining() < required)
            throw new InvalidDataException("Invalid direct video image plane.");
        return IntPtr.Add(address, buffer.Position());
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (codec != null)
        {
            try { codec.Stop(); } catch (Java.Lang.IllegalStateException) { }
            codec.Dispose();
            codec = null;
        }
        extractor.Dispose();
        info.Dispose();
    }
}
