using BeniceSoft.Abp.Auth.Core;
using BeniceSoft.Abp.Core.Users;
using BeniceSoft.Abp.Sample.Application.Contracts;
using BeniceSoft.Abp.Sample.Domain;
using BeniceSoft.Abp.Sample.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BeniceSoft.Abp.Sample.Application.Services;

/// <summary>
/// 验证「浏览器/已认证请求」下 UserPermission 未初始化警告是否复现。
/// 需 Host 开启 UseBeniceSoftUserPermission。
/// </summary>
[AllowAnonymous]
public class UserPermissionProbeAppService : SampleAppServiceBase, IUserPermissionProbeAppService
{
    private readonly ICurrentUserPermissionAccessor _permissionAccessor;
    private readonly IBeniceSoftCurrentUser _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly SampleDbContext _db;
    private readonly ILogger<UserPermissionProbeAppService> _logger;

    public UserPermissionProbeAppService(
        ICurrentUserPermissionAccessor permissionAccessor,
        IBeniceSoftCurrentUser currentUser,
        IHttpContextAccessor httpContextAccessor,
        SampleDbContext db,
        ILogger<UserPermissionProbeAppService> logger)
    {
        _permissionAccessor = permissionAccessor;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _db = db;
        _logger = logger;
    }

    public Task<UserPermissionProbeDto> SnapshotAsync()
    {
        return Task.FromResult(Capture("仅快照：对比 Accessor(AsyncLocal) 与 HttpContext.Features"));
    }

    public async Task<UserPermissionProbeRoundDto> SnapshotAroundSaveChangesAsync()
    {
        var result = new UserPermissionProbeRoundDto
        {
            BeforeSaveChanges = Capture("SaveChanges 前")
        };

        try
        {
            var tag = $"perm-probe-{DateTimeOffset.UtcNow:HHmmssfff}";
            _db.BulkDemoItems.Add(new BulkDemoItem(
                id: Guid.NewGuid(),
                code: $"P-{tag}",
                name: "user-permission-probe",
                quantity: 1,
                batchTag: tag));

            _logger.LogWarning("UserPermissionProbe: about to SaveChanges");
            result.SaveChangesAffected = await _db.SaveChangesAsync();
            _logger.LogWarning("UserPermissionProbe: SaveChanges done, affected={Affected}", result.SaveChangesAffected);

            result.AfterSaveChanges = Capture("SaveChanges 后（字段权限拦截器已读过 Accessor）");
        }
        catch (Exception ex)
        {
            result.Error = ex.ToString();
            result.AfterSaveChanges = Capture("SaveChanges 异常后");
        }

        return result;
    }

    private UserPermissionProbeDto Capture(string note)
    {
        // 故意走公开 getter，复现「未初始化」警告是否打出
        var fromAccessor = _permissionAccessor.UserPermission;
        var fromFeatures = _httpContextAccessor.HttpContext?.Features.Get<IUserPermission>();

        return new UserPermissionProbeDto
        {
            IsAuthenticated = _currentUser.IsAuthenticated,
            UserId = _currentUser.Id,
            ClientId = _currentUser.ClientId,
            AccessorIsInitialized = fromAccessor?.IsInitialized == true,
            AccessorUserId = fromAccessor?.UserId,
            FeaturesHasUserPermission = fromFeatures is not null,
            FeaturesIsInitialized = fromFeatures?.IsInitialized == true,
            FeaturesUserId = fromFeatures?.UserId,
            AccessorEqualsFeatures = ReferenceEquals(fromAccessor, fromFeatures),
            AccessorType = _permissionAccessor.GetType().AssemblyQualifiedName,
            Note = note
        };
    }
}
