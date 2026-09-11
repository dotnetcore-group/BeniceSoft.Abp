using System.ComponentModel;

namespace BeniceSoft.Abp.Extensions.ObjectStorage;

/// <summary>
/// 对象存储访问模式（决定公私桶与下载方式）
/// </summary>
public enum StorageAccessMode
{
    /// <summary>
    /// 私有：写入 PrivateBucket，下载走预签名
    /// </summary>
    [Description("Private")]
    Private = 1,

    /// <summary>
    /// 公有：写入 PublicBucket，下载可拼 PublicBaseUrl
    /// </summary>
    [Description("Public")]
    Public = 2,
}
