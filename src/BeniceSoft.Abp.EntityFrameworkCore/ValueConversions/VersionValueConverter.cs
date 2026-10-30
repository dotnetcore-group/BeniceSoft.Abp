using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BeniceSoft.Abp.EntityFrameworkCore;

public class VersionValueConverter : ValueConverter<Version?, string?>
{
    public VersionValueConverter()
        : base(version => ConvertToString(version), str => ConvertFromString(str))
    {
    }

    private static Version? ConvertFromString(string? str)
    {
        if (Version.TryParse(str, out var version))
        {
            return version;
        }

        return default;
    }

    private static string? ConvertToString(Version? version)
    {
        return version?.ToString();
    }
}
