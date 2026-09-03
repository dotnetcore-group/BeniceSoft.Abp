using System.Net;
using BeniceSoft.Core;
using StackExchange.Redis;

namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// 内部扩展方法
/// </summary>
internal static class InternalExtensions
{
    internal static T? ToObject<T>(this RedisValue aim)
    {
        if (aim.IsNull)
        {
            return default;
        }

        return JsonUtils.DeserializeBytes<T>(aim!, JsonUtils.DefaultOptions);
    }

    internal static object? ToObject(this RedisValue aim, Type type)
    {
        if (aim.IsNull)
        {
            return default;
        }

        return JsonUtils.DeserializeBytes(aim!, type, JsonUtils.DefaultOptions);
    }

    internal static RedisValue ToValue<T>(this T aim)
    {
        return JsonUtils.SerializeBytes(aim, JsonUtils.DefaultOptions);
    }

    internal static T?[] ToObjects<T>(this IEnumerable<RedisValue> aim)
    {
        return aim.Select(t => t.ToObject<T>()).ToArray();
    }

    internal static RedisKey[] ToKeys(this IEnumerable<string> aim)
    {
        return aim.Select(t => (RedisKey)t).ToArray();
    }

    internal static RedisValue[] ToValues<T>(this IEnumerable<T> aim)
    {
        return aim.Select(t => t.ToValue()).ToArray();
    }

    internal static HashSet<T?> ToSetObject<T>(this IEnumerable<RedisValue> aim)
    {
        return new(aim.Select(t => t.ToObject<T>()));
    }

    internal static string GetFriendlyName(this EndPoint endPoint)
    {
        if (endPoint is DnsEndPoint dnsEndPoint)
        {
            return $"{dnsEndPoint.Host}:{dnsEndPoint.Port}";
        }

        if (endPoint is IPEndPoint ipEndPoint)
        {
            return $"{ipEndPoint.Address}:{ipEndPoint.Port}";
        }

        return endPoint.ToString() ?? string.Empty;
    }

    internal static RedisChannel CreateChannel(this string channel, bool pattern = false)
    {
        if (pattern)
        {
            return RedisChannel.Pattern(channel);
        }

        return RedisChannel.Literal(channel);
    }
}

