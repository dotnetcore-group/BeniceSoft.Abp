using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace BeniceSoft.Office.Pdf.Internal;

internal static class PdfPageExtractor
{
    public static byte[] ExtractPage(byte[] pdfBytes, int pageIndex0)
    {
        using var inputStream = new MemoryStream(pdfBytes, writable: false);
        using var input = PdfReader.Open(inputStream, PdfDocumentOpenMode.Import);

        if (pageIndex0 < 0 || pageIndex0 >= input.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex0), $"Page index {pageIndex0} is out of range.");
        }

        using var output = new PdfDocument();
        output.AddPage(input.Pages[pageIndex0]);

        using var outputStream = new MemoryStream();
        output.Save(outputStream, false);
        return outputStream.ToArray();
    }
}
