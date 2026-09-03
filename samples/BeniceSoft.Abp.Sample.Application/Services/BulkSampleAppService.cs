using BeniceSoft.Abp.EntityFrameworkCore;
using BeniceSoft.Abp.EntityFrameworkCore.PostgreSql;
using BeniceSoft.Abp.Sample.Application.Contracts;
using BeniceSoft.Abp.Sample.Domain;
using BeniceSoft.Abp.Sample.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.Sample.Application.Services;

/// <summary>
/// Bulk / Sequence / Hint / ForceSave 
/// </summary>
[AllowAnonymous]
public class BulkSampleAppService : SampleAppServiceBase, IBulkSampleAppService
{
    private readonly IDbContextProvider<SampleDbContext> _dbContextProvider;

    public BulkSampleAppService(IDbContextProvider<SampleDbContext> dbContextProvider)
    {
        _dbContextProvider = dbContextProvider;
    }

    [UnitOfWork]
    public virtual async Task<BulkDemoResultDto> BulkInsertAsync(int count = 20)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        var batchTag = $"ins-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        var items = CreateItems(count, batchTag);

        var affected = await db.BulkInsertAsync(items);
        return await BuildResultAsync(db, "BulkInsert", batchTag, affected);
    }

    [UnitOfWork]
    public virtual async Task<BulkDemoResultDto> BulkUpdateAsync(string batchTag, int quantity = 100)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        var items = await db.BulkDemoItems.AsNoTracking()
            .Where(x => x.BatchTag == batchTag)
            .ToListAsync();

        foreach (var item in items)
        {
            item.Quantity = quantity;
            item.Name = $"{item.Code}-updated";
            item.Version += 1;
        }

        var affected = await db.BulkUpdateAsync(items);
        return await BuildResultAsync(db, "BulkUpdate", batchTag, affected);
    }

    [UnitOfWork]
    public virtual async Task<BulkDemoResultDto> BulkMergeAsync(string? batchTag = null, int count = 10)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        batchTag ??= $"merge-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";

        var existing = await db.BulkDemoItems.AsNoTracking()
            .Where(x => x.BatchTag == batchTag)
            .OrderBy(x => x.Code)
            .Take(count / 2)
            .ToListAsync();

        var items = new List<BulkDemoItem>();
        foreach (var item in existing)
        {
            item.Quantity += 1;
            item.Name = $"{item.Code}-merged";
            item.Version += 1;
            items.Add(item);
        }

        var needNew = count - items.Count;
        if (needNew > 0)
        {
            items.AddRange(CreateItems(needNew, batchTag, startIndex: items.Count));
        }

        var affected = await db.BulkMergeAsync(items, matchBuilder: m => m.MatchTargetOn(x => x.Id));
        return await BuildResultAsync(db, "BulkMerge", batchTag, affected);
    }

    [UnitOfWork]
    public virtual async Task<BulkDemoResultDto> BulkDeleteAsync(string batchTag)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        var items = await db.BulkDemoItems.AsNoTracking()
            .Where(x => x.BatchTag == batchTag)
            .ToListAsync();

        var affected = items.Count == 0 ? 0 : await db.BulkDeleteAsync(items);
        return await BuildResultAsync(db, "BulkDelete", batchTag, affected);
    }

    [UnitOfWork]
    public virtual async Task<BulkDemoResultDto> BulkOperationAsync(int count = 10)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        var batchTag = $"op-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        var items = CreateItems(count, batchTag);

        await using var op = db.BulkOperation();
        var inserted = await op.BulkInsertAsync(items);
        foreach (var item in items)
        {
            item.Quantity = 999;
            item.Version += 1;
        }

        var updated = await op.BulkUpdateAsync(items);
        await op.CommitAsync();

        var result = await BuildResultAsync(db, "BulkOperation", batchTag, inserted + updated);
        result.Affected = inserted + updated;
        return result;
    }

    [UnitOfWork]
    public virtual async Task<BulkDemoResultDto> GetSequencesAsync(int count = 5)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        var sequences = await db.Database.GetSequenceAsync<long>("bulk_demo_seq", count);
        return new BulkDemoResultDto
        {
            Operation = "GetSequence",
            Affected = sequences.Length,
            Sequences = sequences
        };
    }

    [UnitOfWork]
    public virtual async Task<BulkDemoResultDto> ForUpdateQueryAsync(string batchTag)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        var items = await db.BulkDemoItems
            .Where(x => x.BatchTag == batchTag)
            .ForUpdate()
            .ToListAsync();

        return new BulkDemoResultDto
        {
            Operation = "ForUpdate",
            BatchTag = batchTag,
            Affected = items.Count,
            TotalInBatch = items.Count,
            Items = items.Select(ToDto).ToList()
        };
    }

    [UnitOfWork]
    public virtual async Task<BulkDemoResultDto> ForceSaveAsync(string batchTag)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        var item = await db.BulkDemoItems.FirstOrDefaultAsync(x => x.BatchTag == batchTag)
                   ?? throw new InvalidOperationException($"No BulkDemoItem found for batchTag={batchTag}. Call BulkInsert first.");

        // 模拟外部已更�?Version，制造并发冲�?
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE bulk_demo_items SET \"Version\" = \"Version\" + 1 WHERE \"Id\" = {item.Id}");

        item.Name = $"{item.Code}-force-saved";
        item.Quantity += 1;

        var affected = await db.ForceSaveChangeAsync(retryCount: 3);
        return await BuildResultAsync(db, "ForceSaveChange", batchTag, affected);
    }

    private static List<BulkDemoItem> CreateItems(int count, string batchTag, int startIndex = 0)
    {
        var list = new List<BulkDemoItem>(count);
        for (var i = 0; i < count; i++)
        {
            var index = startIndex + i;
            var id = Guid.NewGuid();
            list.Add(new BulkDemoItem(
                id,
                code: $"{batchTag}-{index:D4}",
                name: $"item-{index}",
                quantity: index + 1,
                batchTag: batchTag));
        }

        return list;
    }

    private static async Task<BulkDemoResultDto> BuildResultAsync(SampleDbContext db, string operation, string batchTag, int affected)
    {
        var items = await db.BulkDemoItems.AsNoTracking()
            .Where(x => x.BatchTag == batchTag)
            .OrderBy(x => x.Code)
            .ToListAsync();

        return new BulkDemoResultDto
        {
            Operation = operation,
            BatchTag = batchTag,
            Affected = affected,
            TotalInBatch = items.Count,
            Items = items.Select(ToDto).ToList()
        };
    }

    private static BulkDemoItemDto ToDto(BulkDemoItem x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        Quantity = x.Quantity,
        BatchTag = x.BatchTag,
        Version = x.Version
    };
}
