using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;
using Volo.Abp.RabbitMQ;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IRabbitMqMessageConsumerFactory))]
public class BeniceSoftRabbitMqMessageConsumerFactory : IRabbitMqMessageConsumerFactory, ISingletonDependency, IDisposable
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private IServiceScope? _serviceScope;
    private BeniceSoftRabbitMqMessageConsumer? _consumer;

    public BeniceSoftRabbitMqMessageConsumerFactory(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public IRabbitMqMessageConsumer Create(
            ExchangeDeclareConfiguration exchange,
            QueueDeclareConfiguration queue,
            string? connectionName = null)
    {
        if (_consumer != null)
        {
            return _consumer;
        }

        _serviceScope = _serviceScopeFactory.CreateScope();
        _consumer = _serviceScope.ServiceProvider.GetRequiredService<BeniceSoftRabbitMqMessageConsumer>();
        _consumer.Initialize(exchange, queue, connectionName);
        return _consumer;
    }

    public void Dispose()
    {
        _consumer?.Dispose();
        _consumer = null;
        _serviceScope?.Dispose();
        _serviceScope = null;
    }
}
