using System.Text;
using Spire.Pdf;

namespace BeniceSoft.Office.Pdf.Internal;

internal static class PdfParsePipeline
{
    public static PdfDocumentResult Parse(byte[] pdfBytes, PdfParseOptions options)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(options);

        using var document = Open(pdfBytes);
        var pages = BuildPages(document, options).ToList();

        return new PdfDocumentResult
        {
            PageCount = document.Pages.Count,
            Title = BlankToNull(document.DocumentInformation?.Title),
            Author = BlankToNull(document.DocumentInformation?.Author),
            Pages = pages
        };
    }

    public static IEnumerable<PdfPageResult> EnumeratePages(byte[] pdfBytes, PdfParseOptions options)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(options);

        using var document = Open(pdfBytes);
        foreach (var page in BuildPages(document, options))
        {
            yield return page;
        }
    }

    private static PdfDocument Open(byte[] pdfBytes)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var document = new PdfDocument();
        try
        {
            document.LoadFromBytes(pdfBytes);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static IEnumerable<PdfPageResult> BuildPages(PdfDocument document, PdfParseOptions options)
    {
        var pageCount = document.Pages.Count;
        var from = Math.Max(1, options.FromPage ?? 1);
        var to = Math.Min(pageCount, options.ToPage ?? pageCount);
        if (from > to)
        {
            yield break;
        }

        var needsRender = options.ReadBarcodes || options.IncludePageImage;

        for (var pageNumber = from; pageNumber <= to; pageNumber++)
        {
            yield return BuildPage(document, pageNumber, options, needsRender);
        }
    }

    private static PdfPageResult BuildPage(
        PdfDocument document,
        int pageNumber,
        PdfParseOptions options,
        bool needsRender)
    {
        var page = document.Pages[pageNumber - 1];
        var lines = PdfTextReader.ExtractLines(page);
        var contentKind = PageClassifier.Classify(page, lines, options);
        var text = options.ExtractText && lines.Count > 0
            ? string.Join(Environment.NewLine, lines)
            : string.Empty;
        IReadOnlyList<PdfFieldResult> fields = Array.Empty<PdfFieldResult>();
        if (options.ExtractFields && options.ExtractText)
        {
            fields = PdfTextReader.BuildFields(lines);
        }

        byte[]? pageBytes = null;
        if (options.IncludePageBytes)
        {
            pageBytes = PdfPageExtractor.ExtractPage(document, pageNumber - 1);
        }

        byte[]? pageImage = null;
        IReadOnlyList<PdfBarcodeResult> barcodes = Array.Empty<PdfBarcodeResult>();

        if (needsRender)
        {
            var png = PdfPageRenderer.RenderPng(document, pageNumber - 1, options.Dpi);
            if (options.IncludePageImage)
            {
                pageImage = png;
            }

            if (options.ReadBarcodes)
            {
                using var bitmap = PdfPageRenderer.Decode(png);
                barcodes = PdfBarcodeScanner.Scan(bitmap, options.Barcode);
            }
        }

        if (options.ExtractFields && barcodes.Count > 0)
        {
            fields = MergeBarcodeFields(fields, barcodes);
        }

        return new PdfPageResult
        {
            PageNumber = pageNumber,
            Width = page.Size.Width,
            Height = page.Size.Height,
            ContentKind = contentKind,
            Text = text,
            Fields = fields,
            PagePdfBytes = pageBytes,
            PageImagePng = pageImage,
            Barcodes = barcodes
        };
    }

    private static string? BlankToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static IReadOnlyList<PdfFieldResult> MergeBarcodeFields(
        IReadOnlyList<PdfFieldResult> fields,
        IReadOnlyList<PdfBarcodeResult> barcodes)
    {
        var merged = fields.ToList();
        foreach (var barcode in barcodes)
        {
            var key = barcode.Format?.ToString() ?? "Barcode";
            if (merged.Any(f =>
                    string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(f.Value, barcode.Text, StringComparison.Ordinal)))
            {
                continue;
            }

            merged.Add(new PdfFieldResult { Key = key, Value = barcode.Text });
        }

        return merged;
    }
}
