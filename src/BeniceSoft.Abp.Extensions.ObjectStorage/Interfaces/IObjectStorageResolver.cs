namespace BeniceSoft.Abp.Extensions.ObjectStorage;

/// <summary>
/// 对象存储提供者解析器
/// </summary>
public interface IObjectStorageResolver
{
    IObjectStorageProvider Resolve(StorageProviderType? provider = null);
}
