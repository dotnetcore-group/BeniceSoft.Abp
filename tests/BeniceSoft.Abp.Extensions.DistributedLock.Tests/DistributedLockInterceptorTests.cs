using System.Reflection;
using Microsoft.Extensions.Logging;
using Moq;
using BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;
using Shouldly;
using Volo.Abp.DynamicProxy;
using Xunit;

namespace BeniceSoft.Abp.Extensions.DistributedLock.Tests;

/// <summary>
/// Tests for DistributedLockInterceptor
/// </summary>
public class DistributedLockInterceptorTests
{
    private readonly Mock<ILogger<DistributedLockInterceptor>> _loggerMock;
    private readonly Mock<IDistributedLockProvider> _lockProviderMock;
    private readonly DistributedLockInterceptor _interceptor;

    public DistributedLockInterceptorTests()
    {
        _loggerMock = new Mock<ILogger<DistributedLockInterceptor>>();
        _lockProviderMock = new Mock<IDistributedLockProvider>();
        _interceptor = new DistributedLockInterceptor(_loggerMock.Object, _lockProviderMock.Object);
    }

    [Fact]
    public async Task InterceptAsync_WithoutAttribute_ShouldNotAcquireLock()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithoutLock))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert - 验证没有调用任何 AcquireAsync 重载
        _lockProviderMock.Verify(
            x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(),  It.IsAny<CancellationToken>()),
            Times.Never);
        invocationMock.Verify(x => x.ProceedAsync(), Times.Once);
    }

    [Fact]
    public async Task InterceptAsync_WithAttribute_ShouldAcquireAndReleaseLock()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithLock))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        // 新接口签名包含 autoRenew 参数
        _lockProviderMock
            .Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        _lockProviderMock.Verify(
            x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(),  false, It.IsAny<CancellationToken>()),
            Times.Once);
        _lockProviderMock.Verify(x => x.ReleaseLockAsync(It.IsAny<string>()), Times.Once);
        invocationMock.Verify(x => x.ProceedAsync(), Times.Once);
    }

    [Fact]
    public async Task InterceptAsync_WhenAcquireFails_ShouldThrowAndNotProceed()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithLock))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);

        _lockProviderMock
            .Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _interceptor.InterceptAsync(invocationMock.Object));

        invocationMock.Verify(x => x.ProceedAsync(), Times.Never);
        _lockProviderMock.Verify(x => x.ReleaseLockAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InterceptAsync_WhenMethodThrows_ShouldStillReleaseLock()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithLock))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).ThrowsAsync(new InvalidOperationException("Test exception"));

        _lockProviderMock
            .Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(),  false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await _interceptor.InterceptAsync(invocationMock.Object));

        // Lock should still be released even when exception occurs
        _lockProviderMock.Verify(x => x.ReleaseLockAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InterceptAsync_WithCustomResourceId_ShouldUseFormattedResourceId()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithCustomResourceId))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);
        invocationMock.Setup(x => x.ArgumentsDictionary).Returns(new Dictionary<string, object?>
        {
            { "id", 123 },
            { "name", "test" }
        });

        string? capturedResourceId = null;
        _lockProviderMock
            .Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), false, It.IsAny<CancellationToken>()))
            .Callback<string, TimeSpan, TimeSpan, TimeSpan, bool, bool, CancellationToken>((resourceId, _, _, _, _, _, _) => capturedResourceId = resourceId)
            .ReturnsAsync(true);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        capturedResourceId.ShouldBe("test:lock:123");
    }

    [Fact]
    public async Task InterceptAsync_WithoutResourceId_ShouldUseMethodFullName()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithLock))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        string? capturedResourceId = null;
        _lockProviderMock
            .Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(),  false, It.IsAny<CancellationToken>()))
            .Callback<string, TimeSpan, TimeSpan, TimeSpan, bool, bool, CancellationToken>((resourceId, _, _, _, _, _, _) => capturedResourceId = resourceId)
            .ReturnsAsync(true);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        capturedResourceId.ShouldNotBeNull();
        capturedResourceId.ShouldContain("TestService");
        capturedResourceId.ShouldContain("MethodWithLock");
    }

    [Fact]
    public async Task InterceptAsync_WithAutoRenew_ShouldPassAutoRenewParameter()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithAutoRenew))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        bool capturedAutoRenew = false;
        _lockProviderMock
            .Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(),  true, It.IsAny<CancellationToken>()))
            .Callback<string, TimeSpan, TimeSpan, TimeSpan, bool, bool, CancellationToken>((_, _, _, _, _, autoRenew, _) => capturedAutoRenew = autoRenew)
            .ReturnsAsync(true);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        capturedAutoRenew.ShouldBeTrue();
        _lockProviderMock.Verify(
            x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(),  true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InterceptAsync_WithoutAutoRenew_ShouldPassFalse()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithLock))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        bool capturedAutoRenew = true; // 初始化为 true，验证是否被设置为 false
        _lockProviderMock
            .Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(),  false, It.IsAny<CancellationToken>()))
            .Callback<string, TimeSpan, TimeSpan, TimeSpan, bool, bool, CancellationToken>((_, _, _, _, _, autoRenew, _) => capturedAutoRenew = autoRenew)
            .ReturnsAsync(true);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        capturedAutoRenew.ShouldBeFalse();
    }

    #region Test Service

    private class TestService
    {
        public virtual Task MethodWithoutLock()
        {
            return Task.CompletedTask;
        }

        [DistributedLock]
        public virtual Task MethodWithLock()
        {
            return Task.CompletedTask;
        }

        [DistributedLock(ResourceId = "test:lock:{id}")]
        public virtual Task MethodWithCustomResourceId(int id, string name)
        {
            return Task.CompletedTask;
        }

        [DistributedLock(AutoRenew = true)]
        public virtual Task MethodWithAutoRenew()
        {
            return Task.CompletedTask;
        }
    }

    #endregion
}

