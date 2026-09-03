using BeniceSoft.Core;
using System.Security.Claims;
using System.Text;

namespace BeniceSoft.Abp.Core.Messaging;

/// <summary>
/// 消息上下文中用户 Claims 的编解码，供 MQ / DTM 等跨进程透传复用
/// </summary>
public static class MessageContextTransfer
{
    public sealed record ClaimItem(
        string Type,
        string Value,
        string ValueType,
        string Issuer,
        string OriginalIssuer);

    public static string EncodeClaims(IEnumerable<Claim> claims)
    {
        var items = claims
            .Select(x => new ClaimItem(x.Type, x.Value, x.ValueType, x.Issuer, x.OriginalIssuer))
            .ToList();

        return StringUtils.Hex36String(JsonUtils.SerializeBytes(items));
    }

    public static ClaimsPrincipal? DecodePrincipal(string? encoded, string authenticationType)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        var claimItems = JsonUtils.DeserializeBytes<List<ClaimItem>>(StringUtils.Hex36Bytes(encoded)) ?? [];
        var claims = claimItems
            .Select(x => new Claim(x.Type, x.Value, x.ValueType, x.Issuer, x.OriginalIssuer))
            .ToList();

        return claims.Count == 0
            ? null
            : new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType));
    }

    public static string ReadHeaderString(object? headerValue)
    {
        return headerValue switch
        {
            null => string.Empty,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
            string text => text,
            _ => headerValue.ToStringSafe()
        };
    }
}
