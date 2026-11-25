using System.Text.RegularExpressions;
using Spire.Pdf;
using Spire.Pdf.Texts;

namespace BeniceSoft.Office.Pdf.Internal;

internal static class PdfTextReader
{
    private static readonly Regex TrackingLike = new(
        @"^[A-Z]{1,4}\d{10,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> ExtractLines(PdfPageBase page)
    {
        var raw = new PdfTextExtractor(page).ExtractText(new PdfTextExtractOptions
        {
            IsSimpleExtraction = true
        });
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return raw
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 0)
            .ToArray();
    }

    public static IReadOnlyList<PdfFieldResult> BuildFields(IReadOnlyList<string> lines)
    {
        var fields = new List<PdfFieldResult>();
        string? sectionKey = null;
        var sectionValues = new List<string>();

        void FlushSection()
        {
            if (sectionKey is null)
            {
                return;
            }

            fields.Add(new PdfFieldResult
            {
                Key = sectionKey,
                Value = string.Join(Environment.NewLine, sectionValues).Trim()
            });
            sectionKey = null;
            sectionValues.Clear();
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (TrySplitKeyValue(line, out var key, out var value))
            {
                FlushSection();
                if (string.IsNullOrWhiteSpace(value))
                {
                    sectionKey = key;
                    continue;
                }

                fields.Add(new PdfFieldResult { Key = key, Value = value });
                continue;
            }

            if (sectionKey is not null)
            {
                // Stop section when a new tracking-like token or short route code block appears after address
                if (TrackingLike.IsMatch(line) && sectionValues.Count >= 2)
                {
                    FlushSection();
                    fields.Add(new PdfFieldResult { Key = "TrackingNumber", Value = line });
                    continue;
                }

                sectionValues.Add(line);
                continue;
            }

            if (TrackingLike.IsMatch(line))
            {
                if (!fields.Any(f =>
                        string.Equals(f.Key, "TrackingNumber", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(f.Value, line, StringComparison.Ordinal)))
                {
                    fields.Add(new PdfFieldResult { Key = "TrackingNumber", Value = line });
                }

                continue;
            }

            fields.Add(new PdfFieldResult { Key = string.Empty, Value = line });
        }

        FlushSection();
        return fields;
    }

    private static bool TrySplitKeyValue(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var idx = line.IndexOf(':');
        if (idx < 0)
        {
            idx = line.IndexOf('：');
        }

        if (idx <= 0)
        {
            return false;
        }

        var candidateKey = line[..idx].Trim();
        if (candidateKey.Length == 0 || candidateKey.Length > 40 || candidateKey.Contains(' '))
        {
            // Allow short multi-word keys like "SHIP TO" / "SHIP FROM"
            if (!(candidateKey.Length <= 24 && candidateKey.Count(char.IsLetter) >= 2))
            {
                return false;
            }
        }

        // Avoid treating times / ratios as keys (e.g. "12:30")
        if (candidateKey.All(c => char.IsDigit(c) || c == '.'))
        {
            return false;
        }

        key = candidateKey.TrimEnd();
        value = line[(idx + 1)..].Trim();
        return true;
    }
}
