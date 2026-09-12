namespace BeniceSoft.Abp.Extensions.ObjectStorage.Abstractions;

public class ObjectPutRequest
{
    public string Bucket { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public Stream Content { get; set; } = Stream.Null;

    public string ContentType { get; set; } = "application/octet-stream";

    public long? Size { get; set; }
}
