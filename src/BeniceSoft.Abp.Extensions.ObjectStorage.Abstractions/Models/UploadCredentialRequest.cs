namespace BeniceSoft.Abp.Extensions.ObjectStorage.Abstractions;

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
