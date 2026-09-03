using System.Data.Common;
using BeniceSoft.Abp.EntityFrameworkCore;
using BeniceSoft.Abp.EntityFrameworkCore.PostgreSql;
using BeniceSoft.Abp.Sample.Domain;
using BeniceSoft.Abp.Sample.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Testing;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.Sample.Tests;

/// <summary>
/// QueryFuture：多查询合并为一次往返（真实 PG）。
/// 表结构依赖 PgBulkIntegrationTestModule 初始化时的 Migration + Compensate，测试内不自建表。
/// </summary>
public class PgQueryFutureTests : AbpIntegratedTest<PgBulkIntegrationTestModule>
{
    private readonly IDbContextProvider<SampleDbContext> _dbContextProvider;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public PgQueryFutureTests()
    {
        _dbContextProvider = GetRequiredService<IDbContextProvider<SampleDbContext>>();
        _unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
    }

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    [Fact]
    public async Task Future_Multiple_Queries_Should_Batch_Into_One_Command()
    {
        var batchTag = $"fut-{Guid.NewGuid():N}"[..20];

        var batchCommands = new List<string>();
        var previousExecuting = QueryFutureManager.OnBatchExecuting;
        QueryFutureManager.OnBatchExecuting = cmd => batchCommands.Add(cmd.CommandText);

        try
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var db = await _dbContextProvider.GetDbContextAsync();

            var items = Enumerable.Range(0, 5).Select(i => new BulkDemoItem(
                Guid.NewGuid(),
                $"{batchTag}-{i}",
                $"n-{i}",
                i + 1,
                batchTag)).ToList();
            await db.BulkInsertAsync(items);

            var futureList = db.BulkDemoItems.AsNoTracking()
                .Where(x => x.BatchTag == batchTag)
                .OrderBy(x => x.Code)
                .Future();

            var futureQtys = db.BulkDemoItems.AsNoTracking()
                .Where(x => x.BatchTag == batchTag)
                .Select(x => x.Quantity)
                .Future();

            var list = await futureList.ToListAsync();
            var qtys = await futureQtys.ToListAsync();

            list.Count.ShouldBe(5);
            qtys.Count.ShouldBe(5);
            qtys.Sum().ShouldBe(15);

            batchCommands.Count.ShouldBe(1);
            batchCommands[0].ShouldContain("BeniceSoft Query Future: 1 of 2");
            batchCommands[0].ShouldContain("BeniceSoft Query Future: 2 of 2");

            await db.BulkDeleteAsync(list);
            await uow.CompleteAsync();
        }
        finally
        {
            QueryFutureManager.OnBatchExecuting = previousExecuting;
        }
    }

    [Fact]
    public async Task Future_AllowQueryBatch_False_Should_Still_Return_Correct_Results()
    {
        var batchTag = $"fnb-{Guid.NewGuid():N}"[..20];
        var previous = QueryFutureManager.AllowQueryBatch;
        QueryFutureManager.AllowQueryBatch = false;

        try
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var db = await _dbContextProvider.GetDbContextAsync();

            await db.BulkInsertAsync([
                new BulkDemoItem(Guid.NewGuid(), $"{batchTag}-1", "a", 1, batchTag),
                new BulkDemoItem(Guid.NewGuid(), $"{batchTag}-2", "b", 2, batchTag)
            ]);

            var f1 = db.BulkDemoItems.AsNoTracking().Where(x => x.BatchTag == batchTag).Future();
            var f2 = db.BulkDemoItems.AsNoTracking().Where(x => x.BatchTag == batchTag && x.Quantity == 2).Future();

            var all = await f1.ToListAsync();
            var one = await f2.ToListAsync();

            all.Count.ShouldBe(2);
            one.Count.ShouldBe(1);

            await db.BulkDeleteAsync(all);
            await uow.CompleteAsync();
        }
        finally
        {
            QueryFutureManager.AllowQueryBatch = previous;
        }
    }

    [Fact]
    public async Task FutureValue_With_Future_Should_Batch()
    {
        var batchTag = $"fv-{Guid.NewGuid():N}"[..20];

        var batchCommands = new List<string>();
        var previousExecuting = QueryFutureManager.OnBatchExecuting;
        QueryFutureManager.OnBatchExecuting = cmd => batchCommands.Add(cmd.CommandText);

        try
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var db = await _dbContextProvider.GetDbContextAsync();

            await db.BulkInsertAsync([
                new BulkDemoItem(Guid.NewGuid(), $"{batchTag}-1", "a", 10, batchTag),
                new BulkDemoItem(Guid.NewGuid(), $"{batchTag}-2", "b", 20, batchTag)
            ]);

            var futureList = db.BulkDemoItems.AsNoTracking().Where(x => x.BatchTag == batchTag).Future();
            var futureMax = db.BulkDemoItems.AsNoTracking()
                .Where(x => x.BatchTag == batchTag)
                .Select(x => (int?)x.Quantity)
                .OrderByDescending(x => x)
                .Take(1)
                .FutureValue();

            var list = await futureList.ToListAsync();
            var max = await futureMax.ValueAsync();

            list.Count.ShouldBe(2);
            max.ShouldBe(20);
            batchCommands.Count.ShouldBe(1);

            await db.BulkDeleteAsync(list);
            await uow.CompleteAsync();
        }
        finally
        {
            QueryFutureManager.OnBatchExecuting = previousExecuting;
        }
    }
}
