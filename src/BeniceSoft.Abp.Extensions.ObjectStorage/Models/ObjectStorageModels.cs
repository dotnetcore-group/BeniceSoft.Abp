namespace BeniceSoft.Abp.Extensions.ObjectStorage;

public class ObjectPutRequest
{
    public string Bucket { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public Stream Content { get; set; } = Stream.Null;

    public string ContentType { get; set; } = "application/octet-stream";

    public long? Size { get; set; }
}

public class UploadCredentialRequest
{
    public string Bucket { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public string? FileName { get; set; }

    public int ExpireSeconds { get; set; } = 1800;

    /// <summary>大于 0 时写入 Policy content-length-range。</summary>
    public long MaxSize { get; set; }
}

public class ObjectMetadataInfo
{
    public long Size { get; set; }

    public string ContentType { get; set; } = "application/octet-stream";

    public string? ETag { get; set; }
}

public class UploadCredentialResult
{
    public string UploadUrl { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = "PUT";

    public string Bucket { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public DateTimeOffset ExpireAt { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new();

    public Dictionary<string, string> FormFields { get; set; } = new();
}

public class MultipartUploadPartRequest
{
    public string Bucket { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    public string UploadId { get; set; } = string.Empty;

    public int PartNumber { get; set; }

    public Stream Content { get; set; } = Stream.Null;

    public long? Size { get; set; }
}

public class MultipartUploadedPart
{
    public int PartNumber { get; set; }

    public string ETag { get; set; } = string.Empty;

    public long? Size { get; set; }
}
