using BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests.Fixture;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests;

/// <summary>
/// 分库：Area → ds0/ds1。
/// 同 UoW 跨库写入会走 MultipleDb 自动事务；跨物理库事务传播依赖驱动，此处按库分 UoW 验证路由正确性。
/// </summary>
public class DataSourceShardingTests : ShardingTestBase
{
    private readonly IRepository<ShardAreaOrder, Guid> _repo;
    private readonly IUnitOfWorkManager _uow;

    public DataSourceShardingTests(ShardingTestApplication app) : base(app)
    {
        _repo = GetRequiredService<IRepository<ShardAreaOrder, Guid>>();
        _uow = GetRequiredService<IUnitOfWorkManager>();
    }

    [Fact]
    public async Task AutoRoute_Area_Should_Write_To_Different_DataSources()
    {
        var batch = NewBatch("ds");
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();

        await InsertAsync(new ShardAreaOrder(aId, "A", "east", batch));
        await InsertAsync(new ShardAreaOrder(bId, "B", "west", batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var a = await (await _repo.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.Area == "A" && x.BatchTag == batch)
            .SingleAsync();
        a.Id.ShouldBe(aId);

        var b = await (await _repo.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.Area == "B" && x.BatchTag == batch)
            .SingleAsync();
        b.Id.ShouldBe(bId);
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task AsRoute_MustDataSource_Should_Pin_Ds()
    {
        var id = Guid.NewGuid();
        var batch = NewBatch("ds-must");
        await InsertAsync(new ShardAreaOrder(id, "A", "pin", batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var wrong = await (await _repo.GetQueryableAsync())
            .AsNoTracking()
            .AsRoute(ctx => ctx.MustDataSource[typeof(ShardAreaOrder)] = new HashSet<string> { "ds1" })
            .Where(x => x.Id == id)
            .ToListAsync();
        wrong.Where(x => x is not null).ShouldBeEmpty();

        var ok = await (await _repo.GetQueryableAsync())
            .AsNoTracking()
            .AsRoute(ctx => ctx.MustDataSource[typeof(ShardAreaOrder)] = new HashSet<string> { "ds0" })
            .Where(x => x.Id == id)
            .ToListAsync();
        ok.Where(x => x is not null).Count().ShouldBe(1);
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task FanOut_Across_DataSources_Without_Area_Predicate()
    {
        var batch = NewBatch("ds-fan");
        await InsertAsync(new ShardAreaOrder(Guid.NewGuid(), "A", "a", batch));
        await InsertAsync(new ShardAreaOrder(Guid.NewGuid(), "B", "b", batch));

        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        var all = await (await _repo.GetQueryableAsync())
            .AsNoTracking()
            .Where(x => x.BatchTag == batch)
            .ToListAsync();
        all.Where(x => x is not null).Count().ShouldBe(2);
        await uow.CompleteAsync();
    }

    [Fact]
    public async Task GetKey_Should_Map_Area_To_DataSource_Name()
    {
        var runtime = GetRequiredService<IShardingRuntimeContext>();
        var route = runtime.DataSourceRouteManager.GetRoute(typeof(ShardAreaOrder));
        route.GetKey("A-east").ShouldBe("ds0");
        route.GetKey("B-west").ShouldBe("ds1");
    }

    private async Task InsertAsync(ShardAreaOrder order)
    {
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);
        await _repo.InsertAsync(order, autoSave: true);
        await uow.CompleteAsync();
    }
}
