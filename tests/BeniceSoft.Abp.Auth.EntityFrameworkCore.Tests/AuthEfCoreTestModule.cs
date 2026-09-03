using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using BeniceSoft.Abp.Auth.Core;
using BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Entities;
using BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Mocks;
using BeniceSoft.Abp.Core.Users;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    typeof(BeniceSoftAbpAuthEntityFrameworkCoreModule)
)]
public class AuthEfCoreTestModule : AbpModule
{
    private SqliteConnection? _sqliteConnection;
    private MockCurrentUserPermissionAccessor? _mockPermissionAccessor;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 使用 SQLite 内存数据库
        _sqliteConnection = CreateDatabaseAndGetConnection();

        // 先注册模拟的权限访问器
        _mockPermissionAccessor = new MockCurrentUserPermissionAccessor();
        context.Services.AddSingleton(_mockPermissionAccessor);
        context.Services.AddSingleton<ICurrentUserPermissionAccessor>(sp =>
            sp.GetRequiredService<MockCurrentUserPermissionAccessor>());

        context.Services.AddAbpDbContext<TestDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
            // 为 TestOrder 实体指定自定义仓储
            options.AddRepository<TestOrder, TestOrderRepository>();
        });

        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(abpDbContextConfigurationContext =>
            {
                abpDbContextConfigurationContext.DbContextOptions.UseSqlite(_sqliteConnection);
            });
        });

        // 注册模拟的 BeniceSoft 当前用户
        context.Services.AddSingleton<MockBeniceSoftCurrentUser>();
        context.Services.AddSingleton<IBeniceSoftCurrentUser>(sp =>
            sp.GetRequiredService<MockBeniceSoftCurrentUser>());
    }

    private static SqliteConnection CreateDatabaseAndGetConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // 创建数据库表
        var dbContext = context.ServiceProvider.GetRequiredService<TestDbContext>();
        dbContext.GetService<IRelationalDatabaseCreator>().CreateTables();
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        _sqliteConnection?.Dispose();
    }
}
