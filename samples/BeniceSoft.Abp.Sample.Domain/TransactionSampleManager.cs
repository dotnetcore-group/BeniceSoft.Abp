using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace BeniceSoft.Abp.Sample.Domain;

/// <summary>
/// 管理分布式事务示例订单和库存
/// </summary>
public class TransactionSampleManager : DomainService
{
    private readonly ITransactionSampleRepository _repository;

    public TransactionSampleManager(ITransactionSampleRepository repository)
    {
        _repository = repository;
    }

    public async Task PrepareOrderAsync(string bizId, string productCode, int quantity)
    {
        var order = await _repository.FindOrderAsync(bizId);
        if (order is null)
        {
            order = new TransactionSampleOrder(bizId, productCode, quantity);
            order.Prepare();
            await _repository.InsertOrderAsync(order);
            return;
        }

        order.AssertRequest(productCode, quantity);
        order.Prepare();
    }

    public async Task ConfirmOrderAsync(string bizId, string productCode, int quantity)
    {
        var order = await _repository.FindOrderAsync(bizId)
                    ?? throw new BusinessException("Sample.Transaction.OrderMissing", "待确认的示例订单不存在");
        order.AssertRequest(productCode, quantity);
        order.Confirm();
    }

    public async Task CancelOrderAsync(string bizId, string productCode, int quantity)
    {
        var order = await _repository.FindOrderAsync(bizId);
        if (order is null)
        {
            order = new TransactionSampleOrder(bizId, productCode, quantity);
            order.Cancel();
            await _repository.InsertOrderAsync(order);
            return;
        }

        order.AssertRequest(productCode, quantity);
        order.Cancel();
    }

    public async Task PrepareInventoryAsync(string bizId, string productCode, int quantity)
    {
        var inventory = await _repository.FindInventoryAsync(bizId);
        if (inventory is null)
        {
            inventory = new TransactionSampleInventory(bizId, productCode, quantity);
            inventory.Prepare();
            await _repository.InsertInventoryAsync(inventory);
            return;
        }

        inventory.AssertRequest(productCode, quantity);
        inventory.Prepare();
    }

    public async Task ConfirmInventoryAsync(string bizId, string productCode, int quantity)
    {
        var inventory = await _repository.FindInventoryAsync(bizId)
                        ?? throw new BusinessException("Sample.Transaction.InventoryMissing", "待确认的示例库存不存在");
        inventory.AssertRequest(productCode, quantity);
        inventory.Confirm();
    }

    public async Task CancelInventoryAsync(string bizId, string productCode, int quantity)
    {
        var inventory = await _repository.FindInventoryAsync(bizId);
        if (inventory is null)
        {
            inventory = new TransactionSampleInventory(bizId, productCode, quantity);
            inventory.Cancel();
            await _repository.InsertInventoryAsync(inventory);
            return;
        }

        inventory.AssertRequest(productCode, quantity);
        inventory.Cancel();
    }

    public async Task CreateSagaOrderAsync(string bizId, string productCode, int quantity, decimal amount, string paymentChannel)
    {
        var order = await _repository.FindOrderAsync(bizId);
        if (order is null)
        {
            order = new TransactionSampleOrder(bizId, productCode, quantity, amount, paymentChannel);
            order.Apply();
            await _repository.InsertOrderAsync(order);
            return;
        }

        order.AssertRequest(productCode, quantity, amount, paymentChannel);
        order.Apply();
    }

    public async Task CompensateSagaOrderAsync(string bizId, string productCode, int quantity, decimal amount, string paymentChannel)
    {
        var order = await _repository.FindOrderAsync(bizId);
        if (order is null)
        {
            order = new TransactionSampleOrder(bizId, productCode, quantity, amount, paymentChannel);
            order.Compensate();
            await _repository.InsertOrderAsync(order);
            return;
        }

        order.AssertRequest(productCode, quantity, amount, paymentChannel);
        order.Compensate();
    }

    public async Task DeductSagaInventoryAsync(string bizId, string productCode, int quantity)
    {
        var inventory = await _repository.FindInventoryAsync(bizId);
        if (inventory is null)
        {
            inventory = new TransactionSampleInventory(bizId, productCode, quantity);
            inventory.Apply();
            await _repository.InsertInventoryAsync(inventory);
            return;
        }

        inventory.AssertRequest(productCode, quantity);
        inventory.Apply();
    }

    public async Task CompensateSagaInventoryAsync(string bizId, string productCode, int quantity)
    {
        var inventory = await _repository.FindInventoryAsync(bizId);
        if (inventory is null)
        {
            inventory = new TransactionSampleInventory(bizId, productCode, quantity);
            inventory.Compensate();
            await _repository.InsertInventoryAsync(inventory);
            return;
        }

        inventory.AssertRequest(productCode, quantity);
        inventory.Compensate();
    }
}
