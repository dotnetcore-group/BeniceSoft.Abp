namespace BeniceSoft.Core.Strategy;

public static class IdGeneratorExtensions
{
    public static string NewId(this IIdGenerator id, string prefix, int digits = 0)
    {
        var seq = id.NewSequenceId();
        return seq.NewId(prefix, digits);
    }

    public static async Task<string> NewIdAsync(this IIdGenerator id, string prefix, int digits = 0)
    {
        var seq = await id.NewSequenceIdAsync();
        return seq.NewId(prefix, digits);
    }

    private static string NewId(this long seq, string prefix, int digits = 0)
    {
        if (digits <= 0)
        {
            return $"{prefix}{seq}";
        }

        var len = Math.Floor(Math.Log10(seq) + 1).ToInt32();
        if (len <= digits)
        {
            return $"{prefix}{seq.ToString("D" + digits)}";
        }

        var idStr = seq.ToString().Substring(len - digits, digits);
        return $"{prefix}{idStr}";
    }
}
