using BeniceSoft.Abp.Extensions.Caching.Abstractions.Interfaces;
using MessagePack;
using MessagePack.Resolvers;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Extensions.Caching.MessagePack;

[ExposeServices(typeof(ICacheValueSerializer))]
[Dependency(ReplaceServices = true)]
public class MessagePackCacheValueSerializer : ICacheValueSerializer, ISingletonDependency
{
    private static readonly MessagePackSerializerOptions Options = ContractlessStandardResolver.Options;

    public string Name => "MessagePack";

    public byte[] Serialize<TValue>(TValue data)
    {
        return MessagePackSerializer.Serialize(data, Options);
    }

    public TValue? Deserialize<TValue>(byte[] serializerData)
    {
        return MessagePackSerializer.Deserialize<TValue>(serializerData, Options);
    }

    public object? Deserialize(byte[] serializerData, Type valueType)
    {
        return MessagePackSerializer.Deserialize(valueType, serializerData, Options);
    }
}