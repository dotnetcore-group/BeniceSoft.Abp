using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IDbContextOptionsBuilderCreator
{
    DbContextOptionsBuilder Create(DbContext? shellDbContext);
}

internal sealed class DbContextOptionsBuilderCreator(IDbContextAware aware) : IDbContextOptionsBuilderCreator
{
    public DbContextOptionsBuilder Create(DbContext? shellDbContext)
    {
        var dbType = aware.DbType;
        var type = typeof(DbContextOptionsBuilder<>).MakeGenericType(dbType);
        var builder = Activator.CreateInstance(type) as DbContextOptionsBuilder
                      ?? throw new InvalidOperationException($"Failed to create DbContextOptionsBuilder for [{dbType}]");
        if (shellDbContext != null)
        {
            var applicationServiceProvider = shellDbContext.GetApplicationServiceProvider();
            if (applicationServiceProvider != null)
            {
                builder.UseApplicationServiceProvider(applicationServiceProvider);
            }
        }

        return builder;
    }
}
