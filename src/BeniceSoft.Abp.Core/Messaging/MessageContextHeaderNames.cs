namespace BeniceSoft.Abp.Core.Messaging;

/// <summary>
/// 跨进程透传当前用户 / 租户时使用的 Header 名
/// </summary>
public static class MessageContextHeaderNames
{
    /// <summary>
    /// 用户 Claims（Hex36(JsonBytes)）
    /// </summary>
    public const string UserClaims = "X-User-Claims";

    public const string TenantId = "TenantId";
}
