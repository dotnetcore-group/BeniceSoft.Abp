using System.Text.Json;
using BeniceSoft.Abp.Extensions.Caching.Abstractions.Interfaces;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Extensions.Caching.SystemTextJson;

[ExposeServices(typeof(ICacheValueSerializer))]
[Dependency(ReplaceServices = true)]
public class SystemTextJsonCacheValueSerializer : ICacheValueSerializer, ISingletonDependency
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string Name => "SystemTextJson";

    public byte[] Serialize<TValue>(TValue data)
    {
        return JsonSerializer.SerializeToUtf8Bytes(data, DefaultOptions);
    }

    public TValue? Deserialize<TValue>(byte[] serializerData)
    {
        return JsonSerializer.Deserialize<TValue>(serializerData, DefaultOptions);
    }

    public object? Deserialize(byte[] serializerData, Type valueType)
    {
        return JsonSerializer.Deserialize(serializerData, valueType, DefaultOptions);
    }
}