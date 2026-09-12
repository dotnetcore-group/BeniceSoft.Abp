using Aliyun.OSS;
using Aliyun.OSS.Common;
using BeniceSoft.Abp.Extensions.ObjectStorage.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Timing;

namespace BeniceSoft.Abp.Extensions.ObjectStorage;

public class AliyunOssStorageProvider : IObjectStorageProvider
{
    public StorageProviderType ProviderType => StorageProviderType.AliyunOss;

    private readonly AliyunOssOptions _options;
    private readonly IClock _clock;
    private readonly Lazy<IOss> _client;

    public AliyunOssStorageProvider(IOptions<ObjectStorageOptions> options, IClock clock)
    {
        _options = options.Value.AliyunOss;
        _clock = clock;

        EnsureConfigured();
        _client = new Lazy<IOss>(new OssClient(_options.Endpoint, _options.AccessKeyId, _options.AccessKeySecret));
    }

    public Task PutAsync(ObjectPutRequest request, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var meta = new ObjectMetadata { ContentType = request.ContentType };
        _client.Value.PutObject(request.Bucket, request.ObjectKey, request.Content, meta);
        return Task.CompletedTask;
    }

    public Task<Stream> GetAsync(string bucket, string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var obj = _client.Value.GetObject(bucket, objectKey);
        return Task.FromResult(obj.Content);
    }

    public Task DeleteAsync(string bucket, string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        _client.Value.DeleteObject(bucket, objectKey);
        return Task.CompletedTask;
    }

    public Task<ObjectMetadataInfo?> GetMetadataAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        try
        {
            var meta = _client.Value.GetObjectMetadata(bucket, objectKey);
            return Task.FromResult<ObjectMetadataInfo?>(new ObjectMetadataInfo
            {
                Size = meta.ContentLength,
                ContentType = string.IsNullOrWhiteSpace(meta.ContentType)
                    ? "application/octet-stream"
                    : meta.ContentType,
                ETag = meta.ETag
            });
        }
        catch (Exception ex) when (ex is OssException { ErrorCode: "NoSuchKey" or "NotFound" } or HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound })
        {
            return Task.FromResult<ObjectMetadataInfo?>(null);
        }
    }

    public Task<string> GetDownloadUrlAsync(
        string bucket,
        string objectKey,
        TimeSpan expires,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var expire = _clock.Now.Add(expires);
        var uri = _client.Value.GeneratePresignedUri(bucket, objectKey, expire, SignHttpMethod.Get);
        return Task.FromResult(uri.ToString());
    }

    public Task<UploadCredentialResult> GetUploadCredentialAsync(
        UploadCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var expireSeconds = request.ExpireSeconds > 0 ? request.ExpireSeconds : _options.ExpireSeconds;
        var expireAt = new DateTimeOffset(_clock.Now).AddSeconds(expireSeconds);
        var expiration = expireAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.000'Z'");
        var conditions =
            $"[{{\"bucket\":\"{request.Bucket}\"}},[\"eq\",\"$key\",\"{request.ObjectKey}\"]";
        if (request.MaxSize > 0)
        {
            conditions += $",[\"content-length-range\",1,{request.MaxSize}]";
        }

        conditions += "]";
        var policyJson = $"{{\"expiration\":\"{expiration}\",\"conditions\":{conditions}}}";
        var policy = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(policyJson));
        var signature = SignPolicy(policy);

        var host = $"https://{request.Bucket}.{_options.Endpoint.TrimEnd('/')}";
        return Task.FromResult(new UploadCredentialResult
        {
            UploadUrl = host,
            HttpMethod = "POST",
            Bucket = request.Bucket,
            ObjectKey = request.ObjectKey,
            ExpireAt = expireAt,
            FormFields =
            {
                ["key"] = request.ObjectKey,
                ["policy"] = policy,
                ["OSSAccessKeyId"] = _options.AccessKeyId,
                ["signature"] = signature,
                ["success_action_status"] = "200",
            }
        });
    }

    public Task<string> InitiateMultipartAsync(
        string bucket,
        string objectKey,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        ObjectMetadata? meta = null;
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            meta = new ObjectMetadata { ContentType = contentType };
        }

        var result = meta is null
            ? _client.Value.InitiateMultipartUpload(new InitiateMultipartUploadRequest(bucket, objectKey))
            : _client.Value.InitiateMultipartUpload(new InitiateMultipartUploadRequest(bucket, objectKey)
            {
                ObjectMetadata = meta
            });
        return Task.FromResult(result.UploadId);
    }

    public Task<MultipartUploadedPart> UploadPartAsync(
        MultipartUploadPartRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var partRequest = new UploadPartRequest(request.Bucket, request.ObjectKey, request.UploadId)
        {
            InputStream = request.Content,
            PartSize = request.Size ?? request.Content.Length,
            PartNumber = request.PartNumber
        };
        var result = _client.Value.UploadPart(partRequest);
        return Task.FromResult(new MultipartUploadedPart
        {
            PartNumber = request.PartNumber,
            ETag = result.ETag,
            Size = request.Size
        });
    }

    public Task CompleteMultipartAsync(
        string bucket,
        string objectKey,
        string uploadId,
        IReadOnlyList<MultipartUploadedPart> parts,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var completeRequest = new CompleteMultipartUploadRequest(bucket, objectKey, uploadId);
        foreach (var part in parts.OrderBy(x => x.PartNumber))
        {
            completeRequest.PartETags.Add(new PartETag(part.PartNumber, part.ETag));
        }

        _client.Value.CompleteMultipartUpload(completeRequest);
        return Task.CompletedTask;
    }

    public Task AbortMultipartAsync(
        string bucket,
        string objectKey,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        _client.Value.AbortMultipartUpload(new AbortMultipartUploadRequest(bucket, objectKey, uploadId));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MultipartUploadedPart>> ListPartsAsync(
        string bucket,
        string objectKey,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var result = _client.Value.ListParts(new ListPartsRequest(bucket, objectKey, uploadId));
        IReadOnlyList<MultipartUploadedPart> parts = result.Parts
            .Select(x => new MultipartUploadedPart
            {
                PartNumber = x.PartNumber,
                ETag = x.ETag,
                Size = x.Size
            })
            .ToList();
        return Task.FromResult(parts);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint)
            || string.IsNullOrWhiteSpace(_options.AccessKeyId)
            || string.IsNullOrWhiteSpace(_options.AccessKeySecret))
        {
            throw new UserFriendlyException("阿里云 OSS 配置不完整");
        }
    }

    private string SignPolicy(string policyBase64)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA1(
            System.Text.Encoding.UTF8.GetBytes(_options.AccessKeySecret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(policyBase64));
        return Convert.ToBase64String(hash);
    }
}
