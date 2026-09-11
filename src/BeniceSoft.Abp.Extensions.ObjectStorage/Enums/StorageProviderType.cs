using System.ComponentModel;

namespace BeniceSoft.Abp.Extensions.ObjectStorage;

/// <summary>
/// 对象存储提供方
/// </summary>
public enum StorageProviderType
{
    /// <summary>
    /// 本地磁盘
    /// </summary>
    [Description("Local")]
    Local = 1,

    /// <summary>
    /// 阿里云 OSS
    /// </summary>
    [Description("AliyunOss")]
    AliyunOss = 2,

    /// <summary>
    /// MinIO
    /// </summary>
    [Description("MinIO")]
    Minio = 3,

    /// <summary>
    /// 华为云 OBS
    /// </summary>
    [Description("HuaweiObs")]
    HuaweiObs = 4,
}
