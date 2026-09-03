using BeniceSoft.Office.Pdf.Internal;

namespace BeniceSoft.Office.Pdf;

/// <summary>
/// Entry point for PDF parsing. Probe text/images first; render and scan barcodes only when requested.
/// </summary>
public static class Pdf
{
    public static PdfDocumentResult ParseFile(string path, PdfParseOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return Parse(stream, options, leaveOpen: false);
    }

    public static PdfDocumentResult Parse(byte[] bytes, PdfParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return PdfParsePipeline.Parse(bytes, options ?? PdfParseOptions.Default);
    }

    public static PdfDocumentResult Parse(Stream stream, PdfParseOptions? options = null, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var bytes = ReadAllBytes(stream, leaveOpen);
        return PdfParsePipeline.Parse(bytes, options ?? PdfParseOptions.Default);
    }

    /// <summary>Page-by-page enumeration without buffering all page payloads in a list first.</summary>
    public static IEnumerable<PdfPageResult> EnumeratePages(
        Stream stream,
        PdfParseOptions? options = null,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var bytes = ReadAllBytes(stream, leaveOpen);
        return PdfParsePipeline.EnumeratePages(bytes, options ?? PdfParseOptions.Default);
    }

    public static IEnumerable<PdfPageResult> EnumeratePages(byte[] bytes, PdfParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return PdfParsePipeline.EnumeratePages(bytes, options ?? PdfParseOptions.Default);
    }

    private static byte[] ReadAllBytes(Stream stream, bool leaveOpen)
    {
        if (stream is MemoryStream ms && ms.TryGetBuffer(out var segment) && segment.Array is not null
            && segment.Offset == 0 && segment.Count == ms.Length)
        {
            if (leaveOpen)
            {
                var copy = new byte[segment.Count];
                Buffer.BlockCopy(segment.Array, 0, copy, 0, segment.Count);
                return copy;
            }

            return ms.ToArray();
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
            var length = stream.Length - stream.Position;
            if (length < 0 || length > int.MaxValue)
            {
                throw new InvalidOperationException("PDF stream length is invalid.");
            }

            var buffer = new byte[length];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = stream.Read(buffer, read, buffer.Length - read);
                if (n == 0)
                {
                    break;
                }

                read += n;
            }

            if (read != buffer.Length)
            {
                Array.Resize(ref buffer, read);
            }

            return buffer;
        }

        using var copyStream = new MemoryStream();
        stream.CopyTo(copyStream);
        return copyStream.ToArray();
    }
}

/// <summary>DI-friendly wrapper over <see cref="Pdf"/>.</summary>
public interface IPdfParser
{
    PdfDocumentResult Parse(Stream stream, PdfParseOptions? options = null, bool leaveOpen = false);

    PdfDocumentResult Parse(byte[] bytes, PdfParseOptions? options = null);

    PdfDocumentResult ParseFile(string path, PdfParseOptions? options = null);
}

public sealed class PdfParser : IPdfParser
{
    public PdfDocumentResult Parse(Stream stream, PdfParseOptions? options = null, bool leaveOpen = false)
        => Pdf.Parse(stream, options, leaveOpen);

    public PdfDocumentResult Parse(byte[] bytes, PdfParseOptions? options = null)
        => Pdf.Parse(bytes, options);

    public PdfDocumentResult ParseFile(string path, PdfParseOptions? options = null)
        => Pdf.ParseFile(path, options);
}
