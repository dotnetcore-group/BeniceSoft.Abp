using BeniceSoft.Abp.Extensions.Caching.Abstractions.Interfaces;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Extensions.Caching.Internals;

[ExposeServices(typeof(ICacheValueSerializer))]
public class NullCacheValueSerializer : ICacheValueSerializer, ISingletonDependency
{
    private readonly ILogger<NullCacheValueSerializer> _logger;
    private bool _warningLogged;

    public NullCacheValueSerializer(ILogger<NullCacheValueSerializer> logger)
    {
        _logger = logger;
    }

    public string Name => "Null";

    public byte[] Serialize<TValue>(TValue data)
    {
        LogWarningOnce();
        return [];
    }

    public TValue? Deserialize<TValue>(byte[] serializerData)
    {
        LogWarningOnce();
        return default;
    }

    public object? Deserialize(byte[] serializerData, Type valueType)
    {
        LogWarningOnce();
        return default;
    }

    private void LogWarningOnce()
    {
        if (_warningLogged) return;

        _logger.LogWarning(
            "未配置缓存序列化器，缓存功能将不可用。" +
            "请引用 BeniceSoftAbpCachingMessagePackModule 或 BeniceSoftAbpCachingSystemTextJsonModule 模块");
        _warningLogged = true;
    }
}