namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IDbContextAware
{
    Type DbType { get; }
}

internal sealed class DbContextAware(Type type) : IDbContextAware
{
    public Type DbType { get; } = type;
}
