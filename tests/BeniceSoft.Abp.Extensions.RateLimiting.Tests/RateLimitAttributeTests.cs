using BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;
using Shouldly;
using Xunit;

namespace BeniceSoft.Abp.Extensions.RateLimiting.Tests;

/// <summary>
/// RateLimitAttribute 单元测试
/// </summary>
public class RateLimitAttributeTests
{
    #region 正常测试 - Happy Path Tests

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var attribute = new RateLimitAttribute();

        // Assert
        attribute.LimitBy.ShouldBe(RateLimitBy.Ip);
        attribute.PermitLimit.ShouldBe(100);
        attribute.WindowSeconds.ShouldBe(60);
        attribute.ThrowOnExceeded.ShouldBeTrue();
        attribute.Key.ShouldBeNull();
        attribute.Message.ShouldBeNull();
    }

    [Theory]
    [InlineData(RateLimitBy.Ip)]
    [InlineData(RateLimitBy.UserId)]
    [InlineData(RateLimitBy.TenantId)]
    [InlineData(RateLimitBy.Custom)]
    [InlineData(RateLimitBy.Global)]
    public void LimitBy_AllEnumValues_ShouldBeSettable(RateLimitBy limitBy)
    {
        // Arrange & Act
        var attribute = new RateLimitAttribute { LimitBy = limitBy };

        // Assert
        attribute.LimitBy.ShouldBe(limitBy);
    }

    [Fact]
    public void PermitLimit_ShouldBeSettable()
    {
        // Arrange & Act
        var attribute = new RateLimitAttribute { PermitLimit = 5 };

        // Assert
        attribute.PermitLimit.ShouldBe(5);
    }

    [Fact]
    public void WindowSeconds_ShouldBeSettable()
    {
        // Arrange & Act
        var attribute = new RateLimitAttribute { WindowSeconds = 30 };

        // Assert
        attribute.WindowSeconds.ShouldBe(30);
    }

    [Fact]
    public void ThrowOnExceeded_CanBeDisabled()
    {
        // Arrange & Act
        var attribute = new RateLimitAttribute { ThrowOnExceeded = false };

        // Assert
        attribute.ThrowOnExceeded.ShouldBeFalse();
    }

    #endregion

    #region 边界测试 - Edge Cases

    [Fact]
    public void CustomKey_WithPlaceholders_ShouldAcceptAnyString()
    {
        // Arrange
        var attribute = new RateLimitAttribute
        {
            LimitBy = RateLimitBy.Custom,
            Key = "sms:{phone}:{userId}"
        };

        // Assert
        attribute.Key.ShouldBe("sms:{phone}:{userId}");
    }

    [Fact]
    public void Message_CustomMessage_ShouldBeStored()
    {
        // Arrange
        var customMessage = "自定义限流消息: 请等待 {0} 秒";
        var attribute = new RateLimitAttribute
        {
            Message = customMessage
        };

        // Assert
        attribute.Message.ShouldBe(customMessage);
    }

    #endregion
}

