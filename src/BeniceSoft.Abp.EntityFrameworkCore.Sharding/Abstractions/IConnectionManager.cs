namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IConnectionManager
{
    string GetConnectionString(string name);
}

internal sealed class ConnectionManager(IVirtualDataSource virtualDataSource) : IConnectionManager
{
    public string GetConnectionString(string name)
    {
        if (virtualDataSource.IsDefault(name))
        {
            return virtualDataSource.DefaultConnection;
        }

        return virtualDataSource.GetPhysicDataSource(name).ConnectionString;
    }
}
