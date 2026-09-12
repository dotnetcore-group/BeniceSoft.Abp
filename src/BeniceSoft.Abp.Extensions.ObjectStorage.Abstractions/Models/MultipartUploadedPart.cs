namespace BeniceSoft.Abp.Extensions.ObjectStorage.Abstractions;

public class MultipartUploadedPart
{
    public int PartNumber { get; set; }

    public string ETag { get; set; } = string.Empty;

    public long? Size { get; set; }
}
