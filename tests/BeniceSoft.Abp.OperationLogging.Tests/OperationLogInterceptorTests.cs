using BeniceSoft.Abp.Core.Users;
using BeniceSoft.Abp.OperationLogging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Volo.Abp.DynamicProxy;
using Xunit;

namespace BeniceSoft.Abp.OperationLogging.Tests;

/// <summary>
/// OperationLogInterceptor 单元测试
/// </summary>
public class OperationLogInterceptorTests
{
    private readonly Mock<IOperationLogEventDispatcher> _dispatcherMock;
    private readonly Mock<IBeniceSoftCurrentUser> _currentUserMock;
    private readonly BeniceSoftOperationLogOptions _options;
    private readonly OperationLogInterceptor _interceptor;

    public OperationLogInterceptorTests()
    {
        _dispatcherMock = new Mock<IOperationLogEventDispatcher>();
        _currentUserMock = new Mock<IBeniceSoftCurrentUser>();
        _options = new BeniceSoftOperationLogOptions { ServiceName = "TestService" };

        _currentUserMock.Setup(x => x.Id).Returns(1L);
        _currentUserMock.Setup(x => x.Name).Returns("TestUser");

        _interceptor = new OperationLogInterceptor(
            _dispatcherMock.Object,
            Options.Create(_options),
            _currentUserMock.Object,
            new Mock<ILogger<OperationLogInterceptor>>().Object);
    }

    #region 基本测试

    [Fact]
    public async Task InterceptAsync_WithoutAttribute_ShouldProceedWithoutDispatch()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.MethodWithoutLog))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        _dispatcherMock.Verify(x => x.DispatchAsync(It.IsAny<OperationLogInfo>()), Times.Never);
        invocationMock.Verify(x => x.ProceedAsync(), Times.Once);
    }

    [Fact]
    public async Task InterceptAsync_WithAttribute_ShouldDispatchLog()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.CreateOrder))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);
        invocationMock.Setup(x => x.Arguments).Returns(Array.Empty<object>());

        OperationLogInfo? capturedLog = null;
        _dispatcherMock
            .Setup(x => x.DispatchAsync(It.IsAny<OperationLogInfo>()))
            .Callback<OperationLogInfo>(log => capturedLog = log)
            .Returns(Task.CompletedTask);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        capturedLog.ShouldNotBeNull();
        capturedLog.ServiceName.ShouldBe("TestService");
        capturedLog.OperationType.ShouldBe("Create");
        capturedLog.BizModule.ShouldBe("Order");
        capturedLog.OperatorId.ShouldBe(1L);
        capturedLog.OperatorName.ShouldBe("TestUser");
        capturedLog.OperationTime.Offset.ShouldBe(TimeSpan.Zero); // 应为 UTC
    }

    #endregion

    #region OperationLogContext 测试

    [Fact]
    public async Task InterceptAsync_WithContext_ShouldMergeContextData()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.UpdateOrderWithContext))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);

        // 模拟参数：方法最后一个参数是 OperationLogContext
        var arguments = new object?[] { "order-123", null };
        invocationMock.Setup(x => x.Arguments).Returns(arguments!);
        invocationMock.Setup(x => x.ProceedAsync()).Callback(() =>
        {
            // 模拟方法体内设置 context
            var ctx = (OperationLogContext)arguments[1]!;
            ctx.BizId = "BIZ-001";
            ctx.BizCode = "ORD-2026";
            ctx.Remark = "更新了订单状态";
            ctx.ExtraData["status"] = "Completed";
        }).Returns(Task.CompletedTask);

        OperationLogInfo? capturedLog = null;
        _dispatcherMock
            .Setup(x => x.DispatchAsync(It.IsAny<OperationLogInfo>()))
            .Callback<OperationLogInfo>(log => capturedLog = log)
            .Returns(Task.CompletedTask);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert
        capturedLog.ShouldNotBeNull();
        capturedLog.BizId.ShouldBe("BIZ-001");
        capturedLog.BizCode.ShouldBe("ORD-2026");
        capturedLog.Remark.ShouldBe("更新了订单状态");
        capturedLog.ExtraData.ShouldNotBeNull();
        capturedLog.ExtraData!["status"].ShouldBe("Completed");
    }

    [Fact]
    public async Task InterceptAsync_ContextBizIdOverridesAttribute()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.DeleteOrderWithContext))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);

        var arguments = new object?[] { null };
        invocationMock.Setup(x => x.Arguments).Returns(arguments!);
        invocationMock.Setup(x => x.ProceedAsync()).Callback(() =>
        {
            var ctx = (OperationLogContext)arguments[0]!;
            ctx.BizId = "dynamic-biz-id";
        }).Returns(Task.CompletedTask);

        OperationLogInfo? capturedLog = null;
        _dispatcherMock
            .Setup(x => x.DispatchAsync(It.IsAny<OperationLogInfo>()))
            .Callback<OperationLogInfo>(log => capturedLog = log)
            .Returns(Task.CompletedTask);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert - context.BizId 优先于 attribute.BizId
        capturedLog.ShouldNotBeNull();
        capturedLog.BizId.ShouldBe("dynamic-biz-id");
    }

    #endregion

    #region 边界测试 - 方法异常

    [Fact]
    public async Task InterceptAsync_WhenMethodThrows_ShouldStillDispatchLogAndRethrow()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.CreateOrder))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.Arguments).Returns(Array.Empty<object>());
        invocationMock.Setup(x => x.ProceedAsync()).ThrowsAsync(new InvalidOperationException("业务异常"));

        _dispatcherMock
            .Setup(x => x.DispatchAsync(It.IsAny<OperationLogInfo>()))
            .Returns(Task.CompletedTask);

        // Act & Assert - 业务异常应该继续冒泡
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await _interceptor.InterceptAsync(invocationMock.Object));

        // 即使方法抛异常，日志也应该被分发
        _dispatcherMock.Verify(x => x.DispatchAsync(It.IsAny<OperationLogInfo>()), Times.Once);
    }

    #endregion

    #region 边界测试 - 用户未登录

    [Fact]
    public async Task InterceptAsync_WhenUserNotAuthenticated_ShouldDispatchWithNullOperator()
    {
        // Arrange - 用户未登录
        var currentUserMock = new Mock<IBeniceSoftCurrentUser>();
        currentUserMock.Setup(x => x.Id).Returns((long?)null);
        currentUserMock.Setup(x => x.Name).Returns((string?)null ?? "");

        var interceptor = new OperationLogInterceptor(
            _dispatcherMock.Object,
            Options.Create(_options),
            currentUserMock.Object,
            new Mock<ILogger<OperationLogInterceptor>>().Object);

        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.CreateOrder))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);
        invocationMock.Setup(x => x.Arguments).Returns(Array.Empty<object>());

        OperationLogInfo? capturedLog = null;
        _dispatcherMock
            .Setup(x => x.DispatchAsync(It.IsAny<OperationLogInfo>()))
            .Callback<OperationLogInfo>(log => capturedLog = log)
            .Returns(Task.CompletedTask);

        // Act
        await interceptor.InterceptAsync(invocationMock.Object);

        // Assert - 日志应该正常分发，操作人信息为空
        capturedLog.ShouldNotBeNull();
        capturedLog.OperatorId.ShouldBeNull();
        capturedLog.OperatorName.ShouldBe(string.Empty);
    }

    #endregion

    #region 边界测试 - Context 未设置任何值

    [Fact]
    public async Task InterceptAsync_WhenContextNotModified_ShouldFallbackToAttributeValues()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.DeleteOrderWithContext))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);

        var arguments = new object?[] { null };
        invocationMock.Setup(x => x.Arguments).Returns(arguments!);
        // 方法体内不对 context 做任何修改
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        OperationLogInfo? capturedLog = null;
        _dispatcherMock
            .Setup(x => x.DispatchAsync(It.IsAny<OperationLogInfo>()))
            .Callback<OperationLogInfo>(log => capturedLog = log)
            .Returns(Task.CompletedTask);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert - BizId 应回退到 attribute 上的静态值
        capturedLog.ShouldNotBeNull();
        capturedLog.BizId.ShouldBe("static-biz-id");
        capturedLog.BizCode.ShouldBe(string.Empty);
        capturedLog.Remark.ShouldBe(string.Empty);
    }

    #endregion

    #region 边界测试 - Dispatcher 抛异常

    [Fact]
    public async Task InterceptAsync_WhenDispatcherThrows_ShouldNotAffectBusiness()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.CreateOrder))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);
        invocationMock.Setup(x => x.Arguments).Returns(Array.Empty<object>());

        _dispatcherMock
            .Setup(x => x.DispatchAsync(It.IsAny<OperationLogInfo>()))
            .ThrowsAsync(new Exception("Redis 连接失败"));

        // Act - Dispatcher 异常不应影响业务，不应抛出
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert - 业务方法正常执行了
        invocationMock.Verify(x => x.ProceedAsync(), Times.Once);
        // Dispatcher 也被调用了（只是内部吞掉了异常）
        _dispatcherMock.Verify(x => x.DispatchAsync(It.IsAny<OperationLogInfo>()), Times.Once);
    }

    #endregion

    #region 边界测试 - 最后参数不是 OperationLogContext

    [Fact]
    public async Task InterceptAsync_WhenLastParamNotContext_ShouldNotInjectContext()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.UpdateOrderNoContext))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.Arguments).Returns(new object[] { "order-123", "new-name" });
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        OperationLogInfo? capturedLog = null;
        _dispatcherMock
            .Setup(x => x.DispatchAsync(It.IsAny<OperationLogInfo>()))
            .Callback<OperationLogInfo>(log => capturedLog = log)
            .Returns(Task.CompletedTask);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert - 应正常分发日志，BizId/BizCode/Remark 均为空（无 context 赋值）
        capturedLog.ShouldNotBeNull();
        capturedLog.BizId.ShouldBe(string.Empty);
        capturedLog.BizCode.ShouldBe(string.Empty);
        capturedLog.Remark.ShouldBe(string.Empty);
        capturedLog.OperationType.ShouldBe("Update");
    }

    #endregion

    #region 边界测试 - 无参数方法

    [Fact]
    public async Task InterceptAsync_NoParameters_ShouldDispatchWithoutContext()
    {
        // Arrange
        var invocationMock = new Mock<IAbpMethodInvocation>();
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.CreateOrder))!;
        invocationMock.Setup(x => x.Method).Returns(methodInfo);
        invocationMock.Setup(x => x.Arguments).Returns(Array.Empty<object>());
        invocationMock.Setup(x => x.ProceedAsync()).Returns(Task.CompletedTask);

        OperationLogInfo? capturedLog = null;
        _dispatcherMock
            .Setup(x => x.DispatchAsync(It.IsAny<OperationLogInfo>()))
            .Callback<OperationLogInfo>(log => capturedLog = log)
            .Returns(Task.CompletedTask);

        // Act
        await _interceptor.InterceptAsync(invocationMock.Object);

        // Assert - context 未注入，BizId 应使用 attribute 默认值（空字符串）
        capturedLog.ShouldNotBeNull();
        capturedLog.BizId.ShouldBe(string.Empty);
        capturedLog.ExtraData.ShouldNotBeNull();
        capturedLog.ExtraData.ShouldBeEmpty();
    }

    #endregion

    #region 测试服务类

    private class TestService
    {
        public virtual Task MethodWithoutLog() => Task.CompletedTask;

        [OperationLog(OperationType = "Create", BizModule = "Order")]
        public virtual Task CreateOrder() => Task.CompletedTask;

        [OperationLog(OperationType = "Update", BizModule = "Order")]
        public virtual Task UpdateOrderWithContext(string orderId, OperationLogContext? context = null) => Task.CompletedTask;

        [OperationLog(OperationType = "Update", BizModule = "Order")]
        public virtual Task UpdateOrderNoContext(string orderId, string name) => Task.CompletedTask;

        [OperationLog(OperationType = "Delete", BizModule = "Order", BizId = "static-biz-id")]
        public virtual Task DeleteOrderWithContext(OperationLogContext? context = null) => Task.CompletedTask;
    }

    #endregion
}

