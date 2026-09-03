namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IQueryTracker
{
    public object? Track(object entity, IShardingDbContext context);
}

internal sealed class QueryTracker : IQueryTracker
{
    public object? Track(object entity, IShardingDbContext context)
    {
        var db = context.GetExecutor().Create(entity);
        var attachedEntity = db.GetAttachedEntity(entity);
        if (attachedEntity == null)
        {
            db.Attach(entity);
        }
        else
        {
            return attachedEntity;
        }

        return null;
    }
}
