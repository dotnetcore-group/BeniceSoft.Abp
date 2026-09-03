using BeniceSoft.Abp.Ddd.Domain.Entity;

namespace BeniceSoft.Abp.Sample.Domain;

public class AMUser : BeniceSoftFullAuditedAggregateRoot<long>
{
    public string UserName { get; private set; }

    /// <summary>
    /// 昵称
    /// </summary>
    public string Nickname { get; private set; }

    /// <summary>
    /// 用户所属角色
    /// </summary>
    public IReadOnlyCollection<AmUserRole> Roles => _roles;
    private readonly List<AmUserRole> _roles;

    private AMUser()
    {
        UserName = string.Empty;
        Nickname = string.Empty;
        _roles = [];
    }

    public AMUser(string userName, string nickname) : this()
    {
        UserName = userName;
        Nickname = nickname;
    }

    public void AddRole(long roleId)
    {
        if (_roles.Any(x => x.RoleId == roleId))
        {
            return;
        }

        _roles.Add(new(Id, roleId));
    }

    public void RemoveRole(long roleId)
    {
        _roles.RemoveAll(x => x.RoleId == roleId);
    }
}
