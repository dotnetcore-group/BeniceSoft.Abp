using Volo.Abp;
using Volo.Abp.Domain.Entities;

namespace BeniceSoft.Abp.Sample.Domain;

/// <summary>
/// 分布式事务示例订单
/// </summary>
public class TransactionSampleOrder : Entity<long>
{
    public string BizId { get; private set; } = string.Empty;
    public string ProductCode { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal Amount { get; private set; }
    public string PaymentChannel { get; private set; } = string.Empty;
    public TransactionSampleState State { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    protected TransactionSampleOrder()
    {
    }

    public TransactionSampleOrder(string bizId, string productCode, int quantity, decimal amount = 0, string paymentChannel = "")
    {
        if (string.IsNullOrWhiteSpace(bizId) || bizId.Length > 128 ||
            string.IsNullOrWhiteSpace(productCode) || productCode.Length > 64 || quantity <= 0 || amount < 0 ||
            paymentChannel is null || paymentChannel.Length > 32)
        {
            throw new BusinessException("Sample.Transaction.InvalidOrder", "示例订单参数不合法");
        }

        BizId = bizId;
        ProductCode = productCode;
        Quantity = quantity;
        Amount = amount;
        PaymentChannel = paymentChannel;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AssertRequest(string productCode, int quantity, decimal amount = 0, string paymentChannel = "")
    {
        if (ProductCode != productCode || Quantity != quantity || Amount != amount || PaymentChannel != paymentChannel)
        {
            throw new BusinessException("Sample.Transaction.OrderConflict", "同一 BizId 的订单参数不一致");
        }
    }

    public void Prepare()
    {
        if (State == TransactionSampleState.Prepared)
        {
            return;
        }

        if (State != TransactionSampleState.Initial)
        {
            throw new BusinessException("Sample.Transaction.OrderState", "订单当前状态不允许预创建");
        }

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

        if (State != TransactionSampleState.Prepared)
        {
            throw new BusinessException("Sample.Transaction.OrderState", "订单未预创建，不能确认");
        }

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
            throw new BusinessException("Sample.Transaction.OrderState", "订单当前状态不允许取消");
        }

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

        if (State != TransactionSampleState.Initial)
        {
            throw new BusinessException("Sample.Transaction.OrderState", "订单当前状态不允许创建");
        }

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
            throw new BusinessException("Sample.Transaction.OrderState", "订单当前状态不允许补偿");
        }

        State = TransactionSampleState.Compensated;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

