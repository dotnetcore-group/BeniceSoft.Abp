using BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests.Fixture;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests;

/// <summary>按月分表：自动路由、范围、跨表合并、AsRoute、聚合、CRUD、UseMerge/AsSequence。</summary>
public class MonthTableShardingTests : ShardingTestBase
{
    private readonly IRepository<ShardLedger, Guid> _repo;
    private readonly IUnitOfWorkManager _uow;

    public MonthTableShardingTests(ShardingTestApplication app) : base(app)
    {
        _repo = GetRequiredService<IRepository<ShardLedger, Guid>>();
        _uow = GetRequiredService<IUnitOfWorkManager>();
    }

    [Fact]
    public async Task AutoRoute_Equal_Should_Hit_Single_Month()
    {
        var jan = new DateTime(2024, 1, 1);
        var feb = new DateTime(2024, 2, 1);
        var batch = NewBatch("eq");

        await SeedAsync(
            new ShardLedger(Guid.NewGuid(), "JAN", 10m, jan, batch),
            new ShardLedger(Guid.NewGuid(), "FEB", 20m, feb, batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var rows = await (await _repo.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BizMonth == jan && x.BatchTag == batch)
            .ToListAsync();
        rows.Count.ShouldBe(1);
        rows[0].Code.ShouldBe("JAN");
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task AutoRoute_Range_Should_Merge_Adjacent_Months()
    {
        var jan = new DateTime(2024, 1, 1);
        var feb = new DateTime(2024, 2, 1);
        var mar = new DateTime(2024, 3, 1);
        var batch = NewBatch("rng");

        await SeedAsync(
            new ShardLedger(Guid.NewGuid(), "JAN", 1m, jan, batch),
            new ShardLedger(Guid.NewGuid(), "FEB", 2m, feb, batch),
            new ShardLedger(Guid.NewGuid(), "MAR", 3m, mar, batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var rows = await (await _repo.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BizMonth >= jan && x.BizMonth < mar && x.BatchTag == batch)
            .OrderBy(x => x.BizMonth)
            .ToListAsync();
        rows.Count.ShouldBeGreaterThanOrEqualTo(2);
        var codes = rows.Where(x => x is not null).Select(x => x!.Code).OrderBy(c => c).ToList();
        codes.ShouldBe(["FEB", "JAN"]);
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task No_Shard_Predicate_Should_FanOut_And_Merge()
    {
        var batch = NewBatch("fan");
        await SeedAsync(
            new ShardLedger(Guid.NewGuid(), "A", 1m, new DateTime(2024, 1, 1), batch),
            new ShardLedger(Guid.NewGuid(), "B", 2m, new DateTime(2024, 2, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var rows = await (await _repo.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BatchTag == batch)
            .ToListAsync();
        rows.Where(x => x is not null).Count().ShouldBe(2);
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task AsRoute_Must_Should_Force_Physical_Tail()
    {
        var id = Guid.NewGuid();
        var batch = NewBatch("must");
        await SeedAsync(new ShardLedger(id, "PIN", 1m, new DateTime(2024, 1, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var wrong = await (await _repo.GetQueryableAsync())
            .AsNoTracking()
            .AsRoute(ctx => ctx.MustTable[typeof(ShardLedger)] = new HashSet<string> { "202402" })
            .Where(x => x.Id == id)
            .ToListAsync();
        wrong.ShouldBeEmpty();

        var ok = await (await _repo.GetQueryableAsync())
            .AsNoTracking()
            .AsRoute(ctx => ctx.MustTable[typeof(ShardLedger)] = new HashSet<string> { "202401" })
            .Where(x => x.Id == id)
            .ToListAsync();
        ok.Count.ShouldBe(1);
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task AsRoute_Hint_Should_Prefer_Tails()
    {
        var id = Guid.NewGuid();
        var batch = NewBatch("hint");
        await SeedAsync(new ShardLedger(id, "H", 1m, new DateTime(2024, 1, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var rows = await (await _repo.GetQueryableAsync())
            .AsNoTracking()
            .AsRoute(ctx => ctx.HintTable[typeof(ShardLedger)] = new HashSet<string> { "202401" })
            .Where(x => x.Id == id)
            .ToListAsync();
        rows.Count.ShouldBe(1);
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task Aggregates_Count_Sum_Any_First_Across_Shards()
    {
        var batch = NewBatch("agg");
        await SeedAsync(
            new ShardLedger(Guid.NewGuid(), "A", 10m, new DateTime(2024, 1, 1), batch),
            new ShardLedger(Guid.NewGuid(), "B", 30m, new DateTime(2024, 2, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var q = (await _repo.GetQueryableAsync()).AsNoTracking().Where(x => x.BatchTag == batch);

        (await q.CountAsync()).ShouldBe(2);
        (await q.SumAsync(x => x.Amount)).ShouldBe(40m);
        (await q.AnyAsync(x => x.Code == "B")).ShouldBeTrue();

        var list = await q.ToListAsync();
        list.Where(x => x is not null).Select(x => x!.Code).OrderBy(c => c).First().ShouldBe("A");
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task Repository_UoW_Update_And_Delete()
    {
        var id = Guid.NewGuid();
        var jan = new DateTime(2024, 1, 1);
        var batch = NewBatch("crud");
        await SeedAsync(new ShardLedger(id, "OLD", 1m, jan, batch));

        using (var uow = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            var entity = await (await _repo.GetQueryableAsync())
                .Where(x => x.BizMonth == jan && x.Id == id)
                .SingleAsync();
            entity.Code = "NEW";
            entity.Amount = 99m;
            await _repo.UpdateAsync(entity, autoSave: true);
            await uow.CompleteAsync();
        }

        using (var uow = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            var updated = await (await _repo.GetQueryableAsync())
                .AsNoTracking()
                .Where(x => x.BizMonth == jan && x.Id == id)
                .SingleAsync();
            updated.Code.ShouldBe("NEW");
            updated.Amount.ShouldBe(99m);

            await _repo.DeleteAsync(updated, autoSave: true);
            await uow.CompleteAsync();
        }

        using (var uow = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            var exists = await (await _repo.GetQueryableAsync())
                .AnyAsync(x => x.BizMonth == jan && x.Id == id);
            exists.ShouldBeFalse();
            await uow.CompleteAsync();
        }
    }

    [Fact]
    public async Task AsSequence_And_AsNoSequence_Should_Query_Successfully()
    {
        var batch = NewBatch("seq");
        await SeedAsync(
            new ShardLedger(Guid.NewGuid(), "S1", 1m, new DateTime(2024, 1, 1), batch),
            new ShardLedger(Guid.NewGuid(), "S2", 2m, new DateTime(2024, 2, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var sequenced = await (await _repo.GetQueryableAsync())
            .AsNoTracking()
            .AsSequence(sameComparer: true)
            .Where(x => x.BatchTag == batch)
            .ToListAsync();
        sequenced.Where(x => x is not null).Count().ShouldBe(2);

        var noSeq = await (await _repo.GetQueryableAsync())
            .AsNoTracking()
            .AsNoSequence()
            .Where(x => x.BatchTag == batch)
            .ToListAsync();
        noSeq.Where(x => x is not null).Count().ShouldBe(2);
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task UseMerge_Without_MergeCompiler_Should_Throw_Guidance()
    {
        var batch = NewBatch("merge");
        await SeedAsync(new ShardLedger(Guid.NewGuid(), "M", 1m, new DateTime(2024, 1, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var ex = await Should.ThrowAsync<ShardingException>(async () =>
        {
            await (await _repo.GetQueryableAsync())
                .AsNoTracking()
                .UseMerge()
                .Where(x => x.BatchTag == batch)
                .ToListAsync();
        });
        ex.Message.ShouldContain("UseMerge");
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task AsConnection_Should_Limit_Parallel_Connections()
    {
        var batch = NewBatch("conn");
        await SeedAsync(
            new ShardLedger(Guid.NewGuid(), "C1", 1m, new DateTime(2024, 1, 1), batch),
            new ShardLedger(Guid.NewGuid(), "C2", 2m, new DateTime(2024, 2, 1), batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var rows = await (await _repo.GetQueryableAsync())
            .AsNoTracking()
            .AsConnection(limit: 1)
            .Where(x => x.BatchTag == batch)
            .ToListAsync();
        rows.Where(x => x is not null).Count().ShouldBe(2);
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task Multi_Tail_SaveChanges_In_One_UoW()
    {
        var batch = NewBatch("tx");
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        await _repo.InsertAsync(new ShardLedger(Guid.NewGuid(), "T1", 1m, new DateTime(2024, 1, 1), batch), autoSave: false);
        await _repo.InsertAsync(new ShardLedger(Guid.NewGuid(), "T2", 2m, new DateTime(2024, 2, 1), batch), autoSave: false);
        await uow.CompleteAsync();

        using var read = _uow.Begin(requiresNew: true, isTransactional: true);
        var count = await (await _repo.GetQueryableAsync()).CountAsync(x => x.BatchTag == batch);
        count.ShouldBe(2);
        await read.CompleteAsync();
    }

    private async Task SeedAsync(params ShardLedger[] items)
    {
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        foreach (var item in items)
        {
            await _repo.InsertAsync(item, autoSave: true);
        }

        await uow.CompleteAsync();
    }
}
