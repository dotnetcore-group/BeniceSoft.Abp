using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>QueryFuture 全局配置与按 DbContext 挂起的批缓存。</summary>
public static class QueryFutureManager
{
    /// <summary>是否允许把多个 Future 合并为一次多结果集往返；false 时退回逐条执行。</summary>
    public static bool AllowQueryBatch { get; set; } = true;

    /// <summary>弱引用表：每个 DbContext 对应一个未执行完的 Future 批。</summary>
    public static ConditionalWeakTable<DbContext, QueryFutureBatch> CacheWeakFutureBatch { get; set; } = [];

    /// <summary>真正执行「多查询合并命令」前触发（单查询或 AllowQueryBatch=false 时不触发）。</summary>
    public static Action<DbCommand>? OnBatchExecuting { get; set; }

    /// <summary>合并命令执行完成后触发。</summary>
    public static Action<DbCommand>? OnBatchExecuted { get; set; }

    public static QueryFutureBatch AddOrGetBatch(DbContext ctx)
    {
        if (!CacheWeakFutureBatch.TryGetValue(ctx, out var futureBatch))
        {
            futureBatch = new QueryFutureBatch(ctx);
            CacheWeakFutureBatch.Add(ctx, futureBatch);
        }

        return futureBatch;
    }
}
