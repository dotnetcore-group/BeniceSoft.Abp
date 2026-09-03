namespace BeniceSoft.Core;

public static class RandomUtils
{
    /// <summary>
    /// get guid string (default 32-bit without hyphens)
    /// </summary>
    public static string GuidString(string format = "n") => Guid.NewGuid().ToString(format);
}
