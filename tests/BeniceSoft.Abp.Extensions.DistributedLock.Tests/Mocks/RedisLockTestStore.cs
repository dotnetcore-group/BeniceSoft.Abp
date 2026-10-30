using System.Text.Json;
using BeniceSoft.Abp.Extensions.Redis;
using Moq;
using StackExchange.Redis;

namespace BeniceSoft.Abp.Extensions.DistributedLock.Tests.Mocks;

/// <summary>
/// 替换 Redis 命令边界，测试实际 Provider、RedisLock 和工作单元的协作
/// </summary>
public sealed class RedisLockTestStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, RedisValue> _owners = new();
    private int _attempts;
    private int _renewals;
    public int Attempts => Volatile.Read(ref _attempts);
    public int Renewals => Volatile.Read(ref _renewals);
    public RedisClient Client { get; }
    public Func<Task>? BeforeSetAsync { get; set; }
    public Action? AfterSet { get; set; }
    public TaskCompletionSource Renewed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public RedisLockTestStore()
    {
        var database = new Mock<IDatabase>();
        database.Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), false, When.NotExists, It.IsAny<CommandFlags>()))
            .Returns(async (RedisKey key, RedisValue value, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags) =>
            {
                Interlocked.Increment(ref _attempts);
                if (BeforeSetAsync != null)
                {
                    await BeforeSetAsync();
                }

                bool added;
                lock (_gate)
                {
                    added = _owners.TryAdd(key.ToString(), value);
                }

                AfterSet?.Invoke();
                return added;
            });
        database.Setup(x => x.StringSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), false, When.NotExists, It.IsAny<CommandFlags>()))
            .Returns((RedisKey key, RedisValue value, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags) =>
            {
                Interlocked.Increment(ref _attempts);
                lock (_gate)
                {
                    return _owners.TryAdd(key.ToString(), value);
                }
            });
        database.Setup(x => x.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .Returns((string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags) =>
                Task.FromResult(Evaluate(keys, values)));
        database.Setup(x => x.ScriptEvaluate(It.IsAny<string>(), It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .Returns((string script, RedisKey[] keys, RedisValue[] values, CommandFlags flags) => Evaluate(keys, values));
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        multiplexer.Setup(x => x.GetEndPoints(It.IsAny<bool>())).Returns(Array.Empty<System.Net.EndPoint>());
        var connection = new Mock<IRedisConnection>();
        connection.Setup(x => x.TryConnect()).Returns(multiplexer.Object);
        Client = new RedisClient(connection.Object, 0);
    }

    public string? Owner(string resourceId)
    {
        lock (_gate)
        {
            return _owners.TryGetValue("lock:" + resourceId, out var owner)
                ? JsonSerializer.Deserialize<string>(owner.ToString())
                : null;
        }
    }

    public void Expire(string resourceId)
    {
        lock (_gate)
        {
            _owners.Remove("lock:" + resourceId);
        }
    }

    private RedisResult Evaluate(RedisKey[] keys, RedisValue[] values)
    {
        lock (_gate)
        {
            var key = keys[0].ToString();
            var matches = _owners.TryGetValue(key, out var owner) && owner == values[0];
            if (values.Length == 1)
            {
                return RedisResult.Create(matches && _owners.Remove(key) ? 1L : 0L);
            }

            Interlocked.Increment(ref _renewals);
            Renewed.TrySetResult();
            return RedisResult.Create(matches ? 1L : -1L);
        }
    }
}
