namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public enum SeparationReadStrategy
{
    Loop = 0,
    Random = 1
}

/// <summary>
/// 读取数据库连接策略
/// </summary>
public enum SeparationReadConnectionStrategy
{
    /// <summary>
    /// 每次都是最新的
    /// </summary>
    Latest = 0,

    /// <summary>
    /// 仅第一次读取,将DbContext作为缓存
    /// </summary>
    Cache = 1
}

/// <summary>
/// 读写分离默认行为
/// </summary>
public enum SeparationBehavior
{
    /// <summary>
    /// 默认不启用
    /// </summary>
    Disable = 0,

    /// <summary>
    /// 默认启用
    /// </summary>
    Enable = 1,

    /// <summary>
    /// 不在事务中启用
    /// </summary>
    OutTransaction = 2
}
