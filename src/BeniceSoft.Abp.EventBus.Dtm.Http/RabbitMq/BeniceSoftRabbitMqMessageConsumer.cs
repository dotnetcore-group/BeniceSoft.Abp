using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ExceptionHandling;
using Volo.Abp.RabbitMQ;
using Volo.Abp.Threading;

namespace BeniceSoft.Abp.EventBus.Dtm.Http;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IRabbitMqMessageConsumer), typeof(BeniceSoftRabbitMqMessageConsumer))]
public class BeniceSoftRabbitMqMessageConsumer : RabbitMqMessageConsumer
{
    public BeniceSoftRabbitMqMessageConsumer(
        IConnectionPool connectionPool,
        AbpAsyncTimer timer,
        IExceptionNotifier exceptionNotifier)
        : base(connectionPool, timer, exceptionNotifier)
    {
    }

    public override void OnMessageReceived(Func<IChannel, BasicDeliverEventArgs, Task> callback)
    {
        if (callback.Method.Name == nameof(BeniceSoftRabbitMqDistributedEventBus.ProcessEventAppendHeadersAsync))
        {
            base.OnMessageReceived(callback);
        }
    }

    protected override async Task TryCreateChannelAsync()
    {
        // 尚未挂上有效回调时不要 BasicConsume，否则会空 Callbacks 直接 Ack 丢消息。
        if (Callbacks.IsEmpty)
        {
            return;
        }

        await base.TryCreateChannelAsync();
    }
}
