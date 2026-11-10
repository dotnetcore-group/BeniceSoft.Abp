using BeniceSoft.Abp.Sample.Domain;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace BeniceSoft.Abp.Sample.EntityFrameworkCore;

public class TransactionSampleRepository : ITransactionSampleRepository, ITransientDependency
{
    private readonly IDbContextProvider<SampleDbContext> _dbContextProvider;

    public TransactionSampleRepository(IDbContextProvider<SampleDbContext> dbContextProvider)
    {
        _dbContextProvider = dbContextProvider;
    }

    public async Task<TransactionSampleOrder?> FindOrderAsync(string bizId)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        return await db.TransactionSampleOrders.SingleOrDefaultAsync(x => x.BizId == bizId);
    }

    public async Task<TransactionSampleInventory?> FindInventoryAsync(string bizId)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        return await db.TransactionSampleInventories.SingleOrDefaultAsync(x => x.BizId == bizId);
    }

    public async Task InsertOrderAsync(TransactionSampleOrder order)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        await db.TransactionSampleOrders.AddAsync(order);
    }

    public async Task InsertInventoryAsync(TransactionSampleInventory inventory)
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        await db.TransactionSampleInventories.AddAsync(inventory);
    }
}
