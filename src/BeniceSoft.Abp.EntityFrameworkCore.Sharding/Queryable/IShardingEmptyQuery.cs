using System.Linq.Expressions;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IShardingEmptyQuery
{
    IQueryable GetQueryable();
}

internal sealed class ShardingEmptyQuery<T>(Expression<Func<T, bool>> expression) : IShardingEmptyQuery
{
    public IQueryable GetQueryable()
    {
        return new List<T>(0).AsQueryable().Where(expression);
    }
}
