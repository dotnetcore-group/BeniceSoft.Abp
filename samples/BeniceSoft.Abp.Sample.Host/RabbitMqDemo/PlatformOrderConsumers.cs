using BeniceSoft.Abp.Extensions.RabbitMQ;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace BeniceSoft.Abp.Sample.Host.RabbitMqDemo;

/// <summary>亚马逊订单批次消息（示例：一条消息可含多笔订单）。</summary>
public sealed class AmazonOrderBatchMessage
{
    public string BatchId { get; set; } = string.Empty;

    public List<AmazonOrderItem> Orders { get; set; } = [];
}

public sealed class AmazonOrderItem
{
    public string PlatformOrderId { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}

/// <summary>Warfire 订单批次消息（结构可与 Amazon 完全不同）。</summary>
public sealed class WarfireOrderBatchMessage
{
    public string ImportId { get; set; } = string.Empty;

    public List<WarfireOrderItem> Items { get; set; } = [];
}

public sealed class WarfireOrderItem
{
    public string ExternalId { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public int Qty { get; set; }
}

public class AmazonOrderHandler : RabbitMessageHandlerBase<AmazonOrderBatchMessage>
{
    public AmazonOrderHandler(ILogger<AmazonOrderHandler> logger) : base(logger)
    {
    }

    protected override Task ProcessAsync(
        AmazonOrderBatchMessage message,
        IReadOnlyBasicProperties properties,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation(
            "Amazon handler: BatchId={BatchId}, OrderCount={Count}",
            message.BatchId,
            message.Orders.Count);
        // 真实场景：映射后批量 Insert 本消息内的订单
        return Task.CompletedTask;
    }
}

public class WarfireOrderHandler : RabbitMessageHandlerBase<WarfireOrderBatchMessage>
{
    public WarfireOrderHandler(ILogger<WarfireOrderHandler> logger) : base(logger)
    {
    }

    protected override Task ProcessAsync(
        WarfireOrderBatchMessage message,
        IReadOnlyBasicProperties properties,
        CancellationToken cancellationToken)
    {
        Logger.LogInformation(
            "Warfire handler: ImportId={ImportId}, ItemCount={Count}",
            message.ImportId,
            message.Items.Count);
        return Task.CompletedTask;
    }
}
