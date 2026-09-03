using BeniceSoft.Abp.Sample.Application.Contracts;
using BeniceSoft.Office.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace BeniceSoft.Abp.Sample.Application.Services;

/// <summary>
/// PDF 解析示例：Swagger 只上传文件，解析选项在服务内写死。
/// </summary>
[AllowAnonymous]
public class PdfSampleAppService : ApplicationService
{
    /// <summary>Demo 上传上限 200MB（需配合 Host 的 Form/Kestrel 配置）。</summary>
    public const long MaxUploadBytes = 200L * 1024 * 1024;

    private readonly IPdfParser _pdfParser;

    public PdfSampleAppService(IPdfParser pdfParser)
    {
        _pdfParser = pdfParser;
    }

    /// <summary>上传 PDF，返回文本 / fields / 条码（默认开启）。</summary>
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes, ValueLengthLimit = int.MaxValue)]
    public virtual async Task<PdfParseDemoResultDto> ParseAsync(IFormFile file)
    {
        if (file is null || file.Length <= 0)
        {
            throw new UserFriendlyException("请选择 PDF 文件");
        }

        if (file.Length > MaxUploadBytes)
        {
            throw new UserFriendlyException($"文件过大，最大允许 {MaxUploadBytes / 1024 / 1024}MB");
        }

        var ext = Path.GetExtension(file.FileName);
        if (!string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException("仅支持 PDF 文件");
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        // Demo 写死：抽文本 + 字段 + 条码；不回传大图/单页 PDF
        var options = new PdfParseOptions
        {
            ExtractText = true,
            ExtractFields = true,
            ReadBarcodes = true,
            IncludePageImage = false,
            IncludePageBytes = false,
            Dpi = 200
        };

        var sw = Stopwatch.StartNew();
        var result = _pdfParser.Parse(bytes, options);
        sw.Stop();

        return new PdfParseDemoResultDto
        {
            FileName = file.FileName,
            FileSizeBytes = file.Length,
            PageCount = result.PageCount,
            Title = result.Title,
            Author = result.Author,
            ElapsedMilliseconds = sw.ElapsedMilliseconds,
            Pages = result.Pages.Select(page => new PdfParseDemoPageDto
            {
                PageNumber = page.PageNumber,
                Width = page.Width,
                Height = page.Height,
                HasText = page.HasText,
                HasImages = page.HasImages,
                LikelyScanned = page.LikelyScanned,
                ContentKind = page.ContentKind.ToString(),
                Text = page.Text,
                Fields = page.Fields.Select(f => new PdfParseDemoFieldDto
                {
                    Key = f.Key,
                    Value = f.Value
                }).ToList(),
                PagePdfBytesLength = page.PagePdfBytes?.Length,
                PageImagePngLength = page.PageImagePng?.Length,
                PageImagePngBase64 = null,
                Barcodes = page.Barcodes.Select(b => new PdfParseDemoBarcodeDto
                {
                    Text = b.Text,
                    Format = b.Format?.ToString()
                }).ToList()
            }).ToList()
        };
    }
}
