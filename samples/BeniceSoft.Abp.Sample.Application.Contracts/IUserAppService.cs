using BeniceSoft.Core;
using System.ServiceModel;
using Volo.Abp.Application.Services;

namespace BeniceSoft.Abp.Sample.Application.Contracts;

public interface IUserAppService : IApplicationService
{
    /// <summary>
    /// 测试锁
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    Task<string> TestLockAsync(TestLockInput input);

    /// <summary>
    /// 创建用户
    /// </summary>
    /// <param name="dto"></param>
    /// <returns></returns>

    Task<CreateDto> CreateAsync(CreateDto dto);

    /// <summary>
    /// 获取用户角色ids
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    Task<List<long>> GetUserRoleIdsAsync(long id);

    Task<List<AmUserRoleDto>> GetGetUserRoleIds2Async(long id);

    ///// <summary>
    ///// 测试分页列表返回
    ///// </summary>
    //Task<PagedList<AmUserRoleDto>> GetPagedListAsync(int pageIndex, int pageSize);
}
