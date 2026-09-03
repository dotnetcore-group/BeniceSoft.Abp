using BeniceSoft.Abp.Auth.Repository;

namespace BeniceSoft.Abp.Sample.Domain;

public interface IAmUserRepository : IRowPermissionRepository<AMUser, long>
{
    Task<List<long>> GetUserRoles(long userId);
}
