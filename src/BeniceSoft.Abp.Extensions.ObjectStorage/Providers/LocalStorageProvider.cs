using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Volo.Abp;

namespace BeniceSoft.Abp.Extensions.ObjectStorage;

public class LocalStorageProvider : IObjectStorageProvider
{
    private readonly ObjectStorageOptions _options;
    private readonly IHostEnvironment _hostEnvironment;

    public LocalStorageProvider(IOptions<ObjectStorageOptions> options, IHostEnvironment hostEnvironment)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
    }

    public StorageProviderType ProviderType => StorageProviderType.Local;

    public async Task PutAsync(ObjectPutRequest request, CancellationToken cancellationToken = default)
    {
        var path = GetPhysicalPath(request.Bucket, request.ObjectKey);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = File.Create(path);
        await request.Content.CopyToAsync(fileStream, cancellationToken);
    }

    public Task<Stream> GetAsync(string bucket, string objectKey, CancellationToken cancellationToken = default)
    {
        var path = GetPhysicalPath(bucket, objectKey);
        if (!File.Exists(path))
        {
            throw new UserFriendlyException($"本地文件不存在: {bucket}/{objectKey}");
        }

        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string bucket, string objectKey, CancellationToken cancellationToken = default)
    {
        var path = GetPhysicalPath(bucket, objectKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<ObjectMetadataInfo?> GetMetadataAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var path = GetPhysicalPath(bucket, objectKey);
        if (!File.Exists(path))
        {
            return Task.FromResult<ObjectMetadataInfo?>(null);
        }

        var info = new FileInfo(path);
        return Task.FromResult<ObjectMetadataInfo?>(new ObjectMetadataInfo
        {
            Size = info.Length,
            ContentType = "application/octet-stream"
        });
    }

    public Task<string> GetDownloadUrlAsync(
        string bucket,
        string objectKey,
        TimeSpan expires,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        var path = GetPhysicalPath(bucket, objectKey);
        if (!File.Exists(path))
        {
            throw new UserFriendlyException($"本地文件不存在: {bucket}/{objectKey}");
        }

        // 本地开发返回 file URI，业务侧应优先走服务端下载接口
        return Task.FromResult(new Uri(path).AbsoluteUri);
    }

    public Task<UploadCredentialResult> GetUploadCredentialAsync(
        UploadCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new UserFriendlyException("本地存储不支持直传，请调用 UploadAsync");
    }

    public Task<string> InitiateMultipartAsync(
        string bucket,
        string objectKey,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var uploadId = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(GetPartsDirectory(bucket, objectKey, uploadId));
        return Task.FromResult(uploadId);
    }

    public async Task<MultipartUploadedPart> UploadPartAsync(
        MultipartUploadPartRequest request,
        CancellationToken cancellationToken = default)
    {
        var partPath = GetPartPath(request.Bucket, request.ObjectKey, request.UploadId, request.PartNumber);
        var directory = Path.GetDirectoryName(partPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using (var fileStream = File.Create(partPath))
        {
            await request.Content.CopyToAsync(fileStream, cancellationToken);
        }

        await using var read = File.OpenRead(partPath);
        var hash = await System.Security.Cryptography.MD5.HashDataAsync(read, cancellationToken);
        var etag = Convert.ToHexString(hash).ToLowerInvariant();
        return new MultipartUploadedPart
        {
            PartNumber = request.PartNumber,
            ETag = etag,
            Size = new FileInfo(partPath).Length
        };
    }

    public async Task CompleteMultipartAsync(
        string bucket,
        string objectKey,
        string uploadId,
        IReadOnlyList<MultipartUploadedPart> parts,
        CancellationToken cancellationToken = default)
    {
        var target = GetPhysicalPath(bucket, objectKey);
        var directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var output = File.Create(target);
        foreach (var part in parts.OrderBy(x => x.PartNumber))
        {
            var partPath = GetPartPath(bucket, objectKey, uploadId, part.PartNumber);
            if (!File.Exists(partPath))
            {
                throw new UserFriendlyException($"分片不存在: {part.PartNumber}");
            }

            await using var input = File.OpenRead(partPath);
            await input.CopyToAsync(output, cancellationToken);
        }

        var partsDir = GetPartsDirectory(bucket, objectKey, uploadId);
        if (Directory.Exists(partsDir))
        {
            Directory.Delete(partsDir, recursive: true);
        }
    }

    public Task AbortMultipartAsync(
        string bucket,
        string objectKey,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        var partsDir = GetPartsDirectory(bucket, objectKey, uploadId);
        if (Directory.Exists(partsDir))
        {
            Directory.Delete(partsDir, recursive: true);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MultipartUploadedPart>> ListPartsAsync(
        string bucket,
        string objectKey,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        var partsDir = GetPartsDirectory(bucket, objectKey, uploadId);
        if (!Directory.Exists(partsDir))
        {
            return Task.FromResult<IReadOnlyList<MultipartUploadedPart>>(Array.Empty<MultipartUploadedPart>());
        }

        var parts = Directory.GetFiles(partsDir)
            .Select(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (!int.TryParse(name, out var partNumber))
                {
                    return null;
                }

                return new MultipartUploadedPart
                {
                    PartNumber = partNumber,
                    ETag = string.Empty,
                    Size = new FileInfo(path).Length
                };
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderBy(x => x.PartNumber)
            .ToList();

        return Task.FromResult<IReadOnlyList<MultipartUploadedPart>>(parts);
    }

    private string GetPartsDirectory(string bucket, string objectKey, string uploadId)
    {
        return Path.Combine(GetPhysicalPath(bucket, "_multipart"), uploadId, objectKey.Replace('/', '_'));
    }

    private string GetPartPath(string bucket, string objectKey, string uploadId, int partNumber)
    {
        return Path.Combine(GetPartsDirectory(bucket, objectKey, uploadId), $"{partNumber:D5}.part");
    }

    private string GetPhysicalPath(string bucket, string objectKey)
    {
        var root = _options.Local.RootPath;
        if (!Path.IsPathRooted(root))
        {
            root = Path.Combine(_hostEnvironment.ContentRootPath, root);
        }

        var safeKey = objectKey.Replace('\\', '/').TrimStart('/');
        return Path.GetFullPath(Path.Combine(root, bucket, safeKey.Replace('/', Path.DirectorySeparatorChar)));
    }
}
