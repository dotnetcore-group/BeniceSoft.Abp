using BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests.Fixture;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests;

/// <summary>取模分表（ModInt）。</summary>
public class ModTableShardingTests : ShardingTestBase
{
    private readonly IRepository<ShardBucket, Guid> _repo;
    private readonly IUnitOfWorkManager _uow;

    public ModTableShardingTests(ShardingTestApplication app) : base(app)
    {
        _repo = GetRequiredService<IRepository<ShardBucket, Guid>>();
        _uow = GetRequiredService<IUnitOfWorkManager>();
    }

    [Fact]
    public async Task AutoRoute_By_BucketKey_Should_Split_Even_Odd()
    {
        var batch = NewBatch("mod");
        var evenId = Guid.NewGuid();
        var oddId = Guid.NewGuid();

        using (var uow = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            await _repo.InsertAsync(new ShardBucket(evenId, "even", 10, batch), autoSave: true);
            await _repo.InsertAsync(new ShardBucket(oddId, "odd", 11, batch), autoSave: true);
            await uow.CompleteAsync();
        }

        using (var uow = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            var even = await (await _repo.GetQueryableAsync())
                .AsNoTracking()
                .Where(x => x.BucketKey == 10 && x.BatchTag == batch)
                .SingleAsync();
            even.Id.ShouldBe(evenId);

            var odd = await (await _repo.GetQueryableAsync())
                .AsNoTracking()
                .Where(x => x.BucketKey == 11 && x.BatchTag == batch)
                .SingleAsync();
            odd.Id.ShouldBe(oddId);
            await uow.CompleteAsync();
        }
    }

    [Fact]
    public async Task AsRoute_Must_Mod_Tail_Should_Pin()
    {
        var id = Guid.NewGuid();
        var batch = NewBatch("mod-must");

        using (var uow = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            await _repo.InsertAsync(new ShardBucket(id, "pin", 20, batch), autoSave: true); // 20 % 2 = 0
            await uow.CompleteAsync();
        }

        using (var uow = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            var wrong = await (await _repo.GetQueryableAsync())
                .AsNoTracking()
                .AsRoute(ctx => ctx.MustTable[typeof(ShardBucket)] = new HashSet<string> { "1" })
                .Where(x => x.Id == id)
                .ToListAsync();
            wrong.ShouldBeEmpty();

            var ok = await (await _repo.GetQueryableAsync())
                .AsNoTracking()
                .AsRoute(ctx => ctx.MustTable[typeof(ShardBucket)] = new HashSet<string> { "0" })
                .Where(x => x.Id == id)
                .ToListAsync();
            ok.Count.ShouldBe(1);
            await uow.CompleteAsync();
        }
    }

    [Fact]
    public async Task FanOut_Merge_Without_Bucket_Predicate()
    {
        var batch = NewBatch("mod-fan");
        using (var uow = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            await _repo.InsertAsync(new ShardBucket(Guid.NewGuid(), "a", 2, batch), autoSave: true);
            await _repo.InsertAsync(new ShardBucket(Guid.NewGuid(), "b", 3, batch), autoSave: true);
            await uow.CompleteAsync();
        }

        using (var uow = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            var all = await (await _repo.GetQueryableAsync())
                .AsNoTracking()
                .Where(x => x.BatchTag == batch)
                .ToListAsync();
            all.Where(x => x is not null).Count().ShouldBe(2);
            await uow.CompleteAsync();
        }
    }
}
