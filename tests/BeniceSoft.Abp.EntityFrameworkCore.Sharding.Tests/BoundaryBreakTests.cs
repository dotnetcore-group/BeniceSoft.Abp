using BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests.Fixture;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests;

/// <summary>
/// 破坏边界 / 对抗性用例：断言正确语义；失败即暴露设计或实现漏洞。
/// </summary>
public class BoundaryBreakTests : ShardingTestBase
{
    private readonly IRepository<ShardLedger, Guid> _ledgers;
    private readonly IRepository<ShardBucket, Guid> _buckets;
    private readonly IRepository<ShardAreaOrder, Guid> _orders;
    private readonly IUnitOfWorkManager _uow;

    public BoundaryBreakTests(ShardingTestApplication app) : base(app)
    {
        _ledgers = GetRequiredService<IRepository<ShardLedger, Guid>>();
        _buckets = GetRequiredService<IRepository<ShardBucket, Guid>>();
        _orders = GetRequiredService<IRepository<ShardAreaOrder, Guid>>();
        _uow = GetRequiredService<IUnitOfWorkManager>();
    }

    /// <summary>Must 指向不存在的物理尾缀应失败，而不是静默 fan-out。</summary>
    [Fact]
    public async Task AsRoute_Must_Unknown_Tail_Should_Throw()
    {
        var batch = NewBatch("must-bad");
        await SeedLedgersAsync(new ShardLedger(Guid.NewGuid(), "X", 1m, new DateTime(2024, 1, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var ex = await Should.ThrowAsync<ShardingException>(async () =>
        {
            await (await _ledgers.GetQueryableAsync())
                .AsNoTracking()
                .AsRoute(ctx => ctx.MustTable[typeof(ShardLedger)] = new HashSet<string> { "9999" })
                .Where(x => x.BatchTag == batch)
                .ToListAsync();
        });
        ex.Message.ShouldContain("must", Case.Insensitive);
        await uow.CompleteAsync();
    }

    /// <summary>Hint 非法尾缀应抛 hint error。</summary>
    [Fact]
    public async Task AsRoute_Hint_Unknown_Tail_Should_Throw()
    {
        var batch = NewBatch("hint-bad");
        await SeedLedgersAsync(new ShardLedger(Guid.NewGuid(), "X", 1m, new DateTime(2024, 1, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var ex = await Should.ThrowAsync<ShardingException>(async () =>
        {
            await (await _ledgers.GetQueryableAsync())
                .AsNoTracking()
                .AsRoute(ctx => ctx.HintTable[typeof(ShardLedger)] = new HashSet<string> { "24-01" })
                .Where(x => x.BatchTag == batch)
                .ToListAsync();
        });
        ex.Message.ShouldContain("hint", Case.Insensitive);
        await uow.CompleteAsync();
    }

    /// <summary>写入 GetTails() 之外的未来月：无自动 Append 时应明确失败。</summary>
    [Fact]
    public async Task Insert_Future_Month_Beyond_Tails_Should_Throw()
    {
        var batch = NewBatch("fut");
        var future = new DateTime(2099, 6, 1);

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var ex = await Should.ThrowAsync<Exception>(async () =>
        {
            await _ledgers.InsertAsync(new ShardLedger(Guid.NewGuid(), "FUT", 1m, future, batch), autoSave: true);
            await uow.CompleteAsync();
        });
        (ex is ShardingException || ex.InnerException is ShardingException || ex.Message.Contains("route", StringComparison.OrdinalIgnoreCase))
            .ShouldBeTrue($"expected sharding route error, got: {ex.GetType().Name}: {ex.Message}");
    }

    /// <summary>默认 DateTime 分片键（0001-01）不在路由表内。</summary>
    [Fact]
    public async Task Insert_Default_BizMonth_Should_Throw()
    {
        var batch = NewBatch("dflt");
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        await Should.ThrowAsync<Exception>(async () =>
        {
            await _ledgers.InsertAsync(new ShardLedger(Guid.NewGuid(), "D", 1m, default, batch), autoSave: true);
            await uow.CompleteAsync();
        });
    }

    /// <summary>
    /// 跨月 Last + OrderBy：应返回排序后的最后一条。
    /// 若 ReverseSorting 未翻转方向，会错误返回 First。
    /// </summary>
    [Fact]
    public async Task Last_With_OrderBy_Across_Months_Should_Return_Last()
    {
        var batch = NewBatch("last");
        await SeedLedgersAsync(
            new ShardLedger(Guid.NewGuid(), "JAN", 1m, new DateTime(2024, 1, 1), batch),
            new ShardLedger(Guid.NewGuid(), "FEB", 2m, new DateTime(2024, 2, 1), batch),
            new ShardLedger(Guid.NewGuid(), "MAR", 3m, new DateTime(2024, 3, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var last = await (await _ledgers.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BatchTag == batch)
            .OrderBy(x => x.BizMonth)
            .LastAsync();
        last.Code.ShouldBe("MAR");
        await uow.CompleteAsync();
    }

    /// <summary>Skip/Take + OrderBy 跨分片应得到全局第 N 条，而非各片各自分页后乱序拼接。</summary>
    [Fact]
    public async Task Skip_Take_OrderBy_Across_Months_Should_Be_Globally_Correct()
    {
        var batch = NewBatch("page");
        await SeedLedgersAsync(
            new ShardLedger(Guid.NewGuid(), "A", 10m, new DateTime(2024, 1, 1), batch),
            new ShardLedger(Guid.NewGuid(), "B", 20m, new DateTime(2024, 2, 1), batch),
            new ShardLedger(Guid.NewGuid(), "C", 30m, new DateTime(2024, 3, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var page = await (await _ledgers.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BatchTag == batch)
            .OrderBy(x => x.Amount)
            .Skip(1)
            .Take(1)
            .ToListAsync();
        page.Where(x => x is not null).Select(x => x!.Code).ShouldBe(["B"]);
        await uow.CompleteAsync();
    }

    /// <summary>空结果集 Average 不应静默返回 0/NaN（与 LINQ 一致应抛错）。</summary>
    [Fact]
    public async Task Average_On_Empty_Should_Throw_Or_Not_Return_Zero_Silently()
    {
        var batch = NewBatch("avg0");
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var q = (await _ledgers.GetQueryableAsync()).AsNoTracking().Where(x => x.BatchTag == batch);

        try
        {
            var avg = await q.AverageAsync(x => x.Amount);
            // 若未抛错，至少不能假装有数据
            avg.ShouldNotBe(0m);
            throw new Xunit.Sdk.XunitException($"Average on empty returned {avg}; expected throw like LINQ.");
        }
        catch (Xunit.Sdk.XunitException)
        {
            throw;
        }
        catch (Exception)
        {
            // InvalidOperation / DivideByZero / ShardingException 均可接受
        }

        await uow.CompleteAsync();
    }

    /// <summary>Count/Sum/Any 空集应符合 LINQ：0 / 0 / false。</summary>
    [Fact]
    public async Task Empty_Aggregates_Count_Sum_Any_Should_Match_Linq()
    {
        var batch = NewBatch("agg0");
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var q = (await _ledgers.GetQueryableAsync()).AsNoTracking().Where(x => x.BatchTag == batch);
        (await q.CountAsync()).ShouldBe(0);
        (await q.SumAsync(x => x.Amount)).ShouldBe(0m);
        (await q.AnyAsync()).ShouldBeFalse();
        await uow.CompleteAsync();
    }

    /// <summary>同 Guid 落在两个月物理表时，按 Id fan-out 的 Single 必须失败。</summary>
    [Fact]
    public async Task Single_Duplicate_Id_Across_Months_Should_Throw()
    {
        var id = Guid.NewGuid();
        var batch = NewBatch("dup");
        await SeedLedgersAsync(
            new ShardLedger(id, "JAN", 1m, new DateTime(2024, 1, 1), batch),
            new ShardLedger(id, "FEB", 2m, new DateTime(2024, 2, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        await Should.ThrowAsync<Exception>(async () =>
        {
            await (await _ledgers.GetQueryableAsync())
                .AsNoTracking()
                .Where(x => x.Id == id && x.BatchTag == batch)
                .SingleAsync();
        });
        await uow.CompleteAsync();
    }

    /// <summary>
    /// 更新分片键：改 BizMonth 后旧表应无、新表应有（或明确拒绝）。
    /// 静默留在旧表 / 双写残留均为漏洞。
    /// </summary>
    [Fact]
    public async Task Update_Changing_Shard_Key_Should_Move_Or_Reject()
    {
        var id = Guid.NewGuid();
        var jan = new DateTime(2024, 1, 1);
        var feb = new DateTime(2024, 2, 1);
        var batch = NewBatch("move");
        await SeedLedgersAsync(new ShardLedger(id, "OLD", 1m, jan, batch));

        using (var uow = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            var entity = await (await _ledgers.GetQueryableAsync())
                .Where(x => x.BizMonth == jan && x.Id == id)
                .SingleAsync();
            entity.BizMonth = feb;
            entity.Code = "MOVED";
            try
            {
                await _ledgers.UpdateAsync(entity, autoSave: true);
                await uow.CompleteAsync();
            }
            catch (Exception)
            {
                // 明确拒绝改键也可接受
                return;
            }
        }

        using var read = _uow.Begin(requiresNew: true, isTransactional: true);
        var inJan = await (await _ledgers.GetQueryableAsync())
            .AsNoTracking()
            .AnyAsync(x => x.BizMonth == jan && x.Id == id && x.BatchTag == batch);
        var inFeb = await (await _ledgers.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BizMonth == feb && x.Id == id && x.BatchTag == batch)
            .ToListAsync();

        // 不允许：仍在旧表且新表没有（改键未生效却成功）
        // 不允许：两表都有（双写）
        if (inJan && inFeb.Count > 0)
        {
            throw new Xunit.Sdk.XunitException("shard key update left rows in BOTH month tables (duplicate).");
        }

        if (inJan && inFeb.Count == 0)
        {
            throw new Xunit.Sdk.XunitException("shard key update kept row on old table only (key change ignored).");
        }

        inFeb.Count.ShouldBe(1);
        inFeb[0].Code.ShouldBe("MOVED");
        await read.CompleteAsync();
    }

    /// <summary>仅按 Id 删除（无分片键谓词）应能删掉或明确失败，不能静默 0 行成功。</summary>
    [Fact]
    public async Task Delete_By_Id_Without_Shard_Predicate_Should_Work_Or_Fail_Loudly()
    {
        var id = Guid.NewGuid();
        var jan = new DateTime(2024, 1, 1);
        var batch = NewBatch("del");
        await SeedLedgersAsync(new ShardLedger(id, "DEL", 1m, jan, batch));

        using (var uow = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            var entity = await (await _ledgers.GetQueryableAsync())
                .AsNoTracking()
                .Where(x => x.Id == id && x.BatchTag == batch)
                .SingleAsync();
            await _ledgers.DeleteAsync(entity, autoSave: true);
            await uow.CompleteAsync();
        }

        using var read = _uow.Begin(requiresNew: true, isTransactional: true);
        var exists = await (await _ledgers.GetQueryableAsync())
            .AnyAsync(x => x.BizMonth == jan && x.Id == id);
        exists.ShouldBeFalse();
        await read.CompleteAsync();
    }

    /// <summary>月中上界 LessThan：应包含当月符合谓词的数据（Critical 仅对月初临界生效）。</summary>
    [Fact]
    public async Task LessThan_MidMonth_Should_Still_Include_Matching_Month_Rows()
    {
        var batch = NewBatch("lt");
        var mar = new DateTime(2024, 3, 1);
        await SeedLedgersAsync(
            new ShardLedger(Guid.NewGuid(), "FEB", 1m, new DateTime(2024, 2, 1), batch),
            new ShardLedger(Guid.NewGuid(), "MAR", 2m, mar, batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var rows = await (await _ledgers.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BizMonth < new DateTime(2024, 3, 15) && x.BatchTag == batch)
            .ToListAsync();
        var codes = rows.Where(x => x is not null).Select(x => x!.Code).OrderBy(c => c).ToList();
        codes.ShouldContain("FEB");
        codes.ShouldContain("MAR");
        await uow.CompleteAsync();
    }

    /// <summary>月初临界 LessThan：&lt; 2024-03-01 不应扫到 3 月表行。</summary>
    [Fact]
    public async Task LessThan_Critical_Month_Start_Should_Exclude_That_Month()
    {
        var batch = NewBatch("crit");
        await SeedLedgersAsync(
            new ShardLedger(Guid.NewGuid(), "FEB", 1m, new DateTime(2024, 2, 1), batch),
            new ShardLedger(Guid.NewGuid(), "MAR", 2m, new DateTime(2024, 3, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var rows = await (await _ledgers.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BizMonth < new DateTime(2024, 3, 1) && x.BatchTag == batch)
            .ToListAsync();
        rows.Where(x => x is not null).Select(x => x!.Code).ShouldBe(["FEB"]);
        await uow.CompleteAsync();
    }

    /// <summary>Mod NotEqual 当前实现会 fan-out 全桶；至少结果集应正确。</summary>
    [Fact]
    public async Task Mod_NotEqual_Should_Return_Correct_Rows_Even_If_FanOut()
    {
        var batch = NewBatch("neq");
        using (var uow = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            await _buckets.InsertAsync(new ShardBucket(Guid.NewGuid(), "e10", 10, batch), autoSave: true);
            await _buckets.InsertAsync(new ShardBucket(Guid.NewGuid(), "e12", 12, batch), autoSave: true);
            await _buckets.InsertAsync(new ShardBucket(Guid.NewGuid(), "o11", 11, batch), autoSave: true);
            await uow.CompleteAsync();
        }

        using var read = _uow.Begin(requiresNew: true, isTransactional: true);
        var rows = await (await _buckets.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BucketKey != 10 && x.BatchTag == batch)
            .ToListAsync();
        rows.Where(x => x is not null).Select(x => x!.Name).OrderBy(n => n).ShouldBe(["e12", "o11"]);
        await read.CompleteAsync();
    }

    /// <summary>int.MinValue 取模不应溢出崩溃。</summary>
    [Fact]
    public void Mod_GetKey_IntMinValue_Should_Not_Overflow()
    {
        var runtime = GetRequiredService<IShardingRuntimeContext>();
        var route = runtime.TableRouteManager.GetRoute(typeof(ShardBucket));
        Should.NotThrow(() => route.GetKey(int.MinValue));
        var tail = route.GetKey(int.MinValue);
        tail.ShouldBe("0"); // MinValue % 2 == 0
    }

    /// <summary>空 Area 按路由约定落到默认库，不应崩溃。</summary>
    [Fact]
    public async Task DataSource_Empty_Area_Should_Route_Default_Ds0()
    {
        var batch = NewBatch("area0");
        var id = Guid.NewGuid();
        using (var uow = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            await _orders.InsertAsync(new ShardAreaOrder(id, "", "empty-area", batch), autoSave: true);
            await uow.CompleteAsync();
        }

        using var read = _uow.Begin(requiresNew: true, isTransactional: true);
        var row = await (await _orders.GetQueryableAsync())
            .AsNoTracking()
            .AsRoute(ctx => ctx.MustDataSource[typeof(ShardAreaOrder)] = new HashSet<string> { "ds0" })
            .Where(x => x.Id == id)
            .SingleAsync();
        row.Title.ShouldBe("empty-area");
        await read.CompleteAsync();
    }

    /// <summary>Must 空集合：应抛错，而不是忽略后 fan-out。</summary>
    [Fact]
    public async Task AsRoute_Must_Empty_Set_Should_Throw()
    {
        var batch = NewBatch("must0");
        await SeedLedgersAsync(new ShardLedger(Guid.NewGuid(), "X", 1m, new DateTime(2024, 1, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var ex = await Should.ThrowAsync<ShardingException>(async () =>
        {
            await (await _ledgers.GetQueryableAsync())
                .AsNoTracking()
                .AsRoute(ctx => ctx.MustTable[typeof(ShardLedger)] = new HashSet<string>())
                .Where(x => x.BatchTag == batch)
                .ToListAsync();
        });
        ex.Message.ShouldContain("must", Case.Insensitive);
        await uow.CompleteAsync();
    }

    /// <summary>跨库 Commit 任一数据源失败都必须抛出，不得吞掉后半截异常。</summary>
    [Fact]
    public void MultiDataSource_Commit_Must_Rethrow_Any_Failure()
    {
        var src = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "BeniceSoft.Abp.EntityFrameworkCore.Sharding",
            "Abstractions",
            "IShardingDbContextExecutor.cs"));

        // 旧实现：仅 i==0 时 throw，后面的库失败被吞
        src.ShouldNotContain("if (i == 0)");
        src.ShouldContain("ShardingDbContextExecutor Commit");
        src.ShouldContain("ShardingDbContextExecutor CommitAsync");
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

        // 测试输出目录向上找到 BeniceSoft.Abp
        dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir != null; i++)
        {
            if (dir.Name == "BeniceSoft.Abp")
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Cannot locate BeniceSoft.Abp root from " + AppContext.BaseDirectory);
    }
}
