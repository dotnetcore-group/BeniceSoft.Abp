using BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;
using Shouldly;
using Xunit;

namespace BeniceSoft.Abp.Extensions.RateLimiting.Tests;

/// <summary>
/// RateLimitResult 单元测试
/// </summary>
public class RateLimitResultTests
{
    #region Allowed 工厂方法测试

    [Fact]
    public void Allowed_ShouldSetIsAllowedToTrue()
    {
        // Arrange & Act
        var result = RateLimitResult.Allowed(50, 100);

        // Assert
        result.IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public void Allowed_ShouldSetRemainingCorrectly()
    {
        // Arrange & Act
        var result = RateLimitResult.Allowed(75, 100);

        // Assert
        result.Remaining.ShouldBe(75);
    }

    [Fact]
    public void Allowed_ShouldSetLimitCorrectly()
    {
        // Arrange & Act
        var result = RateLimitResult.Allowed(50, 100);

        // Assert
        result.Limit.ShouldBe(100);
    }

    [Fact]
    public void Allowed_ShouldSetRetryAfterSecondsToZero()
    {
        // Arrange & Act
        var result = RateLimitResult.Allowed(50, 100);

        // Assert
        result.RetryAfterSeconds.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 100)]
    [InlineData(99, 100)]
    [InlineData(1, 1)]
    public void Allowed_WithVariousValues_ShouldWork(long remaining, long limit)
    {
        // Arrange & Act
        var result = RateLimitResult.Allowed(remaining, limit);

        // Assert
        result.IsAllowed.ShouldBeTrue();
        result.Remaining.ShouldBe(remaining);
        result.Limit.ShouldBe(limit);
    }

    #endregion

    #region Rejected 工厂方法测试

    [Fact]
    public void Rejected_ShouldSetIsAllowedToFalse()
    {
        // Arrange & Act
        var result = RateLimitResult.Rejected(30, 100);

        // Assert
        result.IsAllowed.ShouldBeFalse();
    }

    [Fact]
    public void Rejected_ShouldSetRemainingToZero()
    {
        // Arrange & Act
        var result = RateLimitResult.Rejected(30, 100);

        // Assert
        result.Remaining.ShouldBe(0);
    }

    [Fact]
    public void Rejected_ShouldSetRetryAfterSecondsCorrectly()
    {
        // Arrange & Act
        var result = RateLimitResult.Rejected(45, 100);

        // Assert
        result.RetryAfterSeconds.ShouldBe(45);
    }

    [Fact]
    public void Rejected_ShouldSetLimitCorrectly()
    {
        // Arrange & Act
        var result = RateLimitResult.Rejected(30, 200);

        // Assert
        result.Limit.ShouldBe(200);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(60, 100)]
    [InlineData(3600, 1000)]
    [InlineData(0, 50)]
    public void Rejected_WithVariousRetryValues_ShouldWork(int retryAfter, long limit)
    {
        // Arrange & Act
        var result = RateLimitResult.Rejected(retryAfter, limit);

        // Assert
        result.IsAllowed.ShouldBeFalse();
        result.RetryAfterSeconds.ShouldBe(retryAfter);
        result.Limit.ShouldBe(limit);
        result.Remaining.ShouldBe(0);
    }

    #endregion

    #region 边界测试 - Edge Cases

    [Fact]
    public void Allowed_WithZeroRemaining_ShouldStillBeAllowed()
    {
        // 当剩余为0但仍被允许（刚好用完最后一个令牌）
        // Arrange & Act
        var result = RateLimitResult.Allowed(0, 100);

        // Assert
        result.IsAllowed.ShouldBeTrue();
        result.Remaining.ShouldBe(0);
    }

    [Fact]
    public void Allowed_WithLargeValues_ShouldHandleCorrectly()
    {
        // Arrange & Act
        var result = RateLimitResult.Allowed(long.MaxValue - 1, long.MaxValue);

        // Assert
        result.Remaining.ShouldBe(long.MaxValue - 1);
        result.Limit.ShouldBe(long.MaxValue);
    }

    [Fact]
    public void Rejected_WithZeroRetryAfter_ShouldBeValid()
    {
        // Arrange & Act
        var result = RateLimitResult.Rejected(0, 100);

        // Assert
        result.IsAllowed.ShouldBeFalse();
        result.RetryAfterSeconds.ShouldBe(0);
    }

    #endregion
}

