namespace BeniceSoft.Abp.AspNetCore.Localizations;

public class CultureMapInfo
{
    public string TargetCulture { get; set; } = string.Empty;

    public List<string> SourceCultures { get; set; } = [];
}