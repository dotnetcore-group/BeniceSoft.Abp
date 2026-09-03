using System.Diagnostics;
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
using Xunit.Abstractions;

namespace BeniceSoft.Abp.Sample.Tests;

/// <summary>
/// 真实 PG 库上的 Bulk 性能压测：内存构造 N 行 → BulkInsert → BulkUpdate。
/// 默认 20 万行；本地调试可改 <see cref="RowCount"/>。
/// 表结构依赖 PgBulkIntegrationTestModule 初始化时的 Migration + Compensate，测试内不自建表。
/// </summary>
public class PgBulkPerfTests : AbpIntegratedTest<PgBulkIntegrationTestModule>
{
    /// <summary>压测结束后是否删除本批数据。设为 false 便于在库里人工核对。</summary>
    public static bool CleanupAfter = false;

    public const int RowCount = 200_000;

    private readonly IDbContextProvider<SampleDbContext> _dbContextProvider;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ITestOutputHelper _output;

    public PgBulkPerfTests(ITestOutputHelper output)
    {
        _output = output;
        _dbContextProvider = GetRequiredService<IDbContextProvider<SampleDbContext>>();
        _unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
    }

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    [Fact]
    public async Task Bulk_Insert_Then_Update_200k_Should_Report_Timing()
    {
        var batchTag = $"perf-{Guid.NewGuid():N}"[..20];
        _output.WriteLine($"BatchTag={batchTag}, RowCount={RowCount:N0}");

        // ① 内存造数
        var genSw = Stopwatch.StartNew();
        var items = new List<BulkDemoItem>(RowCount);
        for (var i = 0; i < RowCount; i++)
        {
            items.Add(new BulkDemoItem(
                Guid.NewGuid(),
                $"{batchTag}-{i:D6}",
                $"n-{i}",
                i,
                batchTag));
        }

        genSw.Stop();
        _output.WriteLine($"Memory generate: {genSw.ElapsedMilliseconds:N0} ms ({RowsPerSec(RowCount, genSw):N0} rows/s)");

        try
        {
            // ② BulkInsert
            var insertSw = Stopwatch.StartNew();
            int inserted;
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                var db = await _dbContextProvider.GetDbContextAsync();
                inserted = await db.BulkInsertAsync(items, atom => atom.WithCommandTimeout(600).WithBulkCopyTimeout(600));
                await uow.CompleteAsync();
            }

            insertSw.Stop();
            inserted.ShouldBe(RowCount);
            _output.WriteLine($"BulkInsert: {insertSw.ElapsedMilliseconds:N0} ms ({RowsPerSec(RowCount, insertSw):N0} rows/s), affected={inserted:N0}");

            // ③ 内存改字段后 BulkUpdate
            foreach (var item in items)
            {
                item.Quantity = item.Quantity + 1;
                item.Name = $"u-{item.Quantity}";
                item.Version++;
            }

            var updateSw = Stopwatch.StartNew();
            int updated;
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                var db = await _dbContextProvider.GetDbContextAsync();
                updated = await db.BulkUpdateAsync(items, atom => atom.WithCommandTimeout(600).WithBulkCopyTimeout(600));
                await uow.CompleteAsync();
            }

            updateSw.Stop();
            updated.ShouldBe(RowCount);
            _output.WriteLine($"BulkUpdate: {updateSw.ElapsedMilliseconds:N0} ms ({RowsPerSec(RowCount, updateSw):N0} rows/s), affected={updated:N0}");

            // 抽样校验
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                var db = await _dbContextProvider.GetDbContextAsync();
                var count = await db.BulkDemoItems.CountAsync(x => x.BatchTag == batchTag);
                count.ShouldBe(RowCount);

                var sample = await db.BulkDemoItems.AsNoTracking()
                    .Where(x => x.Code == $"{batchTag}-000000")
                    .SingleAsync();
                sample.Quantity.ShouldBe(1);
                sample.Version.ShouldBe(2);
                await uow.CompleteAsync();
            }

            _output.WriteLine(
                $"Summary: Insert={insertSw.Elapsed.TotalSeconds:F2}s, Update={updateSw.Elapsed.TotalSeconds:F2}s, Total={insertSw.Elapsed.TotalSeconds + updateSw.Elapsed.TotalSeconds:F2}s");
        }
        finally
        {
            if (CleanupAfter)
            {
                var cleanSw = Stopwatch.StartNew();
                using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
                {
                    var db = await _dbContextProvider.GetDbContextAsync();
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"DELETE FROM bulk_demo_items WHERE \"BatchTag\" = {batchTag}");
                    await uow.CompleteAsync();
                }

                cleanSw.Stop();
                _output.WriteLine($"Cleanup DELETE: {cleanSw.ElapsedMilliseconds:N0} ms");
            }
            else
            {
                _output.WriteLine($"KeepData=true, rows left in bulk_demo_items WHERE \"BatchTag\" = '{batchTag}'");
            }
        }
    }

    private static double RowsPerSec(int rows, Stopwatch sw)
        => sw.Elapsed.TotalSeconds <= 0 ? rows : rows / sw.Elapsed.TotalSeconds;
}
