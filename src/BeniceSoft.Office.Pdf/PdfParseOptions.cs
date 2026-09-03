namespace BeniceSoft.Office.Pdf;

/// <summary>Pixel rectangle in the rendered page bitmap (origin top-left).</summary>
public readonly record struct PdfPixelRect(int Left, int Top, int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>Options controlling page-by-page PDF parsing.</summary>
public sealed class PdfParseOptions
{
    /// <summary>Extract page text with PdfPig. Default true.</summary>
    public bool ExtractText { get; set; } = true;

    /// <summary>
    /// Build a key/value <see cref="PdfFieldResult"/> list from text (and barcodes when read).
    /// Default true; ignored when <see cref="ExtractText"/> is false and barcodes are not read.
    /// </summary>
    public bool ExtractFields { get; set; } = true;

    /// <summary>Emit a single-page PDF byte[] for each page. Default false (expensive).</summary>
    public bool IncludePageBytes { get; set; }

    /// <summary>Render each page to PNG when true. Default false.</summary>
    public bool IncludePageImage { get; set; }

    /// <summary>Render + ZXing barcode/QR scan. Default false.</summary>
    public bool ReadBarcodes { get; set; }

    /// <summary>Render DPI used for images / barcodes. Default 150.</summary>
    public int Dpi { get; set; } = 150;

    /// <summary>
    /// Minimum letter count before <see cref="PdfPageContentKind.HasText"/> is set strongly
    /// enough to avoid <see cref="PdfPageContentKind.LikelyScanned"/>. Default 20.
    /// </summary>
    public int MinTextCharCount { get; set; } = 20;

    /// <summary>
    /// Image area / page area ratio above which a nearly textless page is marked scanned.
    /// Default 0.85.
    /// </summary>
    public float ScannedImageAreaRatio { get; set; } = 0.85f;

    /// <summary>1-based inclusive start page. Null = first page.</summary>
    public int? FromPage { get; set; }

    /// <summary>1-based inclusive end page. Null = last page.</summary>
    public int? ToPage { get; set; }

    public PdfBarcodeOptions Barcode { get; set; } = new();

    public static PdfParseOptions Default { get; } = new();

    /// <summary>Text + content kind only (no render / barcodes / page bytes).</summary>
    public static PdfParseOptions TextOnly() => new()
    {
        ExtractText = true,
        ExtractFields = true,
        IncludePageBytes = false,
        IncludePageImage = false,
        ReadBarcodes = false
    };

    /// <summary>Full pipeline used by label / logistics PDF intake.</summary>
    public static PdfParseOptions WithBarcodes() => new()
    {
        ExtractText = true,
        ExtractFields = true,
        ReadBarcodes = true,
        Dpi = 200
    };
}

public sealed class PdfBarcodeOptions
{
    /// <summary>
    /// Optional crop in rendered pixels. When null, the whole page is scanned.
    /// </summary>
    public PdfPixelRect? Crop { get; set; }

    public bool TryHarder { get; set; } = true;

    public bool TryInverted { get; set; } = true;

    /// <summary>Null / empty = common 1D + QR formats.</summary>
    public IReadOnlyList<PdfBarcodeFormat>? Formats { get; set; }
}

public enum PdfBarcodeFormat
{
    QrCode,
    Code128,
    Code39,
    Code93,
    Ean13,
    Ean8,
    UpcA,
    UpcE,
    Itf,
    Codabar,
    DataMatrix,
    Pdf417,
    Aztec
}
