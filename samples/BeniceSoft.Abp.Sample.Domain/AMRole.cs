using BeniceSoft.Abp.Ddd.Domain.Entity;

namespace BeniceSoft.Abp.Sample.Domain;

public class AMRole : BeniceSoftFullAuditedAggregateRoot<long>
{
    public string Name { get; private set; }

    public string NormalizedName { get; private set; }

    /// <summary>
    /// 启用/禁用
    /// </summary>
    public bool IsEnabled { get; private set; }

    private AMRole()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
        IsEnabled = false;
    }

    public AMRole(string name, string normalizedName) : this()
    {
        Name = name;
        NormalizedName = normalizedName;
    }
}
