using PDFtoImage;
using SkiaSharp;

namespace BeniceSoft.Office.Pdf.Internal;

internal static class PdfPageRenderer
{
    public static SKBitmap RenderPage(byte[] pdfBytes, int pageIndex0, int dpi)
    {
        var options = new RenderOptions(Dpi: Math.Clamp(dpi, 72, 600));
#pragma warning disable CA1416
        return Conversion.ToImage(pdfBytes, page: pageIndex0, options: options);
#pragma warning restore CA1416
    }

    public static byte[] ToPng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    public static SKBitmap Crop(SKBitmap source, PdfPixelRect crop)
    {
        var left = Math.Clamp(crop.Left, 0, Math.Max(0, source.Width - 1));
        var top = Math.Clamp(crop.Top, 0, Math.Max(0, source.Height - 1));
        var width = Math.Clamp(crop.Width, 1, source.Width - left);
        var height = Math.Clamp(crop.Height, 1, source.Height - top);

        var dest = new SKBitmap(width, height);
        var rect = SKRectI.Create(left, top, width, height);
        if (source.ExtractSubset(dest, rect))
        {
            return dest;
        }

        dest.Dispose();
        dest = new SKBitmap(width, height);
        using var canvas = new SKCanvas(dest);
        canvas.DrawBitmap(
            source,
            SKRect.Create(left, top, width, height),
            SKRect.Create(0, 0, width, height),
            new SKSamplingOptions(SKFilterMode.Linear));
        return dest;
    }
}
