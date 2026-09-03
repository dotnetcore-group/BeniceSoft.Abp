namespace BeniceSoft.Abp.Ddd.Domain.Entity;

/// <summary>
/// 拥有者接口
/// </summary>
public interface IHaveOwnerId
{
    /// <summary>
    /// 拥有者Id
    /// </summary>
    long OwnerId { get; }
}