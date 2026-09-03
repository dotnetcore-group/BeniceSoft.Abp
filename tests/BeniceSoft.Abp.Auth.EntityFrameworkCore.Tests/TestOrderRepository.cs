using BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests.Entities;
using Volo.Abp.EntityFrameworkCore;

namespace BeniceSoft.Abp.Auth.EntityFrameworkCore.Tests;

/// <summary>
/// 测试用的带行权限过滤的仓储
/// </summary>
public class TestOrderRepository : RowPermissionEfCoreRepository<TestDbContext, TestOrder, long>
{
    public TestOrderRepository(IDbContextProvider<TestDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}

