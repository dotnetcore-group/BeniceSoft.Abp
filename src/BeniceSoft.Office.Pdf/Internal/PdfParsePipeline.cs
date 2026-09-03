using UglyToad.PdfPig;

namespace BeniceSoft.Office.Pdf.Internal;

internal static class PdfParsePipeline
{
    public static PdfDocumentResult Parse(byte[] pdfBytes, PdfParseOptions options)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(options);

        using var document = PdfDocument.Open(pdfBytes);
        var pages = BuildPages(document, pdfBytes, options).ToList();

        return new PdfDocumentResult
        {
            PageCount = document.NumberOfPages,
            Title = document.Information.Title,
            Author = document.Information.Author,
            Pages = pages
        };
    }

    public static IEnumerable<PdfPageResult> EnumeratePages(byte[] pdfBytes, PdfParseOptions options)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(options);

        using var document = PdfDocument.Open(pdfBytes);
        foreach (var page in BuildPages(document, pdfBytes, options))
        {
            yield return page;
        }
    }

    private static IEnumerable<PdfPageResult> BuildPages(
        PdfDocument document,
        byte[] pdfBytes,
        PdfParseOptions options)
    {
        var from = Math.Max(1, options.FromPage ?? 1);
        var to = Math.Min(document.NumberOfPages, options.ToPage ?? document.NumberOfPages);
        if (from > to)
        {
            yield break;
        }

        var needsRender = options.ReadBarcodes || options.IncludePageImage;

        for (var pageNumber = from; pageNumber <= to; pageNumber++)
        {
            yield return BuildPage(document, pdfBytes, pageNumber, options, needsRender);
        }
    }

    private static PdfPageResult BuildPage(
        PdfDocument document,
        byte[] pdfBytes,
        int pageNumber,
        PdfParseOptions options,
        bool needsRender)
    {
        var page = document.GetPage(pageNumber);
        var contentKind = PageClassifier.Classify(page, options);
        var text = options.ExtractText ? PdfTextReader.Extract(page) : string.Empty;
        IReadOnlyList<PdfFieldResult> fields = Array.Empty<PdfFieldResult>();
        if (options.ExtractFields && options.ExtractText)
        {
            fields = PdfTextReader.ExtractFields(page);
        }

        byte[]? pageBytes = null;
        if (options.IncludePageBytes)
        {
            pageBytes = PdfPageExtractor.ExtractPage(pdfBytes, pageNumber - 1);
        }

        byte[]? pageImage = null;
        IReadOnlyList<PdfBarcodeResult> barcodes = Array.Empty<PdfBarcodeResult>();

        if (needsRender)
        {
            using var bitmap = PdfPageRenderer.RenderPage(pdfBytes, pageNumber - 1, options.Dpi);

            if (options.IncludePageImage)
            {
                pageImage = PdfPageRenderer.ToPng(bitmap);
            }

            if (options.ReadBarcodes)
            {
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
            Width = (float)page.Width,
            Height = (float)page.Height,
            ContentKind = contentKind,
            Text = text,
            Fields = fields,
            PagePdfBytes = pageBytes,
            PageImagePng = pageImage,
            Barcodes = barcodes
        };
    }

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
