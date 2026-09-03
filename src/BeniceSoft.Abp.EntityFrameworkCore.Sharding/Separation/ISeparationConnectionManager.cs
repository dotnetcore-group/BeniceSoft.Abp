namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface ISeparationConnectionManager
{
    bool AddReadNode(string dataSource, string node, string connectionString);

    string GetReadNode(string dataSource, string? node = null);
}

internal sealed class SeparationConnectionManager : ISeparationConnectionManager, IConnectionManager
{
    private readonly SeparationConnectionResolver _resolver;
    private readonly IVirtualDataSource _virtualDataSource;

    public SeparationConnectionManager(IVirtualDataSource virtualDataSource, ISeparationConnectionFactory factory)
    {
        _virtualDataSource = virtualDataSource;
        var conn = virtualDataSource.Options.Separation.Select(o => factory.Create(virtualDataSource.Options.ReadStrategy, o.Key, o.Value));
        _resolver = new SeparationConnectionResolver(virtualDataSource.Options.ReadStrategy, factory, conn);
    }

    public bool AddReadNode(string dataSource, string node, string connectionString)
    {
        return _resolver.AddConnectionString(dataSource, node, connectionString);
    }

    public string GetConnectionString(string name)
    {
        return GetReadNode(name);
    }

    public string GetReadNode(string dataSource, string? node = null)
    {
        if (!_resolver.Contains(dataSource))
        {
            return _virtualDataSource.GetConnectionString(dataSource);
        }

        return _resolver.GetConnectionString(dataSource, node);
    }
}
