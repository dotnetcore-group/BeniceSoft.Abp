using Spire.Pdf;

namespace BeniceSoft.Office.Pdf.Internal;

internal static class PdfPageExtractor
{
    public static byte[] ExtractPage(PdfDocument document, int pageIndex0)
    {
        var pageCount = document.Pages.Count;
        if (pageIndex0 < 0 || pageIndex0 >= pageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex0), $"Page index {pageIndex0} is out of range.");
        }

        using var output = new PdfDocument();
        output.InsertPage(document, pageIndex0);

        using var outputStream = new MemoryStream();
        output.SaveToStream(outputStream);
        return outputStream.ToArray();
    }
}
