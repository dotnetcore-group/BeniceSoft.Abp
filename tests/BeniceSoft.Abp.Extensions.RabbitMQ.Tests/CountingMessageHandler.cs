using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace BeniceSoft.Abp.Extensions.RabbitMQ.Tests;

/// <summary>Counts calls per message Id; Payload "throw" makes the handler fail.</summary>
public sealed class HandlerCallLog
{
    private readonly ConcurrentDictionary<string, int> _calls = new();

    public void Record(string id) => _calls.AddOrUpdate(id, 1, (_, count) => count + 1);

    public int Count(string id) => _calls.TryGetValue(id, out var count) ? count : 0;

    public async Task WaitForAsync(string id, int minCount, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (Count(id) < minCount)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Message {id} handled {Count(id)} time(s), expected {minCount}");
            }

            await Task.Delay(100, cancellationToken);
        }
    }
}

public sealed class CountingMessageHandler : IRabbitMessageHandler<TestWorkMessage>
{
    public const string ThrowPayload = "throw";

    private readonly HandlerCallLog _log;

    public CountingMessageHandler(HandlerCallLog log)
    {
        _log = log;
    }

    public Task<RabbitMessageResult> HandleAsync(
        TestWorkMessage message,
        IReadOnlyBasicProperties properties,
        CancellationToken cancellationToken = default)
    {
        _log.Record(message.Id);
        if (message.Payload == ThrowPayload)
        {
            throw new InvalidOperationException("simulated handler failure");
        }

        return Task.FromResult(RabbitMessageResult.Success);
    }
}