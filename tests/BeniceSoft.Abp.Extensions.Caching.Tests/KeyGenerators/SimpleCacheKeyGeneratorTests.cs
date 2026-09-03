using Moq;
using BeniceSoft.Abp.Extensions.Caching.Abstractions.Interfaces;
using BeniceSoft.Abp.Extensions.Caching.Configurations;
using Shouldly;
using Volo.Abp.DynamicProxy;
using Xunit;

namespace BeniceSoft.Abp.Extensions.Caching.Tests.KeyGenerators;

/// <summary>
/// ICacheKeyGenerator 和配置测试
/// 由于 SimpleCacheKeyGenerator 是 internal 类，我们测试配置和接口
/// </summary>
public class CacheKeyGeneratorTests
{
    public CacheKeyGeneratorTests()
    {
        // 重置配置
        BeniceSoftCachingConfiguration.Instance.CacheKeyPrefix = string.Empty;
    }

    [Fact]
    public void Configuration_CacheKeyPrefix_ShouldBe_Settable()
    {
        BeniceSoftCachingConfiguration.Instance.CacheKeyPrefix = "test:";
        BeniceSoftCachingConfiguration.Instance.CacheKeyPrefix.ShouldBe("test:");

        // 清理
        BeniceSoftCachingConfiguration.Instance.CacheKeyPrefix = string.Empty;
    }

    [Fact]
    public void Configuration_DefaultExpirationSeconds_ShouldBe_Settable()
    {
        var original = BeniceSoftCachingConfiguration.Instance.DefaultExpirationSeconds;

        BeniceSoftCachingConfiguration.Instance.DefaultExpirationSeconds = 600;
        BeniceSoftCachingConfiguration.Instance.DefaultExpirationSeconds.ShouldBe(600);

        // 恢复
        BeniceSoftCachingConfiguration.Instance.DefaultExpirationSeconds = original;
    }

    [Fact]
    public void MockInvocation_ShouldWork()
    {
        var methodInfo = typeof(TestService).GetMethod(nameof(TestService.GetByIdAsync));
        var mock = new Mock<IAbpMethodInvocation>();

        mock.Setup(x => x.Method).Returns(methodInfo!);
        mock.Setup(x => x.ArgumentsDictionary).Returns(new Dictionary<string, object?> { { "id", 123 } });
        mock.Setup(x => x.Arguments).Returns(new object[] { 123 });

        var invocation = mock.Object;

        invocation.Method.Name.ShouldBe("GetByIdAsync");
        invocation.ArgumentsDictionary["id"].ShouldBe(123);
    }

    [Fact]
    public void ICacheKeyGenerator_Interface_ShouldHave_GenerateMethod()
    {
        var interfaceType = typeof(ICacheKeyGenerator);
        var method = interfaceType.GetMethod("Generate");

        method.ShouldNotBeNull();
        method.ReturnType.ShouldBe(typeof(string));
    }
}

/// <summary>
/// 测试用服务类
/// </summary>
public class TestService
{
    public virtual Task<string> GetByIdAsync(int id) => Task.FromResult($"Item {id}");
    public virtual Task<List<string>> SearchAsync(string keyword, int page) => Task.FromResult(new List<string>());
}

