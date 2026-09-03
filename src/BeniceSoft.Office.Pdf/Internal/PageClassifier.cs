using UglyToad.PdfPig.Content;

namespace BeniceSoft.Office.Pdf.Internal;

internal static class PageClassifier
{
    public static PdfPageContentKind Classify(Page page, PdfParseOptions options)
    {
        var letterCount = page.Letters.Count;
        var images = page.GetImages().ToList();
        var kind = PdfPageContentKind.None;

        if (letterCount > 0)
        {
            kind |= PdfPageContentKind.HasText;
        }

        if (images.Count > 0)
        {
            kind |= PdfPageContentKind.HasImages;
        }

        var pageArea = Math.Max(1d, page.Width * page.Height);
        var imageArea = images.Sum(image =>
        {
            var bounds = image.BoundingBox;
            return Math.Max(0d, bounds.Width * bounds.Height);
        });

        var ratio = imageArea / pageArea;
        if (letterCount < options.MinTextCharCount && ratio >= options.ScannedImageAreaRatio)
        {
            kind |= PdfPageContentKind.LikelyScanned;
        }

        return kind;
    }
}
