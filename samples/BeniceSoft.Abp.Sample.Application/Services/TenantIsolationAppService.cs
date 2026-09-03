using BeniceSoft.Abp.Sample.Application.Contracts;
using BeniceSoft.Abp.Sample.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.Sample.Application.Services;

/// <summary>
/// 逻辑多租户探测（ICurrentTenant，来自认证 claim）。
/// Sample 的 <see cref="SampleDbContext"/> 已启用分片：物理库由 VirtualDataSource 决定，不走 ABP 租户换库。
/// </summary>
public class TenantIsolationAppService : SampleAppServiceBase, ITenantIsolationAppService
{
    private readonly IDbContextProvider<SampleDbContext> _dbContextProvider;
    private readonly ICurrentTenant _currentTenant;

    public TenantIsolationAppService(
        IDbContextProvider<SampleDbContext> dbContextProvider,
        ICurrentTenant currentTenant)
    {
        _dbContextProvider = dbContextProvider;
        _currentTenant = currentTenant;
    }

    [UnitOfWork]
    public virtual async Task<TenantDbInfoDto> GetCurrentDatabaseAsync()
    {
        var db = await _dbContextProvider.GetDbContextAsync();
        await db.Database.OpenConnectionAsync();
        try
        {
            return new TenantDbInfoDto
            {
                TenantId = _currentTenant.Id,
                TenantName = _currentTenant.Name,
                IsHost = !_currentTenant.Id.HasValue,
                DatabaseName = db.Database.GetDbConnection().Database,
                Note = "Sharding DbContext uses VirtualDataSource default (Host ConnectionStrings:Default); not ABP tenant connection strings."
            };
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    [UnitOfWork]
    public virtual async Task<TenantDbInfoDto> WriteProbeAsync(string? code = null)
    {
        // 不再演示「换租户库写入」：分片壳连接固定。仅返回当前逻辑租户 + 实际物理库名。
        _ = code;
        return await GetCurrentDatabaseAsync();
    }
}
