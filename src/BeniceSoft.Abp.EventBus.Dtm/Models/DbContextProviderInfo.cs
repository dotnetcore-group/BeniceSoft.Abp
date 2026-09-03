using System.Reflection;

namespace BeniceSoft.Abp.EventBus.Dtm;

public class DbContextProviderInfo
{
    public Type DbContextType { get; set; }

    public Type DbContextProviderType { get; set; }

    public MethodInfo GetDbContextAsyncMethodInfo { get; set; }

    public DbContextProviderInfo(Type dbContextType, Type dbContextProviderType, MethodInfo getDbContextAsyncMethodInfo)
    {
        DbContextType = dbContextType;
        DbContextProviderType = dbContextProviderType;
        GetDbContextAsyncMethodInfo = getDbContextAsyncMethodInfo;
    }
}
