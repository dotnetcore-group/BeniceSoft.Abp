using System.Data;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using BeniceSoft.Abp.Auth.Repository;
using BeniceSoft.Abp.EntityFrameworkCore;
using BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;
using BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;
using BeniceSoft.Abp.OperationLogging.Abstractions;
using BeniceSoft.Abp.Sample.Application.Contracts;
using BeniceSoft.Abp.Sample.Domain;
using BeniceSoft.Core;
using BeniceSoft.Http.FluentClient;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;

namespace BeniceSoft.Abp.Sample.Application.Services;

/// <summary>
/// 示例
/// </summary>
//[Authorize]
public class SampleAppService : SampleAppServiceBase, IUserAppService
{
    private readonly IAmUserRepository _amUserRepository;
    //private readonly IRowPermissionRepository<AMUser> _rowPermissionRepository;
    private readonly IRowPermissionRepository<AMUser, long> _auth2Repository;
    private readonly IRepository<AMUser, long> _amUser3Repository;

    private readonly IHttpClientFactory _httpClientFactory;

    private readonly IDistributedLockProvider _distributedLockProvider;

    public SampleAppService(
        IAmUserRepository amUserRepository,
        //IRowPermissionRepository<AMUser> rowPermissionRepository
        IRowPermissionRepository<AMUser, long> auth2Repository,
        IRepository<AMUser, long> repository,
        IHttpClientFactory httpClientFactory,
        IDistributedLockProvider distributedLockProvider)
    {

        _amUserRepository = amUserRepository;
        //_rowPermissionRepository = rowPermissionRepository;
        _auth2Repository = auth2Repository;
        _amUser3Repository = repository;
        _httpClientFactory = httpClientFactory;
        _distributedLockProvider = distributedLockProvider;
    }

    [OperationLog(OperationType = "Create", BizModule = "Sample")]
    public virtual async Task<CreateDto> CreateAsync(CreateDto dto)
    {
        await _amUserRepository.InsertAsync(new AMUser("test", "测试用户"));
        return dto;
    }

    /// <summary>
    /// 操作日志示例 - 使用 OperationLogContext 动态设置业务信息
    /// <para>
    /// 方法最后一个参数为 OperationLogContext 类型时，拦截器会自动注入实例，
    /// 方法体内可以设置 BizId、BizCode、Remark、ExtraData 等动态数据，
    /// 这些数据会覆盖 Attribute 上的静态配置。
    /// </para>
    /// </summary>
    [OperationLog(OperationType = "Update", BizModule = "Sample")]
    public virtual async Task<string> UpdateWithContextAsync(long id, string name, OperationLogContext? context = null)
    {
        // 模拟业务操作
        var user = await _amUserRepository.GetAsync(id);

        // 通过 context 动态设置操作日志的业务信息
        if (context != null)
        {
            context.BizId = id.ToString();
            context.BizCode = user.UserName;
            context.Remark = $"更新用户名为: {name}";
            context.ExtraData["oldName"] = user.UserName;
            context.ExtraData["newName"] = name;
        }

        return "OK";
    }

    /// <summary>
    /// 测试锁
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [AllowAnonymous]
    [DistributedLock(ResourceId = "sample:lock:{input.Id}", ExpiresMilliseconds = 5000, AutoRenew = true)]
    public virtual async Task<string> TestLockAsync(TestLockInput input)
    {
        // 测试自动续期功能：
        // 锁过期时间5秒，方法执行8秒，开启自动续期
        // 在6秒时另一个请求应该仍然获取不到锁
        await Task.Delay(8000);
        return "OK";
    }

    /// <summary>
    /// 测试自动续期（默认 TTL 60s，续期周期 TTL/2）
    /// </summary>
    [AllowAnonymous]
    [DistributedLock(ResourceId = "sample:lock-renew:{input.Id}", AutoRenew = true)]
    public virtual async Task<string> TestLockAutoRenewAsync(TestLockInput input)
    {
        var delay = input.DelayMilliseconds > 0 ? input.DelayMilliseconds : 35000;
        await Task.Delay(delay);
        return "OK";
    }

    /// <summary>
    /// 手动使用分布式锁
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [AllowAnonymous]
    public virtual async Task<string> TestLockManualAsync(TestLockInput input)
    {
        var resourceId = $"sample:manual-lock:{input.Id}";

        var acquired = await _distributedLockProvider.AcquireAsync(
            resourceId,
            TimeSpan.FromMilliseconds(5000),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(25),
            autoRenew: true);

        if (!acquired)
        {
            return "资源已被占用，请稍候再试";
        }

        try
        {
            // 手动调用示例：模拟业务执行
            await Task.Delay(8000);
            return "OK";
        }
        finally
        {
            await _distributedLockProvider.ReleaseLockAsync(resourceId);
        }
    }

    /// <summary>
    /// 获取用户角色ids
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<List<long>> GetUserRoleIdsAsync(long id)
    {
        return await _amUserRepository.GetUserRoles(id);
    }


    public async Task<List<AmUserRoleDto>> GetGetUserRoleIds2Async(long id)
    {
        //var queryable = await _rowPermissionRepository.GetQueryableAsync();
        //var userinfo = await QueryableWrapperFactory.CreateWrapper(queryable).AsNoTracking()
        //    .WhereIf(true, s => s.Id == id)
        //    .Include(x => x.Roles)
        //    .FirstOrDefaultAsync();
        var queryable = await _auth2Repository.GetQueryableAsync();
        var userinfo = await QueryableWrapperFactory.CreateWrapper(queryable)
            .AsNoTracking()
            .WhereIf(true, s => s.Id == id)
            .Include(x => x.Roles)
            .FirstOrDefaultAsync();

        if (userinfo == null)
        {
            return [];
        }

        return userinfo.Roles.Adapt<List<AmUserRoleDto>>();
    }

    #region 限流测试接口

    /// <summary>
    /// 测试限流 - 每分钟最多3次（全局）
    /// </summary>
    [AllowAnonymous]
    [RateLimit(LimitBy = RateLimitBy.Global, PermitLimit = 3, WindowSeconds = 60, Message = "全局限流：请求太频繁，请稍后再试")]
    public virtual Task<string> TestRateLimitGlobalAsync()
    {
        return Task.FromResult($"OK - 全局限流测试 - {DateTime.Now:HH:mm:ss}");
    }

    /// <summary>
    /// 测试限流 - 每分钟最多5次（按IP）
    /// </summary>
    [AllowAnonymous]
    [RateLimit(LimitBy = RateLimitBy.Ip, PermitLimit = 5, WindowSeconds = 60, Message = "IP限流：您的请求太频繁")]
    public virtual Task<string> TestRateLimitByIpAsync()
    {
        return Task.FromResult($"OK - IP限流测试 - {DateTime.Now:HH:mm:ss}");
    }

    /// <summary>
    /// 测试限流 - 每分钟最多3次（按用户）
    /// </summary>
    [RateLimit(LimitBy = RateLimitBy.UserId, PermitLimit = 3, WindowSeconds = 60, Message = "用户限流：您的操作太频繁")]
    public virtual Task<string> TestRateLimitByUserAsync()
    {
        return Task.FromResult($"OK - 用户限流测试 - {DateTime.Now:HH:mm:ss}");
    }

    /// <summary>
    /// 测试限流 - 超限但不抛异常
    /// </summary>
    [AllowAnonymous]
    [RateLimit(PermitLimit = 2, WindowSeconds = 60, ThrowOnExceeded = false)]
    public virtual Task<string> TestRateLimitNoThrowAsync()
    {
        return Task.FromResult($"OK - 不抛异常测试 - {DateTime.Now:HH:mm:ss}");
    }

    #endregion

    /// <summary>
    /// 测试分页列表返回
    /// </summary>
    [AllowAnonymous]
    public Task<PagedList<AmUserRoleDto>> GetPagedListAsync(int pageIndex, int pageSize)
    {
        var tenantId = CurrentTenant.Id;
        var eneantId2 = CurrentUser.TenantId;

        // 模拟数据
        var items = new List<AmUserRoleDto>
        {
            new() { Id = 1, Name = "管理员", IsEnabled = true },
            new() { Id = 2, Name = "用户", IsEnabled = true },
            new() { Id = 3, Name = "访客", IsEnabled = false }
        };

        // 模拟分页
        var pagedItems = items.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
        var totalCount = items.Count;

        return Task.FromResult(new PagedList<AmUserRoleDto>(totalCount, pagedItems));
    }

    [AllowAnonymous]
    public async Task<string> TestEncryptAsync()
    {
        var str = JsonUtils.Serialize(new
        {
            wydSku = "SZN260300451"
        });
        var aesKey = "x/QkOLAsrVfuVCYZ8SX/Wg==";

        string data = string.Empty;
        if (!string.IsNullOrEmpty(str) && !string.IsNullOrEmpty(aesKey))
        {
            byte[] toEncryptArray = Encoding.UTF8.GetBytes(str);
            using (Aes aes = Aes.Create())
            {
                byte[] keyArray = Convert.FromBase64String(aesKey);

                aes.Key = keyArray;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.PKCS7;
                aes.IV = new byte[16]; // ECB 模式 IV 无效，但必须赋值，避免报错

                var cryptoTransform = aes.CreateEncryptor();
                var resultArray = cryptoTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
                data = Convert.ToBase64String(resultArray, 0, resultArray.Length);

            }
        }

        var request = new
        {
            uniqueNo = Guid.NewGuid(),
            body = data,
            appid = "API_Test",
            timezoneOffset = 8,
            source = "TEST"
        };

        var url = "https://oms-openapi-test.wydgroup.com/oms/openapi/goods/v1/goods/query";
        var httpClient = _httpClientFactory.CreateClient("FluentClient");
        using var client = new FluentClient(httpClient, url, manageBaseClient: false);
        var result = await client.Post(request).AsString();

        return result;
    }

    [AllowAnonymous]
    public async Task<string> TestDataTableConvertToListAsync()
    {
        await Task.Delay(1);

        var dt = new DataTable("Users");
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("UserName", typeof(string));
        dt.Columns.Add("Nickname", typeof(string));
        dt.Columns.Add("BirthDate", typeof(DateTime));
        dt.Columns.Add("IsActive", typeof(bool));
        dt.Columns.Add("Score", typeof(double));
        dt.Columns.Add("Email", typeof(string));
        dt.Columns.Add("Age", typeof(int));

        // 添加数据
        for (int i = 0; i < 1000000; i++)
        {
            //dt.Rows.Add(1, "张三", new DateTime(1990, 5, 20), true, 95.5);
            dt.Rows.Add(1, "张三", "傻子", new DateTime(1990, 5, 20), true, 95.5);
        }

        dt.Rows.Add(1, "王把稿子", "傻子", new DateTime(1990, 5, 20), true, 95.5, "", 165);

        var watch = new Stopwatch();
        watch.Start();

        var list = ArrayUtils.ToListReflector<AmUserInfoDto>(dt);

        watch.Stop();

        var str = $"ToListReflector 耗时 {watch.Elapsed.TotalMilliseconds} 毫秒   ";

        watch.Restart();
        dt.ToList<AmUserInfoDto>();
        watch.Stop();
        var sre1 = $"ToList 耗时：{watch.Elapsed.TotalMilliseconds} 毫秒    ";

        return str + sre1;
    }
}
