using BeniceSoft.Abp.Auth.EntityFrameworkCore;
using BeniceSoft.Abp.Sample.Domain;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace BeniceSoft.Abp.Sample.EntityFrameworkCore;

public class AmUserRepository : RowPermissionEfCoreRepository<SampleDbContext, AMUser>, IAmUserRepository
{
    public AmUserRepository(IDbContextProvider<SampleDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public async Task<List<long>> GetUserRoles(long userId)
    {
        var queryable = await GetQueryableAsync();
        
        var userinfo = await queryable.Where(x => x.Id == userId)
            .Include(x => x.Roles)
            //.IgnoreQueryFilters()
            .FirstOrDefaultAsync();
        if (userinfo == null)
        {
            return [];
        }

        return userinfo.Roles.Select(x => x.RoleId).ToList();
    }

}
