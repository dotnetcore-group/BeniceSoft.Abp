using RabbitMQ.Client;

namespace BeniceSoft.Abp.Extensions.RabbitMQ;

public interface IRabbitConnection
{
    bool IsConnected { get; }

    Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default);

    Task<IChannel> CreateChannelAsync(CreateChannelOptions? options, CancellationToken cancellationToken = default);
}
