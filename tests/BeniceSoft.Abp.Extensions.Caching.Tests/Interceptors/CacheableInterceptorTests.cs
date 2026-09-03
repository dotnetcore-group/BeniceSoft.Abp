using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using BeniceSoft.Abp.Extensions.Caching.Abstractions.Annotations;
using BeniceSoft.Abp.Extensions.Caching.Abstractions.Interfaces;
using BeniceSoft.Abp.Extensions.Caching.Interceptors;
using Shouldly;
using Volo.Abp.DynamicProxy;
using Xunit;

namespace BeniceSoft.Abp.Extensions.Caching.Tests.Interceptors;

/// <summary>
/// CacheableInterceptor 单元测试
/// </summary>
public class CacheableInterceptorTests
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<ICacheValueSerializer> _serializerMock;
    private readonly Mock<ILogger<CacheableInterceptor>> _loggerMock;
    private readonly CacheableInterceptor _interceptor;

    public CacheableInterceptorTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _serializerMock = new Mock<ICacheValueSerializer>();
        _loggerMock = new Mock<ILogger<CacheableInterceptor>>();
        _interceptor = new CacheableInterceptor(_loggerMock.Object, _cacheMock.Object, _serializerMock.Object);
    }

    #region 无 Cacheable 特性测试

    [Fact]
    public async Task InterceptAsync_WithoutAttribute_ShouldNotCache()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithoutCache))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert - 不应调用缓存
        _cacheMock.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        invocationMock.Verify(x => x.ProceedAsync(), Times.Once);
    }

    #endregion

    #region 有 Cacheable 特性测试

    [Fact]
    public async Task InterceptAsync_WithAttribute_CacheMiss_ShouldExecuteAndCache()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.GetProductAsync))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.Arguments).Returns(new object[] { 123 });
        invocationMock.Setup(x => x.ArgumentsDictionary).Returns(new Dictionary<string, object?> { { "id", 123 } });
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);
        invocationMock.SetupProperty(x => x.ReturnValue, Task.FromResult("Product 123"));

        // 缓存未命中
        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _serializerMock.Setup(x => x.Serialize(It.IsAny<object>())).Returns(new byte[] { 1, 2, 3 });

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        invocationMock.Verify(x => x.ProceedAsync(), Times.Once);
        _cacheMock.Verify(x => x.SetAsync(
            It.IsAny<string>(),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InterceptAsync_WithAttribute_CacheHit_ShouldReturnCachedValue()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.GetProductAsync))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.Arguments).Returns(new object[] { 123 });
        invocationMock.Setup(x => x.ArgumentsDictionary).Returns(new Dictionary<string, object?> { { "id", 123 } });
        invocationMock.SetupProperty(x => x.ReturnValue);

        var cachedBytes = new byte[] { 1, 2, 3 };
        _cacheMock.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedBytes);

        _serializerMock.Setup(x => x.Deserialize(cachedBytes, typeof(string))).Returns("Cached Product");

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert - 不应执行原方法
        invocationMock.Verify(x => x.ProceedAsync(), Times.Never);
        // 返回值应该被设置
        invocationMock.Object.ReturnValue.ShouldNotBeNull();
    }

    #endregion

    #region 同步方法测试

    [Fact]
    public async Task InterceptAsync_SyncMethod_ShouldLogWarningAndProceed()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.GetProductSync))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert - 同步方法应该直接执行
        invocationMock.Verify(x => x.ProceedAsync(), Times.Once);
        _cacheMock.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Void 方法测试

    [Fact]
    public async Task InterceptAsync_VoidMethod_ShouldNotCache()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.VoidMethodWithCache))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert - void 方法不应缓存
        invocationMock.Verify(x => x.ProceedAsync(), Times.Once);
        _cacheMock.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region 测试服务类

    private class TestService
    {
        public virtual Task MethodWithoutCache()
        {
            return Task.CompletedTask;
        }

        [Cacheable]
        public virtual Task<string> GetProductAsync(int id)
        {
            return Task.FromResult($"Product {id}");
        }

        [Cacheable]
        public virtual string GetProductSync(int id)
        {
            return $"Product {id}";
        }

        [Cacheable]
        public virtual Task VoidMethodWithCache()
        {
            return Task.CompletedTask;
        }
    }

    #endregion
}

