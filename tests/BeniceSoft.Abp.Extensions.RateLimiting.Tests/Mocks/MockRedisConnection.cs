using BeniceSoft.Abp.Extensions.Redis;
using Moq;
using StackExchange.Redis;

namespace BeniceSoft.Abp.Extensions.RateLimiting.Tests.Mocks;

/// <summary>
/// Mock Redis 连接，用于模拟令牌桶算法的行为
/// </summary>
public class MockRedisConnection : IRedisConnection
{
    private readonly Dictionary<string, Dictionary<string, double>> _hashData = new();
    private readonly object _lock = new();

    public IConnectionMultiplexer TryConnect()
    {
        var mockMultiplexer = new Mock<IConnectionMultiplexer>();
        var mockDatabase = CreateMockDatabase();
        
        mockMultiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(mockDatabase.Object);
        mockMultiplexer.Setup(x => x.GetEndPoints(It.IsAny<bool>()))
            .Returns(new System.Net.EndPoint[] { new System.Net.IPEndPoint(0, 0) });
        
        var mockServer = new Mock<IServer>();
        mockMultiplexer.Setup(x => x.GetServer(It.IsAny<System.Net.EndPoint>(), It.IsAny<object>()))
            .Returns(mockServer.Object);

        return mockMultiplexer.Object;
    }

    private Mock<IDatabase> CreateMockDatabase()
    {
        var mockDb = new Mock<IDatabase>();
        
        // 不直接 Mock ScriptEvaluateAsync，因为我们使用 LoadedLuaScript.EvaluateAsync
        // 这里主要用于测试基础设施
        
        return mockDb;
    }

    /// <summary>
    /// 模拟获取令牌（用于不依赖 Redis 的测试）
    /// </summary>
    public (bool isAllowed, long remaining) SimulateTokenBucket(
        string key, 
        int capacity, 
        double tokensPerSecond, 
        int requested = 1)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            
            if (!_hashData.TryGetValue(key, out var bucket))
            {
                bucket = new Dictionary<string, double>
                {
                    ["tokens"] = capacity,
                    ["last_time"] = now
                };
                _hashData[key] = bucket;
            }

            var tokens = bucket["tokens"];
            var lastTime = bucket["last_time"];

            // 计算补充的令牌
            var elapsed = (now - lastTime) / 1000.0;
            var refill = elapsed * tokensPerSecond;
            tokens = Math.Min(capacity, tokens + refill);

            // 检查是否有足够令牌
            if (tokens >= requested)
            {
                tokens -= requested;
                bucket["tokens"] = tokens;
                bucket["last_time"] = now;
                return (true, (long)Math.Floor(tokens));
            }
            else
            {
                bucket["tokens"] = tokens;
                bucket["last_time"] = now;
                return (false, (long)Math.Floor(tokens));
            }
        }
    }

    /// <summary>
    /// 重置所有数据
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _hashData.Clear();
        }
    }

    /// <summary>
    /// 设置指定 key 的令牌数
    /// </summary>
    public void SetTokens(string key, double tokens)
    {
        lock (_lock)
        {
            if (!_hashData.TryGetValue(key, out var bucket))
            {
                bucket = new Dictionary<string, double>();
                _hashData[key] = bucket;
            }
            bucket["tokens"] = tokens;
            bucket["last_time"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    #region IRedisConnection 接口实现（测试用 Mock 方法）

    public void Subscribe(string channel, Action<string, string> handler, bool pattern = false)
    {
        // Mock 实现，不做任何操作
    }

    public Task SubscribeAsync(string channel, Action<string, string> handler, bool pattern = false)
    {
        return Task.CompletedTask;
    }

    public void Unsubscribe(string channel, Action<string, string>? handler = null, bool pattern = false)
    {
        // Mock 实现，不做任何操作
    }

    public Task UnsubscribeAsync(string channel, Action<string, string>? handler = null, bool pattern = false)
    {
        return Task.CompletedTask;
    }

    public void UnsubscribeAll()
    {
        // Mock 实现，不做任何操作
    }

    public Task UnsubscribeAllAsync()
    {
        return Task.CompletedTask;
    }

    public long Publish(string channel, string message, bool pattern = false)
    {
        return 0;
    }

    public Task<long> PublishAsync(string channel, string message, bool pattern = false)
    {
        return Task.FromResult(0L);
    }

    public void Dispose()
    {
        // Mock 实现，清理数据
        Reset();
    }

    #endregion
}

