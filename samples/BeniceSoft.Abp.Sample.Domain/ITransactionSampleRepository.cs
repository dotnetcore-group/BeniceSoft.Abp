namespace BeniceSoft.Abp.Sample.Domain;

public interface ITransactionSampleRepository
{
    Task<TransactionSampleOrder?> FindOrderAsync(string bizId);
    Task<TransactionSampleInventory?> FindInventoryAsync(string bizId);
    Task InsertOrderAsync(TransactionSampleOrder order);
    Task InsertInventoryAsync(TransactionSampleInventory inventory);
}
