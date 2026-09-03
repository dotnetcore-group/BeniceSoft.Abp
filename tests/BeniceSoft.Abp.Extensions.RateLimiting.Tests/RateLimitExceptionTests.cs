using BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;
using Shouldly;
using Xunit;

namespace BeniceSoft.Abp.Extensions.RateLimiting.Tests;

/// <summary>
/// RateLimitExceededException 单元测试
/// </summary>
public class RateLimitExceptionTests
{
    #region 正常测试 - Happy Path Tests

    [Fact]
    public void Constructor_WithAllParameters_ShouldSetAllProperties()
    {
        // Arrange
        var key = "test:user:123";
        var limit = 100L;
        var retryAfterSeconds = 30;
        var message = "自定义限流消息";

        // Act
        var exception = new RateLimitExceededException(key, limit, retryAfterSeconds, message);

        // Assert
        exception.Key.ShouldBe(key);
        exception.Limit.ShouldBe(limit);
        exception.RetryAfterSeconds.ShouldBe(retryAfterSeconds);
        exception.Message.ShouldBe(message);
        exception.Remaining.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WithoutMessage_ShouldUseDefaultMessage()
    {
        // Arrange
        var key = "test:user:456";
        var limit = 50L;
        var retryAfterSeconds = 10;

        // Act
        var exception = new RateLimitExceededException(key, limit, retryAfterSeconds);

        // Assert
        exception.Message.ShouldContain("请求过于频繁");
        exception.Message.ShouldContain("10");
        exception.Message.ShouldContain("秒后再试");
    }

    [Fact]
    public void Constructor_WithNullMessage_ShouldUseDefaultMessage()
    {
        // Arrange
        var key = "test:user:789";
        var limit = 25L;
        var retryAfterSeconds = 5;

        // Act
        var exception = new RateLimitExceededException(key, limit, retryAfterSeconds, null);

        // Assert
        exception.Message.ShouldNotBeNullOrWhiteSpace();
        exception.Message.ShouldContain("5");
    }

    [Fact]
    public void Remaining_ShouldAlwaysBeZero()
    {
        // Arrange & Act
        var exception = new RateLimitExceededException("key", 100, 30);

        // Assert - 被拒绝时剩余配额总是 0
        exception.Remaining.ShouldBe(0);
    }

    [Fact]
    public void Exception_ShouldBeSerializableAsExpected()
    {
        // Arrange
        var exception = new RateLimitExceededException("key", 100, 30, "测试消息");

        // Assert - 继承自 Exception
        exception.ShouldBeAssignableTo<Exception>();
    }

    #endregion

    #region 边界测试 - Edge Cases

    [Fact]
    public void Constructor_WithZeroRetryAfter_ShouldBeAllowed()
    {
        // Arrange & Act
        var exception = new RateLimitExceededException("key", 100, 0);

        // Assert
        exception.RetryAfterSeconds.ShouldBe(0);
        exception.Message.ShouldContain("0");
    }

    [Fact]
    public void Constructor_WithZeroLimit_ShouldBeAllowed()
    {
        // Arrange & Act
        var exception = new RateLimitExceededException("key", 0, 30);

        // Assert
        exception.Limit.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WithEmptyKey_ShouldBeAllowed()
    {
        // Arrange & Act
        var exception = new RateLimitExceededException("", 100, 30);

        // Assert
        exception.Key.ShouldBe("");
    }

    [Fact]
    public void Constructor_WithLargeValues_ShouldHandleCorrectly()
    {
        // Arrange
        var largeLimit = long.MaxValue;
        var largeRetry = int.MaxValue;

        // Act
        var exception = new RateLimitExceededException("key", largeLimit, largeRetry);

        // Assert
        exception.Limit.ShouldBe(largeLimit);
        exception.RetryAfterSeconds.ShouldBe(largeRetry);
    }

    [Fact]
    public void Constructor_WithUnicodeKey_ShouldPreserveKey()
    {
        // Arrange
        var unicodeKey = "限流:用户:张三";

        // Act
        var exception = new RateLimitExceededException(unicodeKey, 100, 30);

        // Assert
        exception.Key.ShouldBe(unicodeKey);
    }

    [Fact]
    public void Constructor_WithSpecialCharactersInMessage_ShouldPreserveMessage()
    {
        // Arrange
        var specialMessage = "请求过于频繁！请在 {0} 秒后重试。\n\t测试特殊字符：<>&\"'";

        // Act
        var exception = new RateLimitExceededException("key", 100, 30, specialMessage);

        // Assert
        exception.Message.ShouldBe(specialMessage);
    }

    #endregion
}

