using System.Runtime.InteropServices;
using SkiaSharp;
using Spire.Pdf;

namespace BeniceSoft.Office.Pdf.Internal;

internal static class PdfPageRenderer
{
    public static byte[] RenderPng(PdfDocument document, int pageIndex0, int dpi)
    {
        var clamped = Math.Clamp(dpi, 72, 600);
        using var stream = document.SaveAsImage(pageIndex0, clamped, clamped);
        if (stream is null)
        {
            throw new InvalidOperationException("Failed to render PDF page.");
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public static SKBitmap Decode(byte[] png)
    {
        var bitmap = SKBitmap.Decode(png);
        if (bitmap is null)
        {
            throw new InvalidOperationException("Failed to decode rendered PDF page.");
        }

        return bitmap;
    }

    public static SKBitmap Crop(SKBitmap source, PdfPixelRect crop)
    {
        var left = Math.Clamp(crop.Left, 0, Math.Max(0, source.Width - 1));
        var top = Math.Clamp(crop.Top, 0, Math.Max(0, source.Height - 1));
        var width = Math.Clamp(crop.Width, 1, source.Width - left);
        var height = Math.Clamp(crop.Height, 1, source.Height - top);

        var dest = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(dest);
        canvas.DrawBitmap(
            source,
            new SKRect(left, top, left + width, top + height),
            new SKRect(0, 0, width, height));
        return dest;
    }

    public static byte[] CopyBgra(SKBitmap bitmap)
    {
        if (bitmap.ColorType == SKColorType.Bgra8888)
        {
            return CopyPacked(bitmap);
        }

        using var converted = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(converted);
        canvas.DrawBitmap(bitmap, 0, 0);
        return CopyPacked(converted);
    }

    private static byte[] CopyPacked(SKBitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var rowBytes = bitmap.RowBytes;
        var packed = new byte[width * height * 4];
        var ptr = bitmap.GetPixels();
        if (ptr == IntPtr.Zero || packed.Length == 0)
        {
            return packed;
        }

        if (rowBytes == width * 4)
        {
            Marshal.Copy(ptr, packed, 0, Math.Min(packed.Length, bitmap.ByteCount));
            return packed;
        }

        var rawLength = Math.Min(bitmap.ByteCount, rowBytes * height);
        var raw = new byte[rawLength];
        Marshal.Copy(ptr, raw, 0, raw.Length);
        var copyWidth = width * 4;
        for (var y = 0; y < height; y++)
        {
            var source = y * rowBytes;
            if (source + copyWidth > raw.Length)
            {
                break;
            }

            Buffer.BlockCopy(raw, source, packed, y * copyWidth, copyWidth);
        }

        return packed;
    }
}
