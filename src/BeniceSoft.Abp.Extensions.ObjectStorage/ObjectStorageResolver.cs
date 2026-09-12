using BeniceSoft.Abp.Extensions.ObjectStorage.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp;

namespace BeniceSoft.Abp.Extensions.ObjectStorage;

public class ObjectStorageResolver : IObjectStorageResolver
{
    private readonly ObjectStorageOptions _options;
    private readonly IReadOnlyDictionary<StorageProviderType, IObjectStorageProvider> _providers;

    public ObjectStorageResolver(
        IOptions<ObjectStorageOptions> options,
        IEnumerable<IObjectStorageProvider> providers)
    {
        _options = options.Value;
        _providers = providers.ToDictionary(x => x.ProviderType);
    }

    public IObjectStorageProvider Resolve(StorageProviderType? provider = null)
    {
        var type = provider ?? _options.DefaultProvider;
        if (!_providers.TryGetValue(type, out var storageProvider))
        {
            throw new UserFriendlyException($"未注册存储提供: {type}");
        }

        return storageProvider;
    }
}
