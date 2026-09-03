using SkiaSharp;
using ZXing;
using ZXing.SkiaSharp;

namespace BeniceSoft.Office.Pdf.Internal;

internal static class PdfBarcodeScanner
{
    public static IReadOnlyList<PdfBarcodeResult> Scan(SKBitmap bitmap, PdfBarcodeOptions options)
    {
        using var cropped = options.Crop is { } crop && !crop.IsEmpty
            ? PdfPageRenderer.Crop(bitmap, crop)
            : null;

        var source = cropped ?? bitmap;
        var reader = CreateReader(options);
        var found = new Dictionary<string, PdfBarcodeResult>(StringComparer.Ordinal);

        ScanInto(reader, source, found);

        // Explicit crop: caller already narrowed the region — skip tiling.
        if (options.Crop is null || options.Crop.Value.IsEmpty)
        {
            ScanBands(reader, source, found);
            ScanGrid(reader, source, found);
        }

        return found.Values.ToList();
    }

    private static void ScanBands(
        BarcodeReader reader,
        SKBitmap source,
        IDictionary<string, PdfBarcodeResult> found)
    {
        // Horizontal strips catch 1D barcodes that full-page DecodeMultiple often misses.
        const int bandCount = 5;
        var bandHeight = Math.Max(48, source.Height / 4);
        for (var i = 0; i < bandCount; i++)
        {
            var top = source.Height * i / bandCount;
            if (top + bandHeight > source.Height)
            {
                top = Math.Max(0, source.Height - bandHeight);
            }

            using var band = CropRect(source, 0, top, source.Width, bandHeight);
            ScanInto(reader, band, found);
        }

        // Vertical strips for tall/rotated edge codes.
        const int stripCount = 4;
        var stripWidth = Math.Max(48, source.Width / 3);
        for (var i = 0; i < stripCount; i++)
        {
            var left = source.Width * i / stripCount;
            if (left + stripWidth > source.Width)
            {
                left = Math.Max(0, source.Width - stripWidth);
            }

            using var strip = CropRect(source, left, 0, stripWidth, source.Height);
            ScanInto(reader, strip, found);
        }
    }

    private static void ScanGrid(
        BarcodeReader reader,
        SKBitmap source,
        IDictionary<string, PdfBarcodeResult> found)
    {
        const int cols = 3;
        const int rows = 3;
        const float overlap = 0.2f;

        var tileW = Math.Max(64, (int)(source.Width / (cols - (cols - 1) * overlap)));
        var tileH = Math.Max(64, (int)(source.Height / (rows - (rows - 1) * overlap)));
        var stepX = Math.Max(1, (int)(tileW * (1 - overlap)));
        var stepY = Math.Max(1, (int)(tileH * (1 - overlap)));

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var left = Math.Min(col * stepX, Math.Max(0, source.Width - tileW));
                var top = Math.Min(row * stepY, Math.Max(0, source.Height - tileH));
                var width = Math.Min(tileW, source.Width - left);
                var height = Math.Min(tileH, source.Height - top);
                if (width < 16 || height < 16)
                {
                    continue;
                }

                using var tile = CropRect(source, left, top, width, height);
                ScanInto(reader, tile, found);
            }
        }
    }

    private static void ScanInto(
        BarcodeReader reader,
        SKBitmap source,
        IDictionary<string, PdfBarcodeResult> found)
    {
        var multiple = reader.DecodeMultiple(source);
        if (multiple is { Length: > 0 })
        {
            foreach (var result in multiple)
            {
                TryAdd(found, result);
            }

            return;
        }

        TryAdd(found, reader.Decode(source));
    }

    private static void TryAdd(IDictionary<string, PdfBarcodeResult> found, Result? result)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.Text))
        {
            return;
        }

        var mapped = Map(result);
        var key = $"{mapped.Format}|{mapped.Text}";
        found.TryAdd(key, mapped);
    }

    private static SKBitmap CropRect(SKBitmap source, int left, int top, int width, int height)
        => PdfPageRenderer.Crop(source, new PdfPixelRect(left, top, width, height));

    private static BarcodeReader CreateReader(PdfBarcodeOptions options)
    {
        var formats = MapFormats(options.Formats);
        return new BarcodeReader
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = options.TryHarder,
                TryInverted = options.TryInverted,
                PureBarcode = false,
                PossibleFormats = formats
            }
        };
    }

    private static IList<BarcodeFormat> MapFormats(IReadOnlyList<PdfBarcodeFormat>? formats)
    {
        if (formats is null || formats.Count == 0)
        {
            return new List<BarcodeFormat>
            {
                BarcodeFormat.QR_CODE,
                BarcodeFormat.CODE_128,
                BarcodeFormat.CODE_39,
                BarcodeFormat.CODE_93,
                BarcodeFormat.EAN_13,
                BarcodeFormat.EAN_8,
                BarcodeFormat.UPC_A,
                BarcodeFormat.UPC_E,
                BarcodeFormat.ITF,
                BarcodeFormat.CODABAR,
                BarcodeFormat.DATA_MATRIX,
                BarcodeFormat.PDF_417,
                BarcodeFormat.AZTEC
            };
        }

        return formats.Select(ToZXing).Distinct().ToList();
    }

    private static BarcodeFormat ToZXing(PdfBarcodeFormat format) => format switch
    {
        PdfBarcodeFormat.QrCode => BarcodeFormat.QR_CODE,
        PdfBarcodeFormat.Code128 => BarcodeFormat.CODE_128,
        PdfBarcodeFormat.Code39 => BarcodeFormat.CODE_39,
        PdfBarcodeFormat.Code93 => BarcodeFormat.CODE_93,
        PdfBarcodeFormat.Ean13 => BarcodeFormat.EAN_13,
        PdfBarcodeFormat.Ean8 => BarcodeFormat.EAN_8,
        PdfBarcodeFormat.UpcA => BarcodeFormat.UPC_A,
        PdfBarcodeFormat.UpcE => BarcodeFormat.UPC_E,
        PdfBarcodeFormat.Itf => BarcodeFormat.ITF,
        PdfBarcodeFormat.Codabar => BarcodeFormat.CODABAR,
        PdfBarcodeFormat.DataMatrix => BarcodeFormat.DATA_MATRIX,
        PdfBarcodeFormat.Pdf417 => BarcodeFormat.PDF_417,
        PdfBarcodeFormat.Aztec => BarcodeFormat.AZTEC,
        _ => BarcodeFormat.CODE_128
    };

    private static PdfBarcodeResult Map(Result result) => new()
    {
        Text = result.Text?.Trim() ?? string.Empty,
        Format = FromZXing(result.BarcodeFormat),
        Confidence = null
    };

    private static PdfBarcodeFormat? FromZXing(BarcodeFormat format) => format switch
    {
        BarcodeFormat.QR_CODE => PdfBarcodeFormat.QrCode,
        BarcodeFormat.CODE_128 => PdfBarcodeFormat.Code128,
        BarcodeFormat.CODE_39 => PdfBarcodeFormat.Code39,
        BarcodeFormat.CODE_93 => PdfBarcodeFormat.Code93,
        BarcodeFormat.EAN_13 => PdfBarcodeFormat.Ean13,
        BarcodeFormat.EAN_8 => PdfBarcodeFormat.Ean8,
        BarcodeFormat.UPC_A => PdfBarcodeFormat.UpcA,
        BarcodeFormat.UPC_E => PdfBarcodeFormat.UpcE,
        BarcodeFormat.ITF => PdfBarcodeFormat.Itf,
        BarcodeFormat.CODABAR => PdfBarcodeFormat.Codabar,
        BarcodeFormat.DATA_MATRIX => PdfBarcodeFormat.DataMatrix,
        BarcodeFormat.PDF_417 => PdfBarcodeFormat.Pdf417,
        BarcodeFormat.AZTEC => PdfBarcodeFormat.Aztec,
        _ => null
    };
}
