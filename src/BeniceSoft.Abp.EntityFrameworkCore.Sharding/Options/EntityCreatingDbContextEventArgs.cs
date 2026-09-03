using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public class EntityCreatingDbContextEventArgs(object entity) : EventArgs
{
    public object Entity { get; } = entity;
}

public class EntityCreatedDbContextEventArgs(object entity, DbContext ctx) : EventArgs
{
    public object Entity { get; } = entity;

    public DbContext DbContext { get; } = ctx;
}

public class CreatingDbContextEventArgs(CreateDbStrategy strategy, string dataSource, IRouteTail routeTail) : EventArgs
{
    public CreateDbStrategy Strategy { get; } = strategy;

    public string DataSource { get; } = dataSource;

    public IRouteTail RouteTail { get; } = routeTail;
}

public class CreatedDbContextEventArgs(CreateDbStrategy strategy, string dataSource, IRouteTail routeTail, DbContext ctx) : EventArgs
{
    public CreateDbStrategy Strategy { get; } = strategy;

    public string DataSource { get; } = dataSource;

    public IRouteTail RouteTail { get; } = routeTail;

    public DbContext DbContext { get; } = ctx;
}
