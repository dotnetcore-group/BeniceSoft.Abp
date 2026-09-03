using Moq;
using BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;
using Shouldly;
using Xunit;

namespace BeniceSoft.Abp.Extensions.DistributedLock.Tests;

/// <summary>
/// 自动续期（Watchdog）功能测试
/// </summary>
public class AutoRenewTests
{
    #region DistributedLockAttribute AutoRenew Tests

    [Fact]
    public void DistributedLockAttribute_AutoRenew_DefaultValue_ShouldBeFalse()
    {
        // Arrange & Act
        var attribute = new DistributedLockAttribute();

        // Assert
        attribute.AutoRenew.ShouldBeFalse();
    }

    [Fact]
    public void DistributedLockAttribute_AutoRenew_CanBeSetToTrue()
    {
        // Arrange & Act
        var attribute = new DistributedLockAttribute { AutoRenew = true };

        // Assert
        attribute.AutoRenew.ShouldBeTrue();
    }

    [Fact]
    public void DistributedLockAttribute_WithAutoRenew_AllPropertiesWork()
    {
        // Arrange & Act
        var attribute = new DistributedLockAttribute
        {
            ResourceId = "long-task:{id}",
            ExpiresMilliseconds = 30000,
            WaitMilliseconds = 10000,
            IntervalMilliseconds = 500,
            AutoRenew = true
        };

        // Assert
        attribute.ResourceId.ShouldBe("long-task:{id}");
        attribute.ExpiresMilliseconds.ShouldBe(30000);
        attribute.WaitMilliseconds.ShouldBe(10000);
        attribute.IntervalMilliseconds.ShouldBe(500);
        attribute.AutoRenew.ShouldBeTrue();
    }

    #endregion

    #region IDistributedLockProvider AutoRenew Overloads Tests

    [Fact]
    public void IDistributedLockProvider_ShouldHaveAutoRenewOverloads()
    {
        // Arrange
        var type = typeof(IDistributedLockProvider);
        var methods = type.GetMethods();

        // Assert - AcquireAsync 应该有带 autoRenew 参数的重载
        var acquireAsyncMethods = methods.Where(m => m.Name == "AcquireAsync").ToList();
        acquireAsyncMethods.Count.ShouldBeGreaterThanOrEqualTo(2);

        // 验证有 7 参数版本（包含 autoRenew）
        acquireAsyncMethods.ShouldContain(m => m.GetParameters().Length == 7);

        // Assert - TryAcquireAsync 应该有带 autoRenew 参数的重载
        var tryAcquireAsyncMethods = methods.Where(m => m.Name == "TryAcquireAsync").ToList();
        tryAcquireAsyncMethods.Count.ShouldBeGreaterThanOrEqualTo(2);

        // 验证有 5 参数版本（包含 autoRenew）
        tryAcquireAsyncMethods.ShouldContain(m => m.GetParameters().Length == 5);
    }

    [Fact]
    public void IDistributedLockProvider_ShouldHaveRenewLockAsyncMethod()
    {
        // Arrange
        var type = typeof(IDistributedLockProvider);

        // Assert
        var renewMethod = type.GetMethod("RenewLockAsync");
        renewMethod.ShouldNotBeNull();

        var parameters = renewMethod.GetParameters();
        parameters.Length.ShouldBe(2);
        parameters[0].Name.ShouldBe("resourceId");
        parameters[0].ParameterType.ShouldBe(typeof(string));
        parameters[1].Name.ShouldBe("extends");
        parameters[1].ParameterType.ShouldBe(typeof(TimeSpan));
    }

    #endregion

    #region Mock Provider AutoRenew Tests

    [Fact]
    public async Task MockProvider_AcquireAsync_WithAutoRenew_ShouldWork()
    {
        // Arrange
        var mockProvider = new Mock<IDistributedLockProvider>();
        mockProvider.Setup(x => x.AcquireAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await mockProvider.Object.AcquireAsync(
            "test:resource",
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(100),
            autoRenew: true);

        // Assert
        result.ShouldBeTrue();
        mockProvider.Verify(x => x.AcquireAsync(
            "test:resource",
            TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(100),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MockProvider_TryAcquireAsync_WithAutoRenew_ShouldWork()
    {
        // Arrange
        var mockProvider = new Mock<IDistributedLockProvider>();
        mockProvider.Setup(x => x.TryAcquireAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await mockProvider.Object.TryAcquireAsync(
            "test:resource",
            TimeSpan.FromMinutes(1),
            autoRenew: true);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task MockProvider_RenewLockAsync_ShouldWork()
    {
        // Arrange
        var mockProvider = new Mock<IDistributedLockProvider>();
        mockProvider.Setup(x => x.RenewLockAsync("test:resource", TimeSpan.FromMinutes(1)))
            .ReturnsAsync(true);

        // Act
        var result = await mockProvider.Object.RenewLockAsync("test:resource", TimeSpan.FromMinutes(1));

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task MockProvider_RenewLockAsync_NonExistentLock_ShouldReturnFalse()
    {
        // Arrange
        var mockProvider = new Mock<IDistributedLockProvider>();
        mockProvider.Setup(x => x.RenewLockAsync("non-existent", It.IsAny<TimeSpan>()))
            .ReturnsAsync(false);

        // Act
        var result = await mockProvider.Object.RenewLockAsync("non-existent", TimeSpan.FromMinutes(1));

        // Assert
        result.ShouldBeFalse();
    }

    #endregion
}

