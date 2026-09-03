using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace BeniceSoft.Abp.Extensions.RabbitMQ.Tests;

public class WorkQueueIntegrationTests : RabbitMqTestBase
{
    [Fact]
    public async Task ConnectionPool_Should_Create_Open_Channel()
    {
        var connection = GetRequiredService<IRabbitConnection>();

        await using var channel = await connection.CreateChannelAsync();

        channel.IsOpen.ShouldBeTrue();
    }

    [Fact]
    public async Task WorkQueue_Publish_Then_Consume_Should_Receive_Message()
    {
        var queueName = $"benicesoft.tests.work.{Guid.NewGuid():N}";
        var collector = GetRequiredService<ReceivedMessageCollector>();
        collector.Reset();
        var waiter = collector.ExpectOne();

        var messageContext = GetRequiredService<RabbitMessageContextPropagator>();
        var connection = GetRequiredService<IRabbitConnection>();
        var scopeFactory = GetRequiredService<IServiceScopeFactory>();
        var relational = RabbitRelational.Work(queueName);

        await using var subscriber = new RabbitSubscriber(
            connection,
            scopeFactory,
            relational,
            typeof(TestWorkMessage),
            typeof(TestWorkMessageHandler),
            prefetchCount: 1,
            messageContextPropagator: messageContext);

        await subscriber.StartAsync();

        var expected = new TestWorkMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            Payload = "hello-work-queue"
        };

        using (var publisher = ServiceProvider.CreateWorkPublisher(queueName))
        {
            await publisher.PublishAsync(expected);
        }

        var (actual, _) = await waiter.Task.WaitAsync(TimeSpan.FromSeconds(15));

        actual.Id.ShouldBe(expected.Id);
        actual.Payload.ShouldBe(expected.Payload);
    }

    [Fact]
    public async Task WorkQueue_Should_Propagate_Current_User_And_Tenant()
    {
        var queueName = $"benicesoft.tests.user-context.{Guid.NewGuid():N}";
        var collector = GetRequiredService<ReceivedMessageCollector>();
        collector.Reset();
        var waiter = collector.ExpectOne();

        var messageContext = GetRequiredService<RabbitMessageContextPropagator>();
        var connection = GetRequiredService<IRabbitConnection>();
        var scopeFactory = GetRequiredService<IServiceScopeFactory>();
        var principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        var currentTenant = GetRequiredService<ICurrentTenant>();

        var expectedUserId = 9527L;
        var expectedTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        await using var subscriber = new RabbitSubscriber(
            connection,
            scopeFactory,
            RabbitRelational.Work(queueName),
            typeof(TestWorkMessage),
            typeof(TestWorkMessageHandler),
            prefetchCount: 1,
            messageContextPropagator: messageContext);

        await subscriber.StartAsync();

        var expected = new TestWorkMessage
        {
            Id = Guid.NewGuid().ToString("N"),
            Payload = "user-context"
        };

        var identity = new ClaimsIdentity(authenticationType: "Test");
        identity.AddClaim(new Claim(TestWorkMessageHandler.SubjectClaimType, expectedUserId.ToString()));
        identity.AddClaim(new Claim(AbpClaimTypes.TenantId, expectedTenantId.ToString()));
        var principal = new ClaimsPrincipal(identity);

        using (principalAccessor.Change(principal))
        using (currentTenant.Change(expectedTenantId))
        using (var publisher = ServiceProvider.CreateWorkPublisher(queueName))
        {
            await publisher.PublishAsync(expected);
        }

        var (actual, captured) = await waiter.Task.WaitAsync(TimeSpan.FromSeconds(15));

        actual.Id.ShouldBe(expected.Id);
        captured.UserId.ShouldBe(expectedUserId);
        captured.TenantId.ShouldBe(expectedTenantId);
        captured.IsAuthenticated.ShouldBeTrue();
    }
}
