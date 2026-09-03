namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>
/// QueryFuture 入口（EF+ Query Future 风格）。
/// 多个 Future/FutureValue 挂在同一 DbContext 批上，首次取值时合并为一次数据库往返。
/// </summary>
public static class QueryFutureExtensions
{
    /// <summary>
    /// 延迟执行列表查询，加入当前 DbContext 的 Future 批；取值时触发批执行。
    /// </summary>
    public static QueryFutureEnumerable<T> Future<T>(this IQueryable<T> query)
    {
        if (!QueryFutureManager.AllowQueryBatch)
        {
            var queryFuture = new QueryFutureEnumerable<T>(null, null);
            queryFuture.GetResultDirectly(query);
            return queryFuture;
        }

        QueryFutureBatch futureBatch;
        QueryFutureEnumerable<T> futureQuery;
        if (query.IsInMemoryQueryContext())
        {
            var ctx = query.GetInMemoryContext();
            futureBatch = QueryFutureManager.AddOrGetBatch(ctx);
            futureBatch.IsInMemory = true;
            futureQuery = new QueryFutureEnumerable<T>(futureBatch, query);
        }
        else
        {
            var ctx = query.GetDbContext();
            futureBatch = QueryFutureManager.AddOrGetBatch(ctx);
            futureQuery = new QueryFutureEnumerable<T>(futureBatch, query);
        }

        futureBatch.Queries.Add(futureQuery);
        return futureQuery;
    }

    /// <summary>
    /// 延迟执行标量查询（Count/First 等），加入 Future 批；访问 Value 时触发批执行。
    /// </summary>
    public static QueryFutureValue<T> FutureValue<T>(this IQueryable<T> query)
    {
        if (!QueryFutureManager.AllowQueryBatch)
        {
            var futureValue = new QueryFutureValue<T>(null, null);
            futureValue.GetResultDirectly(query);
            return futureValue;
        }

        QueryFutureBatch futureBatch;
        QueryFutureValue<T> futureQuery;
        if (query.IsInMemoryQueryContext())
        {
            var ctx = query.GetInMemoryContext();
            futureBatch = QueryFutureManager.AddOrGetBatch(ctx);
            futureBatch.IsInMemory = true;
            futureQuery = new QueryFutureValue<T>(futureBatch, query);
        }
        else
        {
            var ctx = query.GetDbContext();
            futureBatch = QueryFutureManager.AddOrGetBatch(ctx);
            futureQuery = new QueryFutureValue<T>(futureBatch, query);
        }

        futureBatch.Queries.Add(futureQuery);
        return futureQuery;
    }
}
