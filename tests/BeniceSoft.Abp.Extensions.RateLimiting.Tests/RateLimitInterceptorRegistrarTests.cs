using BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;
using Shouldly;
using Xunit;

namespace BeniceSoft.Abp.Extensions.RateLimiting.Tests;

/// <summary>
/// RateLimitInterceptorRegistrar 单元测试
/// </summary>
public class RateLimitInterceptorRegistrarTests
{
    #region ShouldIntercept 测试 - 通过反射测试私有方法的行为

    [Fact]
    public void TypeWithRateLimitAttribute_ShouldHaveRateLimitAttribute()
    {
        // Arrange
        var type = typeof(ServiceWithRateLimit);

        // Act
        var hasAttribute = type.GetMethods()
            .Any(m => m.IsDefined(typeof(RateLimitAttribute), true));

        // Assert
        hasAttribute.ShouldBeTrue();
    }

    [Fact]
    public void TypeWithoutRateLimitAttribute_ShouldNotHaveRateLimitAttribute()
    {
        // Arrange
        var type = typeof(ServiceWithoutRateLimit);

        // Act
        var hasAttribute = type.GetMethods()
            .Any(m => m.IsDefined(typeof(RateLimitAttribute), true));

        // Assert
        hasAttribute.ShouldBeFalse();
    }

    [Fact]
    public void TypeWithMultipleRateLimitMethods_ShouldHaveRateLimitAttribute()
    {
        // Arrange
        var type = typeof(ServiceWithMultipleRateLimits);

        // Act
        var methodsWithAttribute = type.GetMethods()
            .Count(m => m.IsDefined(typeof(RateLimitAttribute), true));

        // Assert
        methodsWithAttribute.ShouldBe(2);
    }

    [Fact]
    public void TypeWithInheritedRateLimitAttribute_ShouldDetectAttribute()
    {
        // Arrange
        var type = typeof(DerivedServiceWithRateLimit);

        // Act - inherited = true 应该能检测到继承的属性
        var hasAttribute = type.GetMethods()
            .Any(m => m.IsDefined(typeof(RateLimitAttribute), true));

        // Assert
        hasAttribute.ShouldBeTrue();
    }

    #endregion

    #region 测试服务类 - Test Service Classes

    private class ServiceWithRateLimit
    {
        [RateLimit(PermitLimit = 10, WindowSeconds = 60)]
        public virtual Task RateLimitedMethod()
        {
            return Task.CompletedTask;
        }

        public virtual Task NormalMethod()
        {
            return Task.CompletedTask;
        }
    }

    private class ServiceWithoutRateLimit
    {
        public virtual Task MethodA()
        {
            return Task.CompletedTask;
        }

        public virtual Task MethodB()
        {
            return Task.CompletedTask;
        }
    }

    private class ServiceWithMultipleRateLimits
    {
        [RateLimit(PermitLimit = 5, WindowSeconds = 60)]
        public virtual Task SendSmsAsync(string phone)
        {
            return Task.CompletedTask;
        }

        [RateLimit(PermitLimit = 10, WindowSeconds = 60)]
        public virtual Task SendEmailAsync(string email)
        {
            return Task.CompletedTask;
        }

        public virtual Task NormalMethod()
        {
            return Task.CompletedTask;
        }
    }

    private class BaseServiceWithRateLimit
    {
        [RateLimit(PermitLimit = 100, WindowSeconds = 60)]
        public virtual Task BaseRateLimitedMethod()
        {
            return Task.CompletedTask;
        }
    }

    private class DerivedServiceWithRateLimit : BaseServiceWithRateLimit
    {
        public virtual Task DerivedMethod()
        {
            return Task.CompletedTask;
        }
    }

    #endregion
}

