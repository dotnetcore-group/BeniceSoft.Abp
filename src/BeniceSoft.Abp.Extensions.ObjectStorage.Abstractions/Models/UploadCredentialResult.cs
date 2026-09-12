namespace BeniceSoft.Abp.Extensions.ObjectStorage.Abstractions;

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
