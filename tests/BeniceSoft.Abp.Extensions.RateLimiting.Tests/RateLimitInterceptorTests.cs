using System.Reflection;
using BeniceSoft.Abp.Core.Users;
using BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Volo.Abp.DynamicProxy;
using Xunit;

namespace BeniceSoft.Abp.Extensions.RateLimiting.Tests;

/// <summary>
/// RateLimitInterceptor 单元测试
/// </summary>
public class RateLimitInterceptorTests
{
    private readonly Mock<ILogger<RateLimitInterceptor>> _loggerMock;
    private readonly Mock<IRateLimiter> _rateLimiterMock;
    private readonly Mock<IBeniceSoftCurrentUser> _currentUserMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly RateLimitOptions _options;
    private readonly RateLimitInterceptor _interceptor;

    public RateLimitInterceptorTests()
    {
        _loggerMock = new Mock<ILogger<RateLimitInterceptor>>();
        _rateLimiterMock = new Mock<IRateLimiter>();
        _currentUserMock = new Mock<IBeniceSoftCurrentUser>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _options = new RateLimitOptions
        {
            Enabled = true,
            KeyPrefix = "test:ratelimit",
            DefaultMessage = "请求过于频繁"
        };

        _interceptor = new RateLimitInterceptor(
            _loggerMock.Object,
            _rateLimiterMock.Object,
            _currentUserMock.Object,
            _httpContextAccessorMock.Object,
            Options.Create(_options));
    }

    #region 正常测试 - Happy Path Tests

    [Fact]
    public async Task InterceptAsync_WithoutAttribute_ShouldProceedDirectly()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithoutRateLimit))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert - 不应调用限流器
        _rateLimiterMock.Verify(
            x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        invocationMock.Verify(x => x.ProceedAsync(), Times.Once);
    }

    [Fact]
    public async Task InterceptAsync_WhenDisabled_ShouldProceedDirectly()
    {
        // Arrange
        var disabledOptions = new RateLimitOptions { Enabled = false };
        var interceptor = new RateLimitInterceptor(
            _loggerMock.Object,
            _rateLimiterMock.Object,
            _currentUserMock.Object,
            _httpContextAccessorMock.Object,
            Options.Create(disabledOptions));

        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithRateLimit))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        // Act
        await interceptor.InterceptAsync(invocationMock.Object);

        // Assert - 不应调用限流器
        _rateLimiterMock.Verify(
            x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        invocationMock.Verify(x => x.ProceedAsync(), Times.Once);
    }

    [Fact]
    public async Task InterceptAsync_WhenAllowed_ShouldProceed()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithRateLimit))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        _rateLimiterMock
            .Setup(x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateLimitResult.Allowed(9, 10));

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        _rateLimiterMock.Verify(
            x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
        invocationMock.Verify(x => x.ProceedAsync(), Times.Once);
    }

    [Fact]
    public async Task InterceptAsync_WhenRejected_ThrowOnExceededTrue_ShouldThrowException()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithRateLimit))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        _rateLimiterMock
            .Setup(x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateLimitResult.Rejected(5, 10));

        // Act & Assert
        var exception = await Should.ThrowAsync<RateLimitExceededException>(
            async () => await _interceptor.InterceptAsync(invocationMock.Object));

        exception.RetryAfterSeconds.ShouldBe(5);
        exception.Limit.ShouldBe(10);
    }

    [Fact]
    public async Task InterceptAsync_WhenRejected_ThrowOnExceededFalse_ShouldProceed()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithNoThrow))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        _rateLimiterMock
            .Setup(x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateLimitResult.Rejected(5, 10));

        // Act - 不应抛出异常，应继续执行
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        invocationMock.Verify(x => x.ProceedAsync(), Times.Once);
    }

    #endregion

    #region 限流维度测试 - LimitBy Dimension Tests

    [Fact]
    public async Task InterceptAsync_LimitByGlobal_ShouldUseGlobalKey()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithGlobalLimit))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        string? capturedKey = null;
        _rateLimiterMock
            .Setup(x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, int, CancellationToken>((key, _, _, _) => capturedKey = key)
            .ReturnsAsync(RateLimitResult.Allowed(9, 10));

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        capturedKey.ShouldNotBeNull();
        capturedKey.ShouldContain("global");
    }

    [Fact]
    public async Task InterceptAsync_LimitByUserId_ShouldUseUserIdInKey()
    {
        // Arrange
        _currentUserMock.Setup(x => x.Id).Returns(12345L);

        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithUserLimit))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        string? capturedKey = null;
        _rateLimiterMock
            .Setup(x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, int, CancellationToken>((key, _, _, _) => capturedKey = key)
            .ReturnsAsync(RateLimitResult.Allowed(9, 10));

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        capturedKey.ShouldNotBeNull();
        capturedKey.ShouldContain("user:12345");
    }

    [Fact]
    public async Task InterceptAsync_LimitByUserId_WhenNotAuthenticated_ShouldUseAnonymous()
    {
        // Arrange
        _currentUserMock.Setup(x => x.Id).Returns((long?)null);

        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithUserLimit))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        string? capturedKey = null;
        _rateLimiterMock
            .Setup(x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, int, CancellationToken>((key, _, _, _) => capturedKey = key)
            .ReturnsAsync(RateLimitResult.Allowed(9, 10));

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        capturedKey.ShouldNotBeNull();
        capturedKey.ShouldContain("user:anonymous");
    }

    [Fact]
    public async Task InterceptAsync_LimitByTenantId_ShouldUseTenantIdInKey()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        _currentUserMock.Setup(x => x.TenantId).Returns(tenantId);

        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithTenantLimit))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        string? capturedKey = null;
        _rateLimiterMock
            .Setup(x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, int, CancellationToken>((key, _, _, _) => capturedKey = key)
            .ReturnsAsync(RateLimitResult.Allowed(9, 10));

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        capturedKey.ShouldNotBeNull();
        capturedKey.ShouldContain($"tenant:{tenantId}");
    }

    [Fact]
    public async Task InterceptAsync_LimitByCustomKey_ShouldFormatCustomKey()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithCustomKey))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);
        invocationMock.Setup(x => x.ArgumentsDictionary).Returns(new Dictionary<string, object?>
        {
            { "phone", "13800138000" }
        });

        string? capturedKey = null;
        _rateLimiterMock
            .Setup(x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, int, CancellationToken>((key, _, _, _) => capturedKey = key)
            .ReturnsAsync(RateLimitResult.Allowed(9, 10));

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        capturedKey.ShouldNotBeNull();
        capturedKey.ShouldContain("sms:13800138000");
    }

    #endregion

    #region 自定义消息测试 - Custom Message Tests

    [Fact]
    public async Task InterceptAsync_WithCustomMessage_ShouldUseCustomMessage()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithCustomMessage))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        _rateLimiterMock
            .Setup(x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateLimitResult.Rejected(5, 10));

        // Act & Assert
        var exception = await Should.ThrowAsync<RateLimitExceededException>(
            async () => await _interceptor.InterceptAsync(invocationMock.Object));

        exception.Message.ShouldContain("短信发送太频繁");
    }

    [Fact]
    public async Task InterceptAsync_WithoutCustomMessage_ShouldUseDefaultMessage()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithRateLimit))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        _rateLimiterMock
            .Setup(x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RateLimitResult.Rejected(5, 10));

        // Act & Assert
        var exception = await Should.ThrowAsync<RateLimitExceededException>(
            async () => await _interceptor.InterceptAsync(invocationMock.Object));

        exception.Message.ShouldContain("请求过于频繁"); // 使用 options 中的默认消息
    }

    #endregion

    #region 测试服务类 - Test Service

    private class TestService
    {
        public virtual Task MethodWithoutRateLimit()
        {
            return Task.CompletedTask;
        }

        [RateLimit(PermitLimit = 10, WindowSeconds = 60)]
        public virtual Task MethodWithRateLimit()
        {
            return Task.CompletedTask;
        }

        [RateLimit(PermitLimit = 10, WindowSeconds = 60, ThrowOnExceeded = false)]
        public virtual Task MethodWithNoThrow()
        {
            return Task.CompletedTask;
        }

        [RateLimit(LimitBy = RateLimitBy.Global, PermitLimit = 100, WindowSeconds = 60)]
        public virtual Task MethodWithGlobalLimit()
        {
            return Task.CompletedTask;
        }

        [RateLimit(LimitBy = RateLimitBy.UserId, PermitLimit = 5, WindowSeconds = 60)]
        public virtual Task MethodWithUserLimit()
        {
            return Task.CompletedTask;
        }

        [RateLimit(LimitBy = RateLimitBy.TenantId, PermitLimit = 50, WindowSeconds = 60)]
        public virtual Task MethodWithTenantLimit()
        {
            return Task.CompletedTask;
        }

        [RateLimit(LimitBy = RateLimitBy.Custom, Key = "sms:{phone}", PermitLimit = 1, WindowSeconds = 60)]
        public virtual Task MethodWithCustomKey(string phone)
        {
            return Task.CompletedTask;
        }

        [RateLimit(PermitLimit = 5, WindowSeconds = 60, Message = "短信发送太频繁，请稍后再试")]
        public virtual Task MethodWithCustomMessage()
        {
            return Task.CompletedTask;
        }
    }

    #endregion
}

