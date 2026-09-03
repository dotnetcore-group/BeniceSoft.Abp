using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace BeniceSoft.Abp.Extensions.RabbitMQ;

/// <summary>
/// 消费端基类
/// </summary>
public abstract class RabbitMessageHandlerBase<TMessage> : IRabbitMessageHandler<TMessage>
{
    protected ILogger Logger { get; }

    protected RabbitMessageHandlerBase(ILogger logger)
    {
        Logger = logger;
    }

    public async Task<RabbitMessageResult> HandleAsync(
        TMessage message,
        IReadOnlyBasicProperties properties,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ProcessAsync(message, properties, cancellationToken);
            return RabbitMessageResult.Success;
        }
        catch (RabbitDeadLetterException ex)
        {
            Logger.LogWarning(ex, "Message will be dead-lettered");
            return RabbitMessageResult.DeadLetter;
        }
        catch (RabbitTransientException ex)
        {
            Logger.LogWarning(ex, "Transient failure, message will be redelivered");
            return RabbitMessageResult.Redelivery;
        }
    }

    protected abstract Task ProcessAsync(
        TMessage message,
        IReadOnlyBasicProperties properties,
        CancellationToken cancellationToken);
}

public class RabbitDeadLetterException : Exception
{
    public RabbitDeadLetterException(string message) : base(message)
    {
    }

    public RabbitDeadLetterException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class RabbitTransientException : Exception
{
    public RabbitTransientException(string message) : base(message)
    {
    }

    public RabbitTransientException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
