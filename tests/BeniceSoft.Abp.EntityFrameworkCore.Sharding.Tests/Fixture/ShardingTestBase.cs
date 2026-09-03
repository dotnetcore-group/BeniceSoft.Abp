using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Testing;
using Xunit;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests.Fixture;

/// <summary>所有分片测试共享同一 ABP 应用，避免重复 UseCompensate 打爆 EF ServiceProvider 缓存。</summary>
[CollectionDefinition(Name)]
public class ShardingTestCollection : ICollectionFixture<ShardingTestApplication>
{
    public const string Name = "ShardingFeatureTests";
}

public sealed class ShardingTestApplication : AbpIntegratedTest<ShardingFixtureModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    public T Resolve<T>() where T : notnull => GetRequiredService<T>();

    public IServiceScope CreateScope() => ServiceProvider.CreateScope();
}

[Collection(ShardingTestCollection.Name)]
public abstract class ShardingTestBase
{
    protected ShardingTestApplication App { get; }

    protected ShardingTestBase(ShardingTestApplication app)
    {
        App = app;
    }

    protected T GetRequiredService<T>() where T : notnull => App.Resolve<T>();

    protected static string NewBatch(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..24];
}
