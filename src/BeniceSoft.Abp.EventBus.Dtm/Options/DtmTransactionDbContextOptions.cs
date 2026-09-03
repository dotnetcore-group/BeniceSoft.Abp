namespace BeniceSoft.Abp.EventBus.Dtm;

/// <summary>
/// DTM 事务（MSG/TCC/SAGA）共享的 DbContext 绑定配置。
/// </summary>
public class DtmTransactionDbContextOptions
{
    /// <summary>
    /// 默认 DbContext 类型（AssemblyQualifiedName）。
    /// </summary>
    public string? DefaultDbContextTypeName { get; set; }
}
