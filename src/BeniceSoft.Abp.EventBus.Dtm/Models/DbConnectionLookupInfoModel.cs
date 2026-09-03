namespace BeniceSoft.Abp.EventBus.Dtm;

public class DbConnectionLookupInfoModel
{
    /// <summary>
    /// DbContext类型
    /// </summary>
    public Type DbContextType { get; set; }

    /// <summary>
    /// 租户Id
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// 哈希后的数据库连接字符串
    /// </summary>
    public string HashedConnectionString { get; set; }

    public DbConnectionLookupInfoModel(Type dbContextType, Guid? tenantId, string hashedConnectionString)
    {
        DbContextType = dbContextType;
        TenantId = tenantId;
        HashedConnectionString = hashedConnectionString;
    }
}
