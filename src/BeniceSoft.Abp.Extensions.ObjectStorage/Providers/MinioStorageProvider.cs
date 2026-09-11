using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Volo.Abp;
using Volo.Abp.Timing;

namespace BeniceSoft.Abp.Extensions.ObjectStorage;

public class MinioStorageProvider : IObjectStorageProvider
{
    private readonly MinioOptions _options;
    private readonly IClock _clock;
    private readonly Lazy<IMinioClient> _client;

    public MinioStorageProvider(IOptions<ObjectStorageOptions> options, IClock clock)
    {
        _options = options.Value.Minio;
        _clock = clock;
        _client = new Lazy<IMinioClient>(CreateClient);
    }

    public StorageProviderType ProviderType => StorageProviderType.Minio;

    public async Task PutAsync(ObjectPutRequest request, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var size = request.Size ?? request.Content.Length;
        var args = new PutObjectArgs()
            .WithBucket(request.Bucket)
            .WithObject(request.ObjectKey)
            .WithStreamData(request.Content)
            .WithObjectSize(size)
            .WithContentType(request.ContentType);
        await _client.Value.PutObjectAsync(args, cancellationToken);
    }

    public async Task<Stream> GetAsync(string bucket, string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var memory = new MemoryStream();
        var args = new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithCallbackStream(stream => stream.CopyTo(memory));
        await _client.Value.GetObjectAsync(args, cancellationToken);
        memory.Position = 0;
        return memory;
    }

    public async Task DeleteAsync(string bucket, string objectKey, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var args = new RemoveObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey);
        await _client.Value.RemoveObjectAsync(args, cancellationToken);
    }

    public async Task<ObjectMetadataInfo?> GetMetadataAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectKey);
            var stat = await _client.Value.StatObjectAsync(args, cancellationToken);
            return new ObjectMetadataInfo
            {
                Size = stat.Size,
                ContentType = string.IsNullOrWhiteSpace(stat.ContentType)
                    ? "application/octet-stream"
                    : stat.ContentType,
                ETag = stat.ETag
            };
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return null;
        }
    }

    public async Task<string> GetDownloadUrlAsync(
        string bucket,
        string objectKey,
        TimeSpan expires,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var args = new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithExpiry((int)expires.TotalSeconds);
        return await _client.Value.PresignedGetObjectAsync(args);
    }

    public async Task<UploadCredentialResult> GetUploadCredentialAsync(
        UploadCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var expireSeconds = request.ExpireSeconds > 0 ? request.ExpireSeconds : _options.ExpireSeconds;
        var expireAt = new DateTimeOffset(_clock.Now).AddSeconds(expireSeconds);

        if (request.MaxSize > 0)
        {
            var policy = new PostPolicy();
            policy.SetExpires(expireAt.UtcDateTime);
            policy.SetBucket(request.Bucket);
            policy.SetKey(request.ObjectKey);
            policy.SetContentType(request.ContentType);
            policy.SetContentRange(1, request.MaxSize);
            policy.SetSuccessStatusAction("200");

            var (uri, formData) = await _client.Value.PresignedPostPolicyAsync(policy);
            return new UploadCredentialResult
            {
                UploadUrl = uri.ToString(),
                HttpMethod = "POST",
                Bucket = request.Bucket,
                ObjectKey = request.ObjectKey,
                ExpireAt = expireAt,
                FormFields = formData.ToDictionary(x => x.Key, x => x.Value)
            };
        }

        var args = new PresignedPutObjectArgs()
            .WithBucket(request.Bucket)
            .WithObject(request.ObjectKey)
            .WithExpiry(expireSeconds);
        var url = await _client.Value.PresignedPutObjectAsync(args);
        return new UploadCredentialResult
        {
            UploadUrl = url,
            HttpMethod = "PUT",
            Bucket = request.Bucket,
            ObjectKey = request.ObjectKey,
            ExpireAt = expireAt,
            Headers =
            {
                ["Content-Type"] = request.ContentType
            }
        };
    }

    public Task<string> InitiateMultipartAsync(
        string bucket,
        string objectKey,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        _ = bucket;
        _ = objectKey;
        _ = contentType;
        _ = cancellationToken;

        return Task.FromResult(Guid.NewGuid().ToString("N"));
    }

    public async Task<MultipartUploadedPart> UploadPartAsync(
        MultipartUploadPartRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var size = request.Size ?? request.Content.Length;
        var partKey = GetPartObjectKey(request.ObjectKey, request.UploadId, request.PartNumber);
        var args = new PutObjectArgs()
            .WithBucket(request.Bucket)
            .WithObject(partKey)
            .WithStreamData(request.Content)
            .WithObjectSize(size)
            .WithContentType("application/octet-stream");
        var result = await _client.Value.PutObjectAsync(args, cancellationToken);
        return new MultipartUploadedPart
        {
            PartNumber = request.PartNumber,
            ETag = result.Etag,
            Size = size
        };
    }

    public async Task CompleteMultipartAsync(
        string bucket,
        string objectKey,
        string uploadId,
        IReadOnlyList<MultipartUploadedPart> parts,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await using var output = new MemoryStream();
        foreach (var part in parts.OrderBy(x => x.PartNumber))
        {
            var partKey = GetPartObjectKey(objectKey, uploadId, part.PartNumber);
            var getArgs = new GetObjectArgs()
                .WithBucket(bucket)
                .WithObject(partKey)
                .WithCallbackStream(stream => stream.CopyTo(output));
            await _client.Value.GetObjectAsync(getArgs, cancellationToken);
        }

        output.Position = 0;
        var putArgs = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithStreamData(output)
            .WithObjectSize(output.Length)
            .WithContentType("application/octet-stream");
        await _client.Value.PutObjectAsync(putArgs, cancellationToken);
        await AbortMultipartAsync(bucket, objectKey, uploadId, cancellationToken);
    }

    public async Task AbortMultipartAsync(
        string bucket,
        string objectKey,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var prefix = GetPartObjectPrefix(objectKey, uploadId);
        var objects = await ListObjectNamesAsync(bucket, prefix, cancellationToken);
        foreach (var name in objects)
        {
            var removeArgs = new RemoveObjectArgs()
                .WithBucket(bucket)
                .WithObject(name);
            await _client.Value.RemoveObjectAsync(removeArgs, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<MultipartUploadedPart>> ListPartsAsync(
        string bucket,
        string objectKey,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var prefix = GetPartObjectPrefix(objectKey, uploadId);
        var objects = await ListObjectNamesAsync(bucket, prefix, cancellationToken);
        return objects
            .Select(name =>
            {
                var fileName = name.Split('/').LastOrDefault() ?? string.Empty;
                if (!int.TryParse(fileName, out var partNumber))
                {
                    return null;
                }

                return new MultipartUploadedPart
                {
                    PartNumber = partNumber,
                    ETag = string.Empty,
                    Size = null
                };
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderBy(x => x.PartNumber)
            .ToList();
    }

    private async Task<List<string>> ListObjectNamesAsync(
        string bucket,
        string prefix,
        CancellationToken cancellationToken)
    {
        var names = new List<string>();
        var args = new ListObjectsArgs()
            .WithBucket(bucket)
            .WithPrefix(prefix)
            .WithRecursive(true);
        await foreach (var item in _client.Value.ListObjectsEnumAsync(args).WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(item.Key) && !item.IsDir)
            {
                names.Add(item.Key);
            }
        }

        return names;
    }

    private static string GetPartObjectPrefix(string objectKey, string uploadId)
    {
        return $"_multipart/{uploadId}/{objectKey.Trim('/').Replace('/', '_')}/";
    }

    private static string GetPartObjectKey(string objectKey, string uploadId, int partNumber)
    {
        return $"{GetPartObjectPrefix(objectKey, uploadId)}{partNumber:D5}";
    }

    private IMinioClient CreateClient()
    {
        EnsureConfigured();
        var builder = new MinioClient()
            .WithEndpoint(_options.Endpoint)
            .WithCredentials(_options.AccessKey, _options.SecretKey);
        if (_options.UseSsl)
        {
            builder = builder.WithSSL();
        }

        return builder.Build();
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint)
            || string.IsNullOrWhiteSpace(_options.AccessKey)
            || string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new UserFriendlyException("MinIO 配置不完整");
        }
    }
}
