using BeniceSoft.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Volo.Abp.RabbitMQ;

namespace BeniceSoft.Abp.Extensions.RabbitMQ;

public static class BeniceSoftRabbitMqServiceCollectionExtensions
{
    /// <summary>
    /// 注册作业队列能力，连接复用 ABP <c>RabbitMQ:Connections</c>（经 <see cref="IConnectionPool"/>）。
    /// </summary>
    public static IServiceCollection AddBeniceSoftRabbitMq(this IServiceCollection services)
    {
        services.TryAddSingleton<IRabbitConnection>(sp =>
            new AbpConnectionPoolRabbitConnection(sp.GetRequiredService<IConnectionPool>()));

        services.TryAddSingleton<RabbitMessageContextPropagator>();

        services.TryAddSingleton(sp =>
        {
            var logger = sp.GetService<ILogger<RabbitPublisher>>();
            var messageContext = sp.GetService<RabbitMessageContextPropagator>();
            var pool = sp.GetRequiredService<IConnectionPool>();
            return new Func<RabbitRelational, string?, IRabbitPublisher>((relational, connectionName) =>
            {
                IRabbitConnection connection = string.IsNullOrWhiteSpace(connectionName)
                    ? sp.GetRequiredService<IRabbitConnection>()
                    : new AbpConnectionPoolRabbitConnection(pool, connectionName);
                return new RabbitPublisher(connection, relational, logger, messageContext);
            });
        });

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, RabbitConsumerHostedService>());

        return services;
    }

    /// <summary>
    /// 按平台在代码中注册独立 Work 队列消费者
    /// </summary>
    public static IServiceCollection AddRabbitWorkConsumer<TMessage, THandler>(
        this IServiceCollection services,
        Action<RabbitWorkConsumerOptions> configure)
        where THandler : class, IRabbitMessageHandler<TMessage>
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RabbitWorkConsumerOptions();
        configure(options);

        if (options.QueueName.IsNull() && options.Relational == null)
        {
            throw new ArgumentException("QueueName or Relational must be set", nameof(configure));
        }

        options.Relational ??= RabbitRelational.Work(options.QueueName);

        services.AddTransient<THandler>();
        services.AddTransient<IRabbitMessageHandler<TMessage>, THandler>();
        services.AddSingleton(new RabbitConsumerRegistration
        {
            MessageType = typeof(TMessage),
            HandlerType = typeof(THandler),
            Options = options
        });

        return services;
    }

    public static IRabbitPublisher CreateWorkPublisher(
        this IServiceProvider serviceProvider,
        string queueName,
        string? connectionName = null)
    {
        var factory = serviceProvider.GetRequiredService<Func<RabbitRelational, string?, IRabbitPublisher>>();
        return factory(RabbitRelational.Work(queueName), connectionName);
    }

    public static IRabbitPublisher CreatePublisher(
        this IServiceProvider serviceProvider,
        RabbitRelational relational,
        string? connectionName = null)
    {
        var factory = serviceProvider.GetRequiredService<Func<RabbitRelational, string?, IRabbitPublisher>>();
        return factory(relational, connectionName);
    }
}
