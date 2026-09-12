namespace BeniceSoft.Abp.Extensions.ObjectStorage.Abstractions;

public class MultipartUploadPartRequest
{
    public string Bucket { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public string UploadId { get; set; } = string.Empty;

    public int PartNumber { get; set; }

    public Stream Content { get; set; } = Stream.Null;

    public long? Size { get; set; }
}
