namespace BeniceSoft.Abp.EventBus.Dtm;

/// <summary>
/// DTM 请求头名称常量类
/// </summary>
public static class DtmRequestHeaderNames
{
    public static string ActionApiToken { get; set; } = "ActionApiToken";

    public static string DbContextType { get; set; } = "DbContextType";

    public static string TenantId { get; set; } = "TenantId";

    public static string HashedConnectionString { get; set; } = "HashedConnectionString";

    /// <summary>
    /// 用户 Claims（Hex36(JsonBytes)）
    /// </summary>
    public static string UserClaims { get; set; } = "X-User-Claims";
}
