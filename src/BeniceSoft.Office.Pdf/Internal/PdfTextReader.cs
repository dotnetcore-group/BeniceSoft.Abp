using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;

namespace BeniceSoft.Office.Pdf.Internal;

internal static class PdfTextReader
{
    private static readonly Regex TrackingLike = new(
        @"^[A-Z]{1,4}\d{10,}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Extract(Page page)
    {
        var lines = ExtractLines(page);
        return lines.Count == 0 ? string.Empty : string.Join(Environment.NewLine, lines);
    }

    public static IReadOnlyList<PdfFieldResult> ExtractFields(Page page)
    {
        var lines = ExtractLines(page);
        return BuildFields(lines);
    }

    public static IReadOnlyList<string> ExtractLines(Page page)
    {
        var words = page.GetWords().ToList();
        if (words.Count == 0)
        {
            return Array.Empty<string>();
        }

        var items = words
            .Select(w => new WordBox(
                w.Text.Trim(),
                w.BoundingBox.Left,
                (w.BoundingBox.Bottom + w.BoundingBox.Top) / 2.0,
                w.BoundingBox.Height))
            .Where(w => !string.IsNullOrWhiteSpace(w.Text))
            .OrderByDescending(w => w.CenterY)
            .ThenBy(w => w.Left)
            .ToList();

        if (items.Count == 0)
        {
            return Array.Empty<string>();
        }

        var medianHeight = items
            .Select(w => w.Height)
            .OrderBy(h => h)
            .ElementAt(items.Count / 2);
        var lineTol = Math.Max(2.0, medianHeight * 0.55);

        var lines = new List<string>();
        var current = new List<WordBox> { items[0] };
        var currentY = items[0].CenterY;

        for (var i = 1; i < items.Count; i++)
        {
            var word = items[i];
            if (Math.Abs(word.CenterY - currentY) <= lineTol)
            {
                current.Add(word);
                currentY = current.Average(w => w.CenterY);
                continue;
            }

            lines.Add(JoinLine(current));
            current = new List<WordBox> { word };
            currentY = word.CenterY;
        }

        if (current.Count > 0)
        {
            lines.Add(JoinLine(current));
        }

        return lines;
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
                // Stop section when a new tracking-like token or short route code block appears after address.
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
            // Allow short multi-word keys like "SHIP TO" / "SHIP FROM".
            if (!(candidateKey.Length <= 24 && candidateKey.Count(char.IsLetter) >= 2))
            {
                return false;
            }
        }

        // Avoid treating times / ratios as keys (e.g. "12:30").
        if (candidateKey.All(c => char.IsDigit(c) || c == '.'))
        {
            return false;
        }

        key = candidateKey.TrimEnd();
        value = line[(idx + 1)..].Trim();
        return true;
    }

    private static string JoinLine(List<WordBox> words)
    {
        var ordered = words.OrderBy(w => w.Left).ToList();
        var sb = new StringBuilder();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }

            sb.Append(ordered[i].Text);
        }

        return sb.ToString();
    }

    private readonly record struct WordBox(string Text, double Left, double CenterY, double Height);
}
