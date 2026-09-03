namespace BeniceSoft.Office.Pdf;

public sealed class PdfDocumentResult
{
    public int PageCount { get; init; }

    public string? Title { get; init; }

    public string? Author { get; init; }

    public IReadOnlyList<PdfPageResult> Pages { get; init; } = Array.Empty<PdfPageResult>();
}

public sealed class PdfPageResult
{
    /// <summary>1-based page number.</summary>
    public int PageNumber { get; init; }

    public float Width { get; init; }

    public float Height { get; init; }

    public PdfPageContentKind ContentKind { get; init; }

    public bool HasText => ContentKind.HasFlag(PdfPageContentKind.HasText);

    public bool HasImages => ContentKind.HasFlag(PdfPageContentKind.HasImages);

    public bool LikelyScanned => ContentKind.HasFlag(PdfPageContentKind.LikelyScanned);

    /// <summary>Extracted text when <see cref="PdfParseOptions.ExtractText"/> is enabled.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Structured key/value list derived from page text (and barcodes when scanned).
    /// Empty-key entries are unlabeled lines kept for completeness.
    /// </summary>
    public IReadOnlyList<PdfFieldResult> Fields { get; init; } = Array.Empty<PdfFieldResult>();

    /// <summary>Single-page PDF when <see cref="PdfParseOptions.IncludePageBytes"/> is enabled.</summary>
    public byte[]? PagePdfBytes { get; init; }

    /// <summary>PNG bytes when <see cref="PdfParseOptions.IncludePageImage"/> is enabled.</summary>
    public byte[]? PageImagePng { get; init; }

    public IReadOnlyList<PdfBarcodeResult> Barcodes { get; init; } = Array.Empty<PdfBarcodeResult>();
}

public sealed class PdfFieldResult
{
    public string Key { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;
}

public sealed class PdfBarcodeResult
{
    public string Text { get; init; } = string.Empty;

    public PdfBarcodeFormat? Format { get; init; }

    /// <summary>Confidence is not always available; null means unknown.</summary>
    public float? Confidence { get; init; }
}
