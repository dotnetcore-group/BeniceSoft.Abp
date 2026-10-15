using BeniceSoft.Abp.Extensions.RabbitMQ.EntityFrameworkCore;
using BeniceSoft.Core;
using Dapper;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.Extensions.RabbitMQ.Tests;

/// <summary>Outbox / inbox / bounded retry / DLQ replay. RabbitMQ: real broker from appsettings.json; DB: in-memory SQLite.</summary>
public class ReliableMessagingTests : RabbitMqTestBase
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Tables_Should_Be_Auto_Created_On_Fresh_Database()
    {
        (await TableExistsAsync("rabbit_outbox_messages")).ShouldBeFalse();
        (await TableExistsAsync("rabbit_inbox_messages")).ShouldBeFalse();

        using (var uow = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            await GetRequiredService<IRabbitOutboxPublisher>().PublishToQueueAsync("benicesoft.tests.fresh", NewMessage(), cancellationToken: Ct);
            await uow.CompleteAsync(Ct);
        }

        (await TableExistsAsync("rabbit_outbox_messages")).ShouldBeTrue();
        (await TableExistsAsync("rabbit_inbox_messages")).ShouldBeTrue();
        (await CountAsync("rabbit_outbox_messages")).ShouldBe(1);
    }

    [Fact]
    public async Task Outbox_Rollback_Should_Discard_Business_Row_And_Message()
    {
        using (var uow = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
        {
            var dbContext = await GetRequiredService<IDbContextProvider<TestBusinessDbContext>>().GetDbContextAsync();
            await dbContext.Orders.AddAsync(new TestOrder { Id = Guid.NewGuid(), Name = "rollback" }, Ct);
            await GetRequiredService<IRabbitOutboxPublisher>().PublishToQueueAsync("benicesoft.tests.rollback", NewMessage(), cancellationToken: Ct);
            await uow.SaveChangesAsync(Ct);
            (await CountAsync("rabbit_outbox_messages", inCurrentUow: true)).ShouldBe(1);
            await uow.RollbackAsync(Ct);
        }

        (await CountAsync("test_orders")).ShouldBe(0);
        (await CountAsync("rabbit_outbox_messages")).ShouldBe(0);
    }

    [Fact]
    public async Task Outbox_Publish_Outside_UnitOfWork_Should_Throw()
    {
        var outbox = GetRequiredService<IRabbitOutboxPublisher>();
        await Should.ThrowAsync<InvalidOperationException>(
            () => outbox.PublishToQueueAsync("benicesoft.tests.no-uow", NewMessage(), cancellationToken: Ct));
    }

    [Fact]
    public async Task Outbox_Commit_Then_Dispatcher_Should_Send_And_Mark_Sent()
    {
        var queue = NewQueueName("outbox");
        try
        {
            await DeclareAsync(RabbitRelational.Work(queue));
            var message = NewMessage();
            string messageId;
            using (var uow = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: true))
            {
                var dbContext = await GetRequiredService<IDbContextProvider<TestBusinessDbContext>>().GetDbContextAsync();
                await dbContext.Orders.AddAsync(new TestOrder { Id = Guid.NewGuid(), Name = "commit" }, Ct);
                messageId = await GetRequiredService<IRabbitOutboxPublisher>().PublishToQueueAsync(queue, message, cancellationToken: Ct);
                await uow.CompleteAsync(Ct);
            }

            (await CountAsync("test_orders")).ShouldBe(1);

            var dispatcher = GetRequiredService<RabbitOutboxDispatcher>();
            (await dispatcher.DispatchOnceAsync(Ct)).ShouldBe(1);
            (await dispatcher.DispatchOnceAsync(Ct)).ShouldBe(0);

            (await CountAsync("rabbit_outbox_messages", where: "status = 1 AND sent_time IS NOT NULL AND lock_token IS NULL AND message_id = @messageId", param: new { messageId }))
                .ShouldBe(1);

            await using var channel = await GetRequiredService<IRabbitConnection>().CreateChannelAsync(Ct);
            var delivered = await channel.BasicGetAsync(queue, true, Ct);
            delivered.ShouldNotBeNull();
            delivered.BasicProperties.MessageId.ShouldBe(messageId);
            JsonUtils.Deserialize<TestWorkMessage>(System.Text.Encoding.UTF8.GetString(delivered.Body.Span), JsonUtils.DefaultOptions)!.Id.ShouldBe(message.Id);
        }
        finally
        {
            await DeleteQueuesAsync(queue);
        }
    }

    [Fact]
    public async Task Idempotent_Consumer_Should_Skip_Duplicate_MessageId()
    {
        var queue = NewQueueName("inbox");
        var log = GetRequiredService<HandlerCallLog>();
        try
        {
            await using var subscriber = NewSubscriber(RabbitRelational.Work(queue));
            subscriber.InboxConsumerName = "tests.counting";
            await subscriber.StartAsync(Ct);

            var duplicate = NewMessage();
            var duplicateId = Guid.NewGuid().ToString("N");
            var sentinel = NewMessage();
            using (var publisher = ServiceProvider.CreateWorkPublisher(queue))
            {
                await publisher.PublishAsync(duplicate, p => p.MessageId = duplicateId, Ct);
                await publisher.PublishAsync(duplicate, p => p.MessageId = duplicateId, Ct);
                await publisher.PublishAsync(sentinel, cancellationToken: Ct);
            }

            // Prefetch 1 => in-order; once the sentinel is handled both duplicates have been seen.
            await log.WaitForAsync(sentinel.Id, 1, TimeSpan.FromSeconds(20), Ct);

            log.Count(duplicate.Id).ShouldBe(1);
            (await CountAsync("rabbit_inbox_messages", where: "message_id = @duplicateId", param: new { duplicateId })).ShouldBe(1);
        }
        finally
        {
            await DeleteQueuesAsync(queue);
        }
    }

    [Fact]
    public async Task Exception_Should_Retry_Bounded_Then_DeadLetter()
    {
        var queue = NewQueueName("retry");
        var deadLetterQueue = queue + ".dlq";
        var log = GetRequiredService<HandlerCallLog>();
        try
        {
            await DeclareAsync(RabbitRelational.Work(deadLetterQueue));
            var relational = RabbitRelational.Work(new RabbitQueueDeclare { Name = queue }.SetDeadLetter(string.Empty, deadLetterQueue));

            await using var subscriber = NewSubscriber(relational);
            subscriber.MaxExceptionRetryTimes = 2;
            await subscriber.StartAsync(Ct);

            var message = NewMessage(CountingMessageHandler.ThrowPayload);
            using (var publisher = ServiceProvider.CreatePublisher(relational))
            {
                await publisher.PublishAsync(message, cancellationToken: Ct);
            }

            await WaitForMessageCountAsync(deadLetterQueue, 1);
            log.Count(message.Id).ShouldBe(3); // 1 attempt + 2 retries
            (await GetMessageCountAsync(queue)).ShouldBe(0u);
        }
        finally
        {
            await DeleteQueuesAsync(queue, deadLetterQueue);
        }
    }

    [Fact]
    public async Task DeadLetter_Replay_Should_Move_Messages_Back_To_Original_Queue()
    {
        var queue = NewQueueName("replay");
        var deadLetterQueue = queue + ".dlq";
        try
        {
            await DeclareAsync(RabbitRelational.Work(deadLetterQueue));
            var relational = RabbitRelational.Work(new RabbitQueueDeclare { Name = queue }.SetDeadLetter(string.Empty, deadLetterQueue));
            await DeclareAsync(relational);

            string messageId;
            await using (var channel = await GetRequiredService<IRabbitConnection>().CreateChannelAsync(Ct))
            {
                using (var publisher = ServiceProvider.CreatePublisher(relational))
                {
                    await publisher.PublishAsync(NewMessage(), cancellationToken: Ct);
                }

                await WaitForMessageCountAsync(queue, 1);
                var got = await channel.BasicGetAsync(queue, false, Ct);
                got.ShouldNotBeNull();
                messageId = got.BasicProperties.MessageId!;
                await channel.BasicRejectAsync(got.DeliveryTag, false, Ct); // dead-letter with x-death
            }

            await WaitForMessageCountAsync(deadLetterQueue, 1);

            var replayed = await GetRequiredService<IRabbitDeadLetterReplayer>().ReplayAsync(deadLetterQueue, 10, cancellationToken: Ct);

            replayed.ShouldBe(1);
            await WaitForMessageCountAsync(queue, 1);
            (await GetMessageCountAsync(deadLetterQueue)).ShouldBe(0u);

            await using var check = await GetRequiredService<IRabbitConnection>().CreateChannelAsync(Ct);
            var back = await check.BasicGetAsync(queue, true, Ct);
            back.ShouldNotBeNull();
            back.BasicProperties.MessageId.ShouldBe(messageId);
        }
        finally
        {
            await DeleteQueuesAsync(queue, deadLetterQueue);
        }
    }

    private static TestWorkMessage NewMessage(string payload = "ok") => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Payload = payload
    };

    private static string NewQueueName(string name) => $"benicesoft.tests.{name}.{Guid.NewGuid():N}";

    private RabbitSubscriber NewSubscriber(RabbitRelational relational) => new(
        GetRequiredService<IRabbitConnection>(),
        GetRequiredService<IServiceScopeFactory>(),
        relational,
        typeof(TestWorkMessage),
        typeof(CountingMessageHandler),
        prefetchCount: 1,
        messageContextPropagator: GetRequiredService<RabbitMessageContextPropagator>());

    private async Task<T> WithConnectionAsync<T>(Func<System.Data.Common.DbConnection, System.Data.Common.DbTransaction?, Task<T>> action, bool inCurrentUow = false)
    {
        if (inCurrentUow)
        {
            var current = await GetRequiredService<IDbContextProvider<TestBusinessDbContext>>().GetDbContextAsync();
            return await action(current.Database.GetDbConnection(), current.Database.CurrentTransaction?.GetDbTransaction());
        }

        using var scope = ServiceProvider.CreateScope();
        using var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
        var dbContext = await scope.ServiceProvider.GetRequiredService<IDbContextProvider<TestBusinessDbContext>>().GetDbContextAsync();
        await dbContext.Database.OpenConnectionAsync(Ct);
        var result = await action(dbContext.Database.GetDbConnection(), null);
        await uow.CompleteAsync(Ct);
        return result;
    }

    private Task<bool> TableExistsAsync(string table)
        => WithConnectionAsync(async (connection, tx) =>
            await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @table", new { table }, tx) > 0);

    private Task<long> CountAsync(string table, bool inCurrentUow = false, string? where = null, object? param = null)
        => WithConnectionAsync(async (connection, tx) =>
        {
            var exists = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @table", new { table }, tx) > 0;
            return exists
                ? await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {table}" + (where == null ? string.Empty : " WHERE " + where), param, tx)
                : 0;
        }, inCurrentUow);

    private async Task DeclareAsync(RabbitRelational relational)
    {
        await using var channel = await GetRequiredService<IRabbitConnection>().CreateChannelAsync(Ct);
        await relational.ConsumerDeclareAsync(channel, Ct);
    }

    private async Task<uint> GetMessageCountAsync(string queue)
    {
        await using var channel = await GetRequiredService<IRabbitConnection>().CreateChannelAsync(Ct);
        return await channel.MessageCountAsync(queue, Ct);
    }

    private async Task WaitForMessageCountAsync(string queue, uint expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        uint count;
        while ((count = await GetMessageCountAsync(queue)) < expected)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Queue {queue} has {count} message(s), expected {expected}");
            }

            await Task.Delay(200, Ct);
        }
    }

    private async Task DeleteQueuesAsync(params string[] queues)
    {
        await using var channel = await GetRequiredService<IRabbitConnection>().CreateChannelAsync();
        foreach (var queue in queues)
        {
            await channel.QueueDeleteAsync(queue, false, false);
        }
    }
}