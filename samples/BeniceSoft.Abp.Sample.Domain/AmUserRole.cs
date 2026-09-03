using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities;

namespace BeniceSoft.Abp.Sample.Domain;

public class AmUserRole : Entity
{
    public long UserId { get; private set; }

    public long RoleId { get; private set; }

    private AmUserRole()
    {
    }

    public AmUserRole(long userId, long roleId)
    {
        UserId = userId;
        RoleId = roleId;
    }

    public override object[] GetKeys()
    {
        return [UserId, RoleId];
    }
}
