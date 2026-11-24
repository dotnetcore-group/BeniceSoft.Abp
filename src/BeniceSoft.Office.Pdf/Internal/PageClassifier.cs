using Spire.Pdf;
using Spire.Pdf.Utilities;

namespace BeniceSoft.Office.Pdf.Internal;

internal static class PageClassifier
{
    public static PdfPageContentKind Classify(
        PdfPageBase page,
        IReadOnlyList<string> lines,
        PdfParseOptions options)
    {
        // 用非空白字符数近似字形数量，避免纯数字页被当成扫描件
        var charCount = 0;
        foreach (var line in lines)
        {
            foreach (var c in line)
            {
                if (!char.IsWhiteSpace(c))
                {
                    charCount++;
                }
            }
        }

        var images = new PdfImageHelper().GetImagesInfo(page) ?? Array.Empty<PdfImageInfo>();
        var kind = PdfPageContentKind.None;

        if (charCount > 0)
        {
            kind |= PdfPageContentKind.HasText;
        }

        if (images.Length > 0)
        {
            kind |= PdfPageContentKind.HasImages;
        }

        var pageArea = Math.Max(1d, page.Size.Width * page.Size.Height);
        var imageArea = 0d;
        foreach (var image in images)
        {
            var bounds = image.Bounds;
            imageArea += Math.Max(0d, bounds.Width * bounds.Height);
        }

        var ratio = imageArea / pageArea;
        if (charCount < options.MinTextCharCount && ratio >= options.ScannedImageAreaRatio)
        {
            kind |= PdfPageContentKind.LikelyScanned;
        }

        return kind;
    }
}
