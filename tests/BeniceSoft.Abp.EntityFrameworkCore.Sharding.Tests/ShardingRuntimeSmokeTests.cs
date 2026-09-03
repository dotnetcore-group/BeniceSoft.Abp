using BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests.Fixture;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests;

/// <summary>运行时上下文 / 路由注册冒烟。</summary>
public class ShardingRuntimeSmokeTests : ShardingTestBase
{
    public ShardingRuntimeSmokeTests(ShardingTestApplication app) : base(app)
    {
    }
    [Fact]
    public void RuntimeContext_Should_Expose_Routes_And_DataSources()
    {
        var runtime = GetRequiredService<IShardingRuntimeContext<ShardingFixtureDbContext>>();
        runtime.ShouldNotBeNull();

        var virtualDs = runtime.VirtualDataSource;
        virtualDs.DefaultDataSource.ShouldBe("ds0");
        var all = virtualDs.GetAllDataSource();
        all.ShouldContain("ds0");
        all.ShouldContain("ds1");

        runtime.RouteOptions.HasTableRoute(typeof(ShardLedger)).ShouldBeTrue();
        runtime.RouteOptions.HasTableRoute(typeof(ShardBucket)).ShouldBeTrue();
        runtime.RouteOptions.HasDataSourceRoute(typeof(ShardAreaOrder)).ShouldBeTrue();
    }

    [Fact]
    public void TableRoute_GetTails_Should_Include_Current_Months_And_Mod_Buckets()
    {
        var runtime = GetRequiredService<IShardingRuntimeContext>();
        var month = runtime.TableRouteManager.GetRoute(typeof(ShardLedger));
        month.GetTails().ShouldContain("202401");
        month.GetTails().ShouldContain("202402");

        var mod = runtime.TableRouteManager.GetRoute(typeof(ShardBucket));
        mod.GetTails().ShouldBe(["0", "1"], ignoreOrder: true);
    }

    [Fact]
    public void DataSourceRoute_GetAll_Should_List_Configured_Sources()
    {
        var runtime = GetRequiredService<IShardingRuntimeContext>();
        var route = runtime.DataSourceRouteManager.GetRoute(typeof(ShardAreaOrder));
        route.GetAll().ShouldBe(["ds0", "ds1"], ignoreOrder: true);
        route.GetKey("A").ShouldBe("ds0");
        route.GetKey("B").ShouldBe("ds1");
    }

    [Fact]
    public void Shell_DbContext_Should_Create_Executor()
    {
        using var scope = App.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShardingFixtureDbContext>();
        db.GetExecutor().ShouldNotBeNull();
        db.IsExecutor.ShouldBeFalse();
    }
}
