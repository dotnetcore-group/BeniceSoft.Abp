namespace BeniceSoft.Abp.Extensions.ObjectStorage;

/// <summary>
/// 对象存储配置
/// </summary>
public class ObjectStorageOptions
{
    public const string Section = "ObjectStorage";

    /// <summary>
    /// 存储提供方
    /// </summary>
    public StorageProviderType DefaultProvider { get; set; } = StorageProviderType.Local;

    /// <summary>
    /// 公共存储桶名
    /// </summary>
    public string PublicBucketName { get; set; } = string.Empty;

    /// <summary>
    /// 私有存储桶名
    /// </summary>
    public string PrivateBucketName { get; set; } = string.Empty;

    /// <summary>
    /// 公共桶直链前缀（CDN / 绑定域名），不含末尾 /
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// 上传时是否生成缩略图（与前端入参无关）
    /// </summary>
    public ThumbnailOptions Thumbnail { get; set; } = new();

    public LocalStorageOptions Local { get; set; } = new();

    public AliyunOssOptions AliyunOss { get; set; } = new();

    public MinioOptions Minio { get; set; } = new();

    public HuaweiObsOptions HuaweiObs { get; set; } = new();

    /// <summary>
    /// 按访问模式解析桶名
    /// </summary>
    public string ResolveBucket(StorageAccessMode accessMode)
    {
        var bucket = accessMode == StorageAccessMode.Public
            ? PublicBucketName
            : PrivateBucketName;

        if (string.IsNullOrWhiteSpace(bucket))
        {
            throw new InvalidOperationException(
                accessMode == StorageAccessMode.Public
                    ? "未配置 ObjectStorage:PublicBucketName"
                    : "未配置 ObjectStorage:PrivateBucketName");
        }

        return bucket;
    }

    public bool IsPublicBucket(string? bucket) =>
        !string.IsNullOrWhiteSpace(bucket)
        && !string.IsNullOrWhiteSpace(PublicBucketName)
        && string.Equals(bucket, PublicBucketName, StringComparison.Ordinal);

    public string? TryBuildPublicUrl(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(PublicBaseUrl) || string.IsNullOrWhiteSpace(objectKey))
        {
            return null;
        }

        return $"{PublicBaseUrl.TrimEnd('/')}/{objectKey.TrimStart('/')}";
    }
}

/// <summary>
/// 缩略图生成配置（启用后 Upload / 直传完成时自动二次 Put）
/// </summary>
public class ThumbnailOptions
{
    public bool Enabled { get; set; }

    /// <summary>最长边限制（等比缩小）</summary>
    public int MaxWidth { get; set; } = 200;

    public int MaxHeight { get; set; } = 200;

    /// <summary>输出 JPEG 质量 1–100</summary>
    public int JpegQuality { get; set; } = 75;
}

public class LocalStorageOptions
{
    /// <summary>
    /// 本地根目录
    /// </summary>
    public string RootPath { get; set; } = "App_Data/files";
}

public class AliyunOssOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string InternalEndpoint { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string AccessKeySecret { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public int ExpireSeconds { get; set; } = 1800;
}

public class MinioOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool UseSsl { get; set; }
    public int ExpireSeconds { get; set; } = 1800;
}

public class HuaweiObsOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public int ExpireSeconds { get; set; } = 1800;
}
