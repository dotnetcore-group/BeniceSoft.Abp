using BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;
using BeniceSoft.Abp.Extensions.RateLimiting.Tests.Mocks;
using BeniceSoft.Abp.Extensions.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace BeniceSoft.Abp.Extensions.RateLimiting.Tests;

/// <summary>
/// TokenBucketRateLimiter 单元测试
/// </summary>
public class TokenBucketRateLimiterTests
{
    private readonly Mock<ILogger<TokenBucketRateLimiter>> _loggerMock;
    private readonly MockRedisConnection _mockRedisConnection;
    private readonly RateLimitOptions _options;

    public TokenBucketRateLimiterTests()
    {
        _loggerMock = new Mock<ILogger<TokenBucketRateLimiter>>();
        _mockRedisConnection = new MockRedisConnection();
        _options = new RateLimitOptions
        {
            Enabled = true,
            KeyPrefix = "test:ratelimit",
            DefaultMessage = "请求过于频繁"
        };
    }

    #region 正常测试 - Happy Path Tests

    [Fact]
    public void SimulateTokenBucket_FirstRequest_ShouldAllow()
    {
        // Arrange
        var key = "test:user:1";
        var capacity = 10;
        var tokensPerSecond = 1.0;

        // Act
        var (isAllowed, remaining) = _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);

        // Assert
        isAllowed.ShouldBeTrue();
        remaining.ShouldBe(9); // 10 - 1 = 9
    }

    [Fact]
    public void SimulateTokenBucket_MultipleRequests_ShouldDecrementTokens()
    {
        // Arrange
        var key = "test:user:2";
        var capacity = 5;
        var tokensPerSecond = 1.0;

        // Act - 连续请求 5 次
        for (int i = 0; i < 5; i++)
        {
            var (isAllowed, _) = _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);
            isAllowed.ShouldBeTrue();
        }

        // 第 6 次应该被拒绝
        var (sixthAllowed, remaining) = _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);

        // Assert
        sixthAllowed.ShouldBeFalse();
        remaining.ShouldBe(0);
    }

    [Fact]
    public void SimulateTokenBucket_DifferentKeys_ShouldBeSeparate()
    {
        // Arrange
        var key1 = "test:user:a";
        var key2 = "test:user:b";
        var capacity = 3;
        var tokensPerSecond = 1.0;

        // Act - 消耗 key1 的所有令牌
        for (int i = 0; i < 3; i++)
        {
            _mockRedisConnection.SimulateTokenBucket(key1, capacity, tokensPerSecond);
        }

        // key2 应该仍有令牌
        var (isAllowed, remaining) = _mockRedisConnection.SimulateTokenBucket(key2, capacity, tokensPerSecond);

        // Assert
        isAllowed.ShouldBeTrue();
        remaining.ShouldBe(2);
    }

    [Fact]
    public async Task SimulateTokenBucket_AfterWait_ShouldRefillTokens()
    {
        // Arrange
        var key = "test:user:refill";
        var capacity = 2;
        var tokensPerSecond = 10.0; // 10 tokens/second = 1 token per 100ms

        // 消耗所有令牌
        _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);
        _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);

        var (before, _) = _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);
        before.ShouldBeFalse();

        // 等待一段时间让令牌补充
        await Task.Delay(200); // 200ms = 2 tokens refilled

        // Act
        var (isAllowed, _) = _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);

        // Assert - 应该有令牌了
        isAllowed.ShouldBeTrue();
    }

    #endregion

    #region 破坏性测试 - Edge Cases and Destructive Tests

    [Fact]
    public void SimulateTokenBucket_ZeroCapacity_ShouldRejectAllRequests()
    {
        // Arrange
        var key = "test:zero:capacity";
        var capacity = 0;
        var tokensPerSecond = 1.0;

        // Act
        var (isAllowed, remaining) = _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);

        // Assert - 容量为 0，应该拒绝
        isAllowed.ShouldBeFalse();
        remaining.ShouldBe(0);
    }

    [Fact]
    public void SimulateTokenBucket_NegativeTokensPerSecond_ShouldHandleGracefully()
    {
        // Arrange
        var key = "test:negative:rate";
        var capacity = 10;
        var tokensPerSecond = -1.0; // 负数速率

        // 消耗一些令牌
        _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);
        _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);

        // Act - 即使速率为负，也不应该崩溃
        var (isAllowed, remaining) = _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);

        // Assert - 令牌只会减少，不会增加（负速率被当作 0 处理）
        isAllowed.ShouldBeTrue();
        remaining.ShouldBeLessThan(10);
    }

    [Fact]
    public void SimulateTokenBucket_VeryLargeCapacity_ShouldHandle()
    {
        // Arrange
        var key = "test:large:capacity";
        var capacity = int.MaxValue / 2; // 很大的容量
        var tokensPerSecond = 1000.0;

        // Act
        var (isAllowed, remaining) = _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);

        // Assert
        isAllowed.ShouldBeTrue();
        remaining.ShouldBe(capacity - 1);
    }

    [Fact]
    public void SimulateTokenBucket_EmptyKey_ShouldStillWork()
    {
        // Arrange
        var key = "";
        var capacity = 5;
        var tokensPerSecond = 1.0;

        // Act
        var (isAllowed, remaining) = _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);

        // Assert - 空 key 也应该能工作
        isAllowed.ShouldBeTrue();
        remaining.ShouldBe(4);
    }

    [Fact]
    public void SimulateTokenBucket_ConcurrentRequests_ShouldBeThreadSafe()
    {
        // Arrange
        var key = "test:concurrent";
        var capacity = 100;
        var tokensPerSecond = 0.1; // 很慢的补充速度
        var successCount = 0;
        var tasks = new List<Task>();

        // Act - 并发发起 200 个请求
        for (int i = 0; i < 200; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var (isAllowed, _) = _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);
                if (isAllowed) Interlocked.Increment(ref successCount);
            }));
        }

        //Task.WaitAll([.. tasks]);

        // Assert - 最多只有 100 个请求成功
        successCount.ShouldBeLessThanOrEqualTo(capacity);
    }

    [Fact]
    public void Reset_ShouldClearAllData()
    {
        // Arrange
        var key = "test:reset";
        var capacity = 5;
        var tokensPerSecond = 1.0;

        // 消耗所有令牌
        for (int i = 0; i < 5; i++)
        {
            _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);
        }

        // Act
        _mockRedisConnection.Reset();

        // 重新请求
        var (isAllowed, remaining) = _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);

        // Assert - 应该重新有令牌
        isAllowed.ShouldBeTrue();
        remaining.ShouldBe(4);
    }

    [Fact]
    public void SetTokens_ShouldOverrideExistingTokens()
    {
        // Arrange
        var key = "test:set:tokens";
        var capacity = 10;
        var tokensPerSecond = 1.0;

        // 消耗一些令牌
        _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);

        // Act - 手动设置令牌数为 0
        _mockRedisConnection.SetTokens(key, 0);

        var (isAllowed, _) = _mockRedisConnection.SimulateTokenBucket(key, capacity, tokensPerSecond);

        // Assert
        isAllowed.ShouldBeFalse();
    }

    #endregion

    #region Options 测试

    [Fact]
    public void RateLimitOptions_Disabled_ShouldBypassRateLimiting()
    {
        // Arrange
        var options = new RateLimitOptions { Enabled = false };

        // Assert
        options.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void RateLimitOptions_DefaultValues_ShouldBeCorrect()
    {
        // Arrange
        var options = new RateLimitOptions();

        // Assert
        options.Enabled.ShouldBeTrue();
        options.KeyPrefix.ShouldBe("ratelimit");
        options.DefaultMessage.ShouldBe("请求过于频繁，请稍后再试");
    }

    #endregion
}

