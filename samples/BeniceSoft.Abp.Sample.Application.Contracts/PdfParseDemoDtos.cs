namespace BeniceSoft.Abp.Sample.Application.Contracts;

public class PdfParseDemoResultDto
{
    public string FileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int PageCount { get; set; }

    public string? Title { get; set; }

    public string? Author { get; set; }

    public long ElapsedMilliseconds { get; set; }

    public List<PdfParseDemoPageDto> Pages { get; set; } = new();
}

public class PdfParseDemoPageDto
{
    public int PageNumber { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    public bool HasText { get; set; }

    public bool HasImages { get; set; }

    public bool LikelyScanned { get; set; }

    public string ContentKind { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public List<PdfParseDemoFieldDto> Fields { get; set; } = new();

    public int? PagePdfBytesLength { get; set; }

    public int? PageImagePngLength { get; set; }

    /// <summary>Only filled when includePageImage=true (can be large).</summary>
    public string? PageImagePngBase64 { get; set; }

    public List<PdfParseDemoBarcodeDto> Barcodes { get; set; } = new();
}

public class PdfParseDemoFieldDto
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public class PdfParseDemoBarcodeDto
{
    public string Text { get; set; } = string.Empty;

    public string? Format { get; set; }
}
