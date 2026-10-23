using System.Net;

namespace BeniceSoft.Abp.ServiceDiscovery;

/// <summary>
/// 把上报地址收成 scheme://host:port，供网关按服务名 + 地址认实例
/// </summary>
internal static class ServiceAddress
{
    public static string Canonicalize(string raw)
    {
        var text = raw.Trim();
        if (text.Length == 0)
        {
            throw new ServiceRegistryException("服务注册地址为空，请配置 ServiceDiscovery:Address");
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "http://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || string.IsNullOrEmpty(uri.Host)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new ServiceRegistryException($"服务注册地址无效：{raw}");
        }

        if (IsUnroutable(uri.Host))
        {
            throw new ServiceRegistryException(
                $"服务注册地址 {raw} 不是可访问主机，请把 ServiceDiscovery:Address 配成具体 IP 或域名加端口");
        }

        var host = uri.HostNameType == UriHostNameType.IPv6
            ? $"[{uri.Host}]"
            : uri.Host.ToLowerInvariant();
        return $"{uri.Scheme}://{host}:{uri.Port}";
    }

    private static bool IsUnroutable(string host)
    {
        if (host is "+" or "*" or "0.0.0.0")
        {
            return true;
        }

        return IPAddress.TryParse(host, out var ip)
               && (IPAddress.Any.Equals(ip) || IPAddress.IPv6Any.Equals(ip));
    }
}
