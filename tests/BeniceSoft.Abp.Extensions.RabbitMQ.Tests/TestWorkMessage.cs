using System.Collections.Concurrent;
using RabbitMQ.Client;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;

namespace BeniceSoft.Abp.Extensions.RabbitMQ.Tests;

public sealed class TestWorkMessage
{
    public string Id { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;
}

public sealed class CapturedUserContext
{
    public long? UserId { get; init; }

    public Guid? TenantId { get; init; }

    public bool IsAuthenticated { get; init; }
}

public sealed class ReceivedMessageCollector
{
    private readonly ConcurrentQueue<TaskCompletionSource<(TestWorkMessage Message, CapturedUserContext UserContext)>> _waiters = new();

    public void Reset()
    {
        while (_waiters.TryDequeue(out var waiter))
        {
            waiter.TrySetCanceled();
        }
    }

    public TaskCompletionSource<(TestWorkMessage Message, CapturedUserContext UserContext)> ExpectOne()
    {
        var tcs = new TaskCompletionSource<(TestWorkMessage, CapturedUserContext)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _waiters.Enqueue(tcs);
        return tcs;
    }

    public void Collect(TestWorkMessage message, CapturedUserContext userContext)
    {
        if (_waiters.TryDequeue(out var waiter))
        {
            waiter.TrySetResult((message, userContext));
        }
    }
}

public sealed class TestWorkMessageHandler : IRabbitMessageHandler<TestWorkMessage>
{
    public const string SubjectClaimType = "sub";

    private readonly ReceivedMessageCollector _collector;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public TestWorkMessageHandler(
        ReceivedMessageCollector collector,
        ICurrentPrincipalAccessor principalAccessor,
        ICurrentTenant currentTenant)
    {
        _collector = collector;
        _principalAccessor = principalAccessor;
        _currentTenant = currentTenant;
    }

    public Task<RabbitMessageResult> HandleAsync(
        TestWorkMessage message,
        IReadOnlyBasicProperties properties,
        CancellationToken cancellationToken = default)
    {
        var subject = _principalAccessor.Principal?.FindFirst(SubjectClaimType)?.Value;
        long? userId = null;
        if (long.TryParse(subject, out var parsed))
        {
            userId = parsed;
        }

        _collector.Collect(message, new CapturedUserContext
        {
            UserId = userId,
            TenantId = _currentTenant.Id,
            IsAuthenticated = _principalAccessor.Principal?.Identity?.IsAuthenticated ?? false
        });

        return Task.FromResult(RabbitMessageResult.Success);
    }
}
