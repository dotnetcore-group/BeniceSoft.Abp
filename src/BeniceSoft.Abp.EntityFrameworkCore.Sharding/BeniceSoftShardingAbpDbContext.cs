using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// ABP + 分片壳 DbContext：UoW / IRepository 持有此类实例；物理库由 <see cref="IShardingDbContextExecutor"/> 创建。
/// 若配置了分表路由，请同时继承 <see cref="IShardingTableDbContext"/>
/// </summary>
public abstract class BeniceSoftShardingAbpDbContext<TDbContext> : BeniceSoftAbpDbContext<TDbContext>, IShardingDbContext
    where TDbContext : DbContext
{
    private bool _executorCreated;
    private ShardingDbContextExecutor? _executor;

    protected BeniceSoftShardingAbpDbContext(DbContextOptions<TDbContext> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    public IShardingDbContextExecutor? TryGetExecutor()
    {
        if (!_executorCreated)
        {
            if (this.IsShellDbContext())
            {
                _executor = new ShardingDbContextExecutor(this);
            }

            _executorCreated = true;
        }

        return _executor;
    }

    /// <inheritdoc />
    public IShardingDbContextExecutor GetExecutor()
        => TryGetExecutor()
           ?? throw new ShardingInvalidOperationException(
               $"{GetType().Name} is a physical sharding DbContext and has no executor. Use the shell DbContext.");

    /// <inheritdoc />
    /// <remarks>物理 DbContext 为 true；壳为 false。</remarks>
    public bool IsExecutor => TryGetExecutor() is null;

    public override void Dispose()
    {
        _executor?.Dispose();
        base.Dispose();
    }

    public override async ValueTask DisposeAsync()
    {
        if (_executor is not null)
        {
            await _executor.DisposeAsync();
        }

        await base.DisposeAsync();
    }
}
