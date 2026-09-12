namespace BeniceSoft.Abp.Extensions.ObjectStorage.Abstractions;

public static class FileScenes
{
    public const string Default = "default";
    public static string Normalize(string? scene) =>
        string.IsNullOrWhiteSpace(scene)
            ? Default
            : scene.Trim().Replace('\\', '/').Trim('/');
}
