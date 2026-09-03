using BeniceSoft.Core;
using RabbitMQ.Client;

namespace BeniceSoft.Abp.Extensions.RabbitMQ;

/// <summary>
/// RabbitMQ 拓扑关系：Work / Fanout / Direct / Topic。
/// </summary>
public abstract class RabbitRelational
{
    /// <summary>
    /// 工作队列：多消费者竞争消费同一队列。发布时 Exchange 为空，RoutingKey=队列名。
    /// </summary>
    public static RabbitRelational Work(RabbitQueueDeclare queueDeclare)
    {
        if (queueDeclare.Name.IsNull())
        {
            throw new ArgumentException(nameof(queueDeclare.Name));
        }

        return new WorkRelational
        {
            RoutingKey = queueDeclare.Name,
            BindRoutingKey = queueDeclare.Name,
            QueueDeclare = queueDeclare
        };
    }

    public static RabbitRelational Work(string queueName, IDictionary<string, object?>? arguments = null)
        => Work(new RabbitQueueDeclare { Name = queueName, Arguments = arguments });

    /// <summary>
    /// 广播：Exchange 将同一份数据分发给多个队列。
    /// </summary>
    public static RabbitRelational Fanout(RabbitExchangeDeclare exchangeDeclare, RabbitQueueDeclare queueDeclare)
    {
        if (exchangeDeclare.Name.IsNull())
        {
            throw new ArgumentException(nameof(exchangeDeclare.Name));
        }

        if (queueDeclare.Name.IsNull())
        {
            throw new ArgumentException(nameof(queueDeclare.Name));
        }

        return new FanoutRelational
        {
            ExchangeType = ExchangeTypes.Fanout,
            ExchangeDeclare = exchangeDeclare,
            QueueDeclare = queueDeclare
        };
    }

    public static RabbitRelational Fanout(
        string exchangeName,
        string queueName,
        IDictionary<string, object?>? exchangeArguments = null,
        IDictionary<string, object?>? queueArguments = null)
        => Fanout(
            new RabbitExchangeDeclare { Name = exchangeName, Arguments = exchangeArguments },
            new RabbitQueueDeclare { Name = queueName, Arguments = queueArguments });

    /// <summary>
    /// 路由：按 RoutingKey 精确分发。
    /// </summary>
    public static RabbitRelational Direct(RabbitExchangeDeclare exchangeDeclare, RabbitQueueDeclare queueDeclare, string routingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeDeclare.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueDeclare.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        return new DirectRelational
        {
            ExchangeType = ExchangeTypes.Direct,
            BindRoutingKey = routingKey,
            RoutingKey = routingKey,
            ExchangeDeclare = exchangeDeclare,
            QueueDeclare = queueDeclare
        };
    }

    public static RabbitRelational Direct(
        string exchangeName,
        string queueName,
        string routingKey,
        IDictionary<string, object?>? exchangeArguments = null,
        IDictionary<string, object?>? queueArguments = null)
        => Direct(
            new RabbitExchangeDeclare { Name = exchangeName, Arguments = exchangeArguments },
            new RabbitQueueDeclare { Name = queueName, Arguments = queueArguments },
            routingKey);

    /// <summary>
    /// 主题：RoutingKey 模糊匹配（* / #）。
    /// </summary>
    public static RabbitRelational Topic(
        RabbitExchangeDeclare exchangeDeclare,
        RabbitQueueDeclare queueDeclare,
        string bindRoutingKey,
        string routingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeDeclare.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueDeclare.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindRoutingKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        return new TopicRelational
        {
            ExchangeType = ExchangeTypes.Topic,
            BindRoutingKey = bindRoutingKey,
            RoutingKey = routingKey,
            ExchangeDeclare = exchangeDeclare,
            QueueDeclare = queueDeclare
        };
    }

    public static RabbitRelational Topic(
        string exchangeName,
        string queueName,
        string bindRoutingKey,
        string routingKey,
        IDictionary<string, object?>? exchangeArguments = null,
        IDictionary<string, object?>? queueArguments = null)
        => Topic(
            new RabbitExchangeDeclare { Name = exchangeName, Arguments = exchangeArguments },
            new RabbitQueueDeclare { Name = queueName, Arguments = queueArguments },
            bindRoutingKey,
            routingKey);

    public RabbitRelational SetBindArguments(IDictionary<string, object?> arguments)
    {
        if (ExchangeType.IsNull())
        {
            throw new InvalidOperationException("the current mode does not support queue binding");
        }

        BindArguments = arguments;
        return this;
    }

    public string ExchangeType { get; private set; } = string.Empty;

    public string BindRoutingKey { get; private set; } = string.Empty;

    public string RoutingKey { get; private set; } = string.Empty;

    public IDictionary<string, object?>? BindArguments { get; private set; }

    public RabbitExchangeDeclare? ExchangeDeclare { get; private set; }

    public RabbitQueueDeclare? QueueDeclare { get; private set; }

    public abstract Task ProducerDeclareAsync(IChannel channel, CancellationToken cancellationToken = default);

    public abstract Task ConsumerDeclareAsync(IChannel channel, CancellationToken cancellationToken = default);

    private static class ExchangeTypes
    {
        public const string Direct = "direct";
        public const string Topic = "topic";
        public const string Fanout = "fanout";
    }

    private sealed class WorkRelational : RabbitRelational
    {
        public override Task ProducerDeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
            => channel.QueueDeclareAsync(
                QueueDeclare!.Name,
                QueueDeclare.Durable,
                QueueDeclare.Exclusive,
                QueueDeclare.AutoDelete,
                QueueDeclare.Arguments,
                false,
                cancellationToken);

        public override Task ConsumerDeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
            => ProducerDeclareAsync(channel, cancellationToken);
    }

    private sealed class FanoutRelational : RabbitRelational
    {
        public override Task ProducerDeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
            => channel.ExchangeDeclareAsync(
                ExchangeDeclare!.Name,
                ExchangeType,
                ExchangeDeclare.Durable,
                ExchangeDeclare.AutoDelete,
                ExchangeDeclare.Arguments,
                false,
                cancellationToken);

        public override async Task ConsumerDeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            await ProducerDeclareAsync(channel, cancellationToken);
            await channel.QueueDeclareAsync(
                QueueDeclare!.Name,
                QueueDeclare.Durable,
                QueueDeclare.Exclusive,
                QueueDeclare.AutoDelete,
                QueueDeclare.Arguments,
                false,
                cancellationToken);
            await channel.QueueBindAsync(QueueDeclare.Name, ExchangeDeclare!.Name, string.Empty, BindArguments, false, cancellationToken);
        }
    }

    private sealed class DirectRelational : RabbitRelational
    {
        public override async Task ProducerDeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            await channel.ExchangeDeclareAsync(
                ExchangeDeclare!.Name,
                ExchangeType,
                ExchangeDeclare.Durable,
                ExchangeDeclare.AutoDelete,
                ExchangeDeclare.Arguments,
                false,
                cancellationToken);
        }

        public override async Task ConsumerDeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            await ProducerDeclareAsync(channel, cancellationToken);
            await channel.QueueDeclareAsync(
                QueueDeclare!.Name,
                QueueDeclare.Durable,
                QueueDeclare.Exclusive,
                QueueDeclare.AutoDelete,
                QueueDeclare.Arguments,
                false,
                cancellationToken);
            await channel.QueueBindAsync(QueueDeclare.Name, ExchangeDeclare!.Name, BindRoutingKey, BindArguments, false, cancellationToken);
        }
    }

    private sealed class TopicRelational : RabbitRelational
    {
        public override async Task ProducerDeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            await channel.ExchangeDeclareAsync(
                ExchangeDeclare!.Name,
                ExchangeType,
                ExchangeDeclare.Durable,
                ExchangeDeclare.AutoDelete,
                ExchangeDeclare.Arguments,
                false,
                cancellationToken);
        }

        public override async Task ConsumerDeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
        {
            await ProducerDeclareAsync(channel, cancellationToken);
            await channel.QueueDeclareAsync(
                QueueDeclare!.Name,
                QueueDeclare.Durable,
                QueueDeclare.Exclusive,
                QueueDeclare.AutoDelete,
                QueueDeclare.Arguments,
                false,
                cancellationToken);
            await channel.QueueBindAsync(QueueDeclare.Name, ExchangeDeclare!.Name, BindRoutingKey, BindArguments, false, cancellationToken);
        }
    }
}

public class RabbitExchangeDeclare
{
    public string Name { get; set; } = string.Empty;

    public bool Durable { get; set; } = true;

    public bool AutoDelete { get; set; }

    public IDictionary<string, object?>? Arguments { get; set; }
}

public class RabbitQueueDeclare
{
    public string Name { get; set; } = string.Empty;

    public bool Durable { get; set; } = true;

    public bool Exclusive { get; set; }

    public bool AutoDelete { get; set; }

    public IDictionary<string, object?>? Arguments { get; set; }

    public RabbitQueueDeclare SetDeadLetter(string? exchangeName, string? routingKey)
    {
        if (exchangeName.IsNull() && routingKey.IsNull())
        {
            throw new ArgumentException("both parameters cannot be empty");
        }

        Arguments ??= new Dictionary<string, object?>();
        Arguments["x-dead-letter-exchange"] = exchangeName.ToStringSafe();
        Arguments["x-dead-letter-routing-key"] = routingKey.ToStringSafe();
        return this;
    }

    public RabbitQueueDeclare SetDeadLetter(RabbitRelational relational)
        => SetDeadLetter(relational.ExchangeDeclare?.Name, relational.BindRoutingKey);
}
