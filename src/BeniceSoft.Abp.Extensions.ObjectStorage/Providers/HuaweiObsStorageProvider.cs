using Microsoft.Extensions.Options;
using OBS;
using OBS.Model;
using Volo.Abp;
using Volo.Abp.Timing;

namespace BeniceSoft.Abp.Extensions.ObjectStorage;

public class HuaweiObsStorageProvider : IObjectStorageProvider
{
    private readonly HuaweiObsOptions _options;
    private readonly IClock _clock;
    private readonly Lazy<ObsClient> _client;

    public HuaweiObsStorageProvider(IOptions<ObjectStorageOptions> options, IClock clock)
    {
        _options = options.Value.HuaweiObs;
        _clock = clock;
        _client = new Lazy<ObsClient>(CreateClient);
    }

    public StorageProviderType ProviderType => StorageProviderType.HuaweiObs;

    public Task PutAsync(ObjectPutRequest request, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var putRequest = new PutObjectRequest
        {
            BucketName = request.Bucket,
            ObjectKey = request.ObjectKey,
            InputStream = request.Content,
            ContentType = request.ContentType,
            AutoClose = false
        };
        if (request.Size is > 0)
        {
            putRequest.ContentLength = request.Size;
        }

        _client.Value.PutObject(putRequest);
        return Task.CompletedTask;
    }

    public Task<Stream> GetAsync(string bucket, string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        using var response = _client.Value.GetObject(new GetObjectRequest
        {
            BucketName = bucket,
            ObjectKey = objectKey
        });

        var memory = new MemoryStream();
        response.OutputStream.CopyTo(memory);
        memory.Position = 0;
        return Task.FromResult<Stream>(memory);
    }

    public Task DeleteAsync(string bucket, string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        _client.Value.DeleteObject(new DeleteObjectRequest
        {
            BucketName = bucket,
            ObjectKey = objectKey
        });
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
            var response = _client.Value.GetObjectMetadata(new GetObjectMetadataRequest
            {
                BucketName = bucket,
                ObjectKey = objectKey
            });
            return Task.FromResult<ObjectMetadataInfo?>(new ObjectMetadataInfo
            {
                Size = response.ContentLength,
                ContentType = string.IsNullOrWhiteSpace(response.ContentType)
                    ? "application/octet-stream"
                    : response.ContentType,
                ETag = response.ETag
            });
        }
        catch (ObsException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
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
        var expireSeconds = Math.Max(1, (long)expires.TotalSeconds);
        var request = new CreateTemporarySignatureRequest
        {
            BucketName = bucket,
            ObjectKey = objectKey,
            Method = HttpVerb.GET,
            Expires = expireSeconds
        };

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            request.Headers["response-content-disposition"] =
                $"attachment;filename=\"{fileName.Replace("\"", string.Empty)}\"";
        }

        var response = _client.Value.CreateTemporarySignature(request);
        return Task.FromResult(response.SignUrl);
    }

    public Task<UploadCredentialResult> GetUploadCredentialAsync(
        UploadCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var expireSeconds = request.ExpireSeconds > 0 ? request.ExpireSeconds : _options.ExpireSeconds;
        var expireAt = new DateTimeOffset(_clock.Now).AddSeconds(expireSeconds);

        if (request.MaxSize > 0)
        {
            return Task.FromResult(BuildPostUploadCredential(request, expireAt));
        }

        var signed = _client.Value.CreateTemporarySignature(new CreateTemporarySignatureRequest
        {
            BucketName = request.Bucket,
            ObjectKey = request.ObjectKey,
            Method = HttpVerb.PUT,
            Expires = expireSeconds,
            Headers =
            {
                ["Content-Type"] = request.ContentType
            }
        });

        return Task.FromResult(new UploadCredentialResult
        {
            UploadUrl = signed.SignUrl,
            HttpMethod = "PUT",
            Bucket = request.Bucket,
            ObjectKey = request.ObjectKey,
            ExpireAt = expireAt,
            Headers =
            {
                ["Content-Type"] = request.ContentType
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
        var initRequest = new InitiateMultipartUploadRequest
        {
            BucketName = bucket,
            ObjectKey = objectKey
        };
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            initRequest.ContentType = contentType;
        }

        var result = _client.Value.InitiateMultipartUpload(initRequest);
        return Task.FromResult(result.UploadId);
    }

    public Task<MultipartUploadedPart> UploadPartAsync(
        MultipartUploadPartRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var partRequest = new UploadPartRequest
        {
            BucketName = request.Bucket,
            ObjectKey = request.ObjectKey,
            UploadId = request.UploadId,
            PartNumber = request.PartNumber,
            InputStream = request.Content,
            AutoClose = false
        };
        if (request.Size is > 0)
        {
            partRequest.PartSize = request.Size;
        }

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
        var completeRequest = new CompleteMultipartUploadRequest
        {
            BucketName = bucket,
            ObjectKey = objectKey,
            UploadId = uploadId
        };
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
        _client.Value.AbortMultipartUpload(new AbortMultipartUploadRequest
        {
            BucketName = bucket,
            ObjectKey = objectKey,
            UploadId = uploadId
        });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MultipartUploadedPart>> ListPartsAsync(
        string bucket,
        string objectKey,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var result = _client.Value.ListParts(new ListPartsRequest
        {
            BucketName = bucket,
            ObjectKey = objectKey,
            UploadId = uploadId
        });

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

    private ObsClient CreateClient()
    {
        EnsureConfigured();
        return new ObsClient(_options.AccessKeyId, _options.SecretAccessKey, _options.Endpoint);
    }

    private UploadCredentialResult BuildPostUploadCredential(
        UploadCredentialRequest request,
        DateTimeOffset expireAt)
    {
        var expiration = expireAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.000'Z'");
        var conditions =
            $"[{{\"bucket\":\"{request.Bucket}\"}},[\"eq\",\"$key\",\"{request.ObjectKey}\"],[\"content-length-range\",1,{request.MaxSize}]";
        if (!string.IsNullOrWhiteSpace(request.ContentType))
        {
            conditions += $",[\"eq\",\"$Content-Type\",\"{request.ContentType}\"]";
        }

        conditions += "]";
        var policyJson = $"{{\"expiration\":\"{expiration}\",\"conditions\":{conditions}}}";
        var policy = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(policyJson));
        var signature = SignPolicy(policy);
        var uploadUrl = BuildPostUploadUrl(request.Bucket);

        return new UploadCredentialResult
        {
            UploadUrl = uploadUrl,
            HttpMethod = "POST",
            Bucket = request.Bucket,
            ObjectKey = request.ObjectKey,
            ExpireAt = expireAt,
            FormFields =
            {
                ["key"] = request.ObjectKey,
                ["policy"] = policy,
                ["AccessKeyId"] = _options.AccessKeyId,
                ["signature"] = signature,
                ["content-type"] = request.ContentType,
                ["success_action_status"] = "200",
            }
        };
    }

    private string BuildPostUploadUrl(string bucket)
    {
        var endpoint = _options.Endpoint.Trim().TrimEnd('/');
        if (endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return $"{endpoint}/{bucket}/";
        }

        return $"https://{bucket}.{endpoint}/";
    }

    private string SignPolicy(string policyBase64)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA1(
            System.Text.Encoding.UTF8.GetBytes(_options.SecretAccessKey));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(policyBase64));
        return Convert.ToBase64String(hash);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint)
            || string.IsNullOrWhiteSpace(_options.AccessKeyId)
            || string.IsNullOrWhiteSpace(_options.SecretAccessKey))
        {
            throw new UserFriendlyException("华为云 OBS 配置不完整");
        }
    }
}
