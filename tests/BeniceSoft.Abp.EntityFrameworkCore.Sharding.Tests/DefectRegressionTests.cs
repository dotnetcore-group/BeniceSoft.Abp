using System.Reflection;
using BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests.Fixture;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests;

/// <summary>
/// 缺陷回归：先暴露问题，再修实现。失败即确认漏洞仍在。
/// </summary>
public class DefectRegressionTests : ShardingTestBase
{
    private readonly IRepository<ShardLedger, Guid> _ledgers;
    private readonly IRepository<ShardBucket, Guid> _buckets;
    private readonly IUnitOfWorkManager _uow;

    public DefectRegressionTests(ShardingTestApplication app) : base(app)
    {
        _ledgers = GetRequiredService<IRepository<ShardLedger, Guid>>();
        _buckets = GetRequiredService<IRepository<ShardBucket, Guid>>();
        _uow = GetRequiredService<IUnitOfWorkManager>();
    }

    /// <summary>
    /// MemoryStrictly 跨分片 ToList：并行物理 DbContext 必须在查询结束后全部 Dispose。
    /// 泄漏会导致连接池耗尽（当前最高优先级缺陷）。
    /// </summary>
    [Fact]
    public async Task MemoryStrictly_FanOut_ToList_Should_Dispose_All_Parallel_DbContexts()
    {
        var batch = NewBatch("leak");
        using (var seed = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            await _buckets.InsertAsync(new ShardBucket(Guid.NewGuid(), "e", 10, batch), autoSave: true);
            await _buckets.InsertAsync(new ShardBucket(Guid.NewGuid(), "o", 11, batch), autoSave: true);
            await seed.CompleteAsync();
        }

        using var scope = App.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShardingFixtureDbContext>();
        var executor = db.GetExecutor();
        var parallelContexts = new List<DbContext>();

        void OnCreated(object? sender, CreatedDbContextEventArgs e)
        {
            if (e.Strategy == CreateDbStrategy.ParallelQuery)
            {
                parallelContexts.Add(e.DbContext);
            }
        }

        executor.CreatedDbContext += OnCreated;
        try
        {
            using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
            var rows = await db.Set<ShardBucket>()
                .AsNoTracking()
                .AsConnection(limit: 8, ConnectionMode.MemoryStrictly)
                .Where(x => x.BatchTag == batch)
                .ToListAsync();
            rows.Where(x => x is not null).Count().ShouldBe(2);
            await uow.CompleteAsync();
        }
        finally
        {
            executor.CreatedDbContext -= OnCreated;
        }

        parallelContexts.Count.ShouldBeGreaterThanOrEqualTo(2,
            "fan-out should create at least one ParallelQuery context per mod tail");

        var leaked = parallelContexts.Where(c => !IsDisposed(c)).ToList();
        leaked.ShouldBeEmpty(
            $"leaked {leaked.Count}/{parallelContexts.Count} ParallelQuery DbContext(s) after ToListAsync completed");
    }

    /// <summary>
    /// FirstAsync 同样走 StreamMergeEnumerable，MemoryStrictly 下也必须释放并行上下文。
    /// </summary>
    [Fact]
    public async Task MemoryStrictly_FirstAsync_Should_Dispose_Parallel_DbContexts()
    {
        var batch = NewBatch("leak-first");
        using (var seed = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            await _buckets.InsertAsync(new ShardBucket(Guid.NewGuid(), "e", 2, batch), autoSave: true);
            await _buckets.InsertAsync(new ShardBucket(Guid.NewGuid(), "o", 3, batch), autoSave: true);
            await seed.CompleteAsync();
        }

        using var scope = App.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShardingFixtureDbContext>();
        var executor = db.GetExecutor();
        var parallelContexts = new List<DbContext>();
        void OnCreated(object? sender, CreatedDbContextEventArgs e)
        {
            if (e.Strategy == CreateDbStrategy.ParallelQuery)
            {
                parallelContexts.Add(e.DbContext);
            }
        }

        executor.CreatedDbContext += OnCreated;
        try
        {
            using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
            var first = await db.Set<ShardBucket>()
                .AsNoTracking()
                .AsConnection(limit: 8, ConnectionMode.MemoryStrictly)
                .Where(x => x.BatchTag == batch)
                .OrderBy(x => x.Name)
                .FirstAsync();
            first.ShouldNotBeNull();
            await uow.CompleteAsync();
        }
        finally
        {
            executor.CreatedDbContext -= OnCreated;
        }

        parallelContexts.ShouldNotBeEmpty();
        parallelContexts.Where(c => !IsDisposed(c)).ShouldBeEmpty();
    }

    /// <summary>
    /// 同步 Count/Sum：当前 WrapEnsureMerge.Merge 抛 NotImplemented；
    /// 产品决策：不提供同步聚合，应明确 NotSupported（引导 Async），而非 NotImplemented。
    /// </summary>
    [Fact]
    public async Task Sync_Count_Should_Fail_With_Clear_NotSupported_Not_NotImplemented()
    {
        var batch = NewBatch("sync-cnt");
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var q = (await _ledgers.GetQueryableAsync()).AsNoTracking().Where(x => x.BatchTag == batch);

        var ex = Should.Throw<Exception>(() => q.Count());
        var root = Unwrap(ex);
        root.ShouldNotBeOfType<NotImplementedException>(
            "NotImplementedException is a defect; sync should be explicitly NotSupported");
        (root is NotSupportedException or ShardingException).ShouldBeTrue(
            $"expected NotSupported/ShardingException, got {root.GetType().Name}: {root.Message}");
        root.Message.ShouldContain("Async", Case.Insensitive);
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task Sync_Sum_Should_Fail_With_Clear_NotSupported_Not_NotImplemented()
    {
        var batch = NewBatch("sync-sum");
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var q = (await _ledgers.GetQueryableAsync()).AsNoTracking().Where(x => x.BatchTag == batch);

        var ex = Should.Throw<Exception>(() => q.Sum(x => x.Amount));
        var root = Unwrap(ex);
        root.ShouldNotBeOfType<NotImplementedException>();
        (root is NotSupportedException or ShardingException).ShouldBeTrue(
            $"expected NotSupported/ShardingException, got {root.GetType().Name}: {root.Message}");
        root.Message.ShouldContain("Async", Case.Insensitive);
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task Sync_ExecuteDelete_Should_Fail_With_Clear_NotSupported_Not_NotImplemented()
    {
        var batch = NewBatch("sync-del");
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var q = (await _ledgers.GetQueryableAsync()).Where(x => x.BatchTag == batch);

        var ex = Should.Throw<Exception>(() => q.ExecuteDelete());
        var root = Unwrap(ex);
        root.ShouldNotBeOfType<NotImplementedException>();
        (root is NotSupportedException or ShardingException).ShouldBeTrue(
            $"expected NotSupported/ShardingException, got {root.GetType().Name}: {root.Message}");
        root.Message.ShouldContain("Async", Case.Insensitive);
        await uow.CompleteAsync();
    }

    /// <summary>
    /// 跨分片 GroupBy + Max：同一组键落在多个物理表时触发内存 Max 合并。
    /// nameof(methodName) 写错会导致 Max 反射失败。
    /// </summary>
    [Fact]
    public async Task GroupBy_Max_Across_Shards_Should_Merge_Correctly()
    {
        var batch = NewBatch("gmax");
        await SeedLedgersAsync(
            new ShardLedger(Guid.NewGuid(), "SAME", 10m, new DateTime(2024, 1, 1), batch),
            new ShardLedger(Guid.NewGuid(), "SAME", 30m, new DateTime(2024, 2, 1), batch),
            new ShardLedger(Guid.NewGuid(), "SAME", 20m, new DateTime(2024, 3, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var rows = await (await _ledgers.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BatchTag == batch)
            .GroupBy(x => x.Code)
            .Select(g => new { Code = g.Key, MaxAmount = g.Max(x => x.Amount) })
            .ToListAsync();

        rows.Count.ShouldBe(1);
        rows[0].Code.ShouldBe("SAME");
        rows[0].MaxAmount.ShouldBe(30m);
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task GroupBy_Min_Across_Shards_Should_Merge_Correctly()
    {
        var batch = NewBatch("gmin");
        await SeedLedgersAsync(
            new ShardLedger(Guid.NewGuid(), "SAME", 10m, new DateTime(2024, 1, 1), batch),
            new ShardLedger(Guid.NewGuid(), "SAME", 30m, new DateTime(2024, 2, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var rows = await (await _ledgers.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BatchTag == batch)
            .GroupBy(x => x.Code)
            .Select(g => new { Code = g.Key, MinAmount = g.Min(x => x.Amount) })
            .ToListAsync();

        rows.Count.ShouldBe(1);
        rows[0].MinAmount.ShouldBe(10m);
        await uow.CompleteAsync();
    }

    /// <summary>
    /// 嵌套 AsRoute / RouteScope Dispose 后应恢复外层 Current，而不是清空为 null。
    /// </summary>
    [Fact]
    public void Nested_RouteScope_Dispose_Should_Restore_Outer_Context()
    {
        var manager = GetRequiredService<IShardingRuntimeContext>().RouteManager;
        using (manager.CreateScope())
        {
            var outer = manager.Current;
            outer.ShouldNotBeNull();
            outer.MustTable[typeof(ShardBucket)] = new HashSet<string> { "0" };

            using (manager.CreateScope())
            {
                manager.Current.ShouldNotBeNull();
                manager.Current.ShouldNotBeSameAs(outer);
                manager.Current.MustTable[typeof(ShardBucket)] = new HashSet<string> { "1" };
            }

            manager.Current.ShouldBeSameAs(outer,
                "nested scope Dispose must restore outer ShardingRouteContext, not set null");
            manager.Current.MustTable[typeof(ShardBucket)].ShouldBe(new HashSet<string> { "0" });
        }

        manager.Current.ShouldBeNull();
    }

    /// <summary>
    /// 读写分离 Cache：不得用单个 _connectionString 缓存跨数据源读连接。
    /// （源码断言；修复后应变为按 dataSource 缓存。）
    /// </summary>
    [Fact]
    public void ReadSeparation_Cache_Must_Not_Reuse_Single_ConnectionString_Field()
    {
        var src = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "BeniceSoft.Abp.EntityFrameworkCore.Sharding",
            "Abstractions",
            "IDataSourceDbContext.cs"));

        src.Contains("_connectionString ??= manager.GetReadNode(dataSource, node);")
            .ShouldBeFalse("Cache strategy must key read connection by dataSource; single-field cache cross-contaminates DS");
    }

    /// <summary>
    /// DataSourceDbContext.Count 若恒为 0，MultipleDb 在同库多表尾时判断不准。
    /// </summary>
    [Fact]
    public void DataSourceDbContext_Count_Must_Be_Updated_When_Creating_Contexts()
    {
        var src = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "BeniceSoft.Abp.EntityFrameworkCore.Sharding",
            "Abstractions",
            "IDataSourceDbContext.cs"));

        // 自动属性 `public int Count { get; }` 从未赋值 → 恒 0
        src.Contains("public int Count { get; }")
            .ShouldBeFalse("Count must be a mutable counter updated on Create/Dispose of physical contexts");
    }

    /// <summary>
    /// InternalExtensions.CreateExpression 不得用 nameof(methodName)（恒为 \"methodName\"）。
    /// </summary>
    [Fact]
    public void MaxMin_Reflection_Must_Use_MethodName_Parameter_Not_Nameof_Parameter()
    {
        var src = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "BeniceSoft.Abp.EntityFrameworkCore.Sharding",
            "InternalExtensions.cs"));

        src.Contains("m.Name == nameof(methodName)")
            .ShouldBeFalse("nameof(methodName) is always \"methodName\"; must compare to methodName variable");
    }

    private async Task SeedLedgersAsync(params ShardLedger[] items)
    {
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        foreach (var item in items)
        {
            await _ledgers.InsertAsync(item, autoSave: true);
        }

        await uow.CompleteAsync();
    }

    private static bool IsDisposed(DbContext ctx)
    {
        try
        {
            _ = ctx.ChangeTracker;
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    private static Exception Unwrap(Exception ex)
    {
        while (ex is AggregateException or TargetInvocationException { InnerException: not null })
        {
            ex = ex.InnerException!;
        }

        return ex;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "BeniceSoft.Abp.EntityFrameworkCore.Sharding");
            if (Directory.Exists(candidate))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Cannot locate BeniceSoft.Abp root from " + AppContext.BaseDirectory);
    }
}
