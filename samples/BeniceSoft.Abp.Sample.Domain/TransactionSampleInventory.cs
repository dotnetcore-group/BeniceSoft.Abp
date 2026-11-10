using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace BeniceSoft.Abp.Sample.Domain;

/// <summary>
/// 按 BizId 隔离的分布式事务示例库存
/// </summary>
public class TransactionSampleInventory : Entity<long>
{
    public string BizId { get; private set; } = string.Empty;
    public string ProductCode { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public int AvailableQuantity { get; private set; } = 10;
    public int FrozenQuantity { get; private set; }
    public int DeductedQuantity { get; private set; }
    public TransactionSampleState State { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    protected TransactionSampleInventory()
    {
    }

    public TransactionSampleInventory(string bizId, string productCode, int quantity)
    {
        if (string.IsNullOrWhiteSpace(bizId) || bizId.Length > 128 ||
            string.IsNullOrWhiteSpace(productCode) || productCode.Length > 64 || quantity <= 0)
        {
            throw new BusinessException("Sample.Transaction.InvalidInventory", "示例库存参数不合法");
        }

        BizId = bizId;
        ProductCode = productCode;
        Quantity = quantity;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AssertRequest(string productCode, int quantity)
    {
        if (ProductCode != productCode || Quantity != quantity)
        {
            throw new BusinessException("Sample.Transaction.InventoryConflict", "同一 BizId 的库存参数不一致");
        }
    }

    public void Prepare()
    {
        if (State == TransactionSampleState.Prepared)
        {
            return;
        }

        if (State != TransactionSampleState.Initial || AvailableQuantity < Quantity)
        {
            throw new BusinessException("Sample.Transaction.InventoryUnavailable", "库存状态不允许冻结或可用库存不足");
        }

        AvailableQuantity -= Quantity;
        FrozenQuantity += Quantity;
        State = TransactionSampleState.Prepared;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Confirm()
    {
        if (State == TransactionSampleState.Confirmed)
        {
            return;
        }

        if (State != TransactionSampleState.Prepared || FrozenQuantity != Quantity)
        {
            throw new BusinessException("Sample.Transaction.InventoryState", "库存未冻结，不能确认扣减");
        }

        FrozenQuantity -= Quantity;
        DeductedQuantity += Quantity;
        State = TransactionSampleState.Confirmed;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        if (State == TransactionSampleState.Cancelled)
        {
            return;
        }

        if (State != TransactionSampleState.Initial && State != TransactionSampleState.Prepared)
        {
            throw new BusinessException("Sample.Transaction.InventoryState", "库存当前状态不允许取消冻结");
        }

        AvailableQuantity += FrozenQuantity;
        FrozenQuantity = 0;
        State = TransactionSampleState.Cancelled;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Apply()
    {
        if (State == TransactionSampleState.Applied)
        {
            return;
        }

        if (State != TransactionSampleState.Initial || AvailableQuantity < Quantity)
        {
            throw new BusinessException("Sample.Transaction.InventoryUnavailable", "库存状态不允许扣减或可用库存不足");
        }

        AvailableQuantity -= Quantity;
        DeductedQuantity += Quantity;
        State = TransactionSampleState.Applied;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Compensate()
    {
        if (State == TransactionSampleState.Compensated)
        {
            return;
        }

        if (State != TransactionSampleState.Initial && State != TransactionSampleState.Applied)
        {
            throw new BusinessException("Sample.Transaction.InventoryState", "库存当前状态不允许补偿");
        }

        AvailableQuantity += DeductedQuantity;
        DeductedQuantity = 0;
        State = TransactionSampleState.Compensated;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
