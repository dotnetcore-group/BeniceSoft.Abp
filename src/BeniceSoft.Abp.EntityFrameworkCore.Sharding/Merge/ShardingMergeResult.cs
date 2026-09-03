using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal sealed class ShardingMergeResult<T>(DbContext? ctx, T result)
{
    public DbContext? DbContext { get; } = ctx;

    public T Result { get; } = result;
}
