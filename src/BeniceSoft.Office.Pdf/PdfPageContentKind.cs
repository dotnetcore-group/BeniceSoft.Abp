namespace BeniceSoft.Office.Pdf;

/// <summary>
/// Lightweight classification of what a PDF page contains.
/// A page may have both text and images at the same time.
/// </summary>
[Flags]
public enum PdfPageContentKind
{
    None = 0,

    /// <summary>Extractable vector/text objects are present.</summary>
    HasText = 1,

    /// <summary>Embedded image XObjects are present.</summary>
    HasImages = 2,

    /// <summary>
    /// Heuristic: almost no text and one or more large images covering most of the page
    /// (typical scanned page).
    /// </summary>
    LikelyScanned = 4
}
