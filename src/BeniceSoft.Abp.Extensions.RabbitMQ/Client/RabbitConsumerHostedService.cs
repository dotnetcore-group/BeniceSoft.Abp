using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Volo.Abp.RabbitMQ;

namespace BeniceSoft.Abp.Extensions.RabbitMQ;

internal sealed class RabbitConsumerHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<RabbitConsumerRegistration> _registrations;
    private readonly ILogger<RabbitConsumerHostedService> _logger;
    private readonly List<RabbitSubscriber> _subscribers = [];

    public RabbitConsumerHostedService(
        IServiceProvider serviceProvider,
        IEnumerable<RabbitConsumerRegistration> registrations,
        ILogger<RabbitConsumerHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _registrations = registrations;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var pool = _serviceProvider.GetService<IConnectionPool>();
        if (pool == null)
        {
            _logger.LogWarning("IConnectionPool is not registered; RabbitMQ consumers will not start");
            return;
        }

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var defaultConnection = _serviceProvider.GetRequiredService<IRabbitConnection>();

        foreach (var registration in _registrations)
        {
            var options = registration.Options;
            if (string.IsNullOrWhiteSpace(options.QueueName) && options.Relational?.QueueDeclare?.Name == null)
            {
                throw new InvalidOperationException(
                    $"QueueName is required for consumer {registration.HandlerType.Name}");
            }

            IRabbitConnection connection = string.IsNullOrWhiteSpace(options.ConnectionName)
                ? defaultConnection
                : new AbpConnectionPoolRabbitConnection(pool, options.ConnectionName);

            var relational = options.Relational ?? RabbitRelational.Work(options.QueueName);
            var messageContext = _serviceProvider.GetService<RabbitMessageContextPropagator>();
            var subscriber = new RabbitSubscriber(
                connection,
                scopeFactory,
                relational,
                registration.MessageType,
                registration.HandlerType,
                options.PrefetchCount,
                options.MaxRedeliveryTimes,
                _logger,
                messageContext);

            await subscriber.StartAsync(cancellationToken);
            _subscribers.Add(subscriber);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var subscriber in _subscribers)
        {
            await subscriber.DisposeAsync();
        }

        _subscribers.Clear();
    }
}
