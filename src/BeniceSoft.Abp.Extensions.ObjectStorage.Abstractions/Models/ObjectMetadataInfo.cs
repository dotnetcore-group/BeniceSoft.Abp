namespace BeniceSoft.Abp.Extensions.ObjectStorage.Abstractions;

public class ObjectMetadataInfo
{
    public long Size { get; set; }

    public string ContentType { get; set; } = "application/octet-stream";

    public string? ETag { get; set; }
}
