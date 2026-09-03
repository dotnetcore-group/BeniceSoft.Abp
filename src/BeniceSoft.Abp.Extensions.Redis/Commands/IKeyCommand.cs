namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// Redis Key 命令接口
/// </summary>
public interface IKeyCommand
{
    /// <summary>
    /// 删除给定的一个或多个 key
    /// </summary>
    long Del(params string[] keys);

    /// <summary>
    /// 删除给定的一个或多个 key
    /// </summary>
    Task<long> DelAsync(params string[] keys);

    /// <summary>
    /// 检查给定 key 是否存在
    /// </summary>
    bool Exists(string key);

    /// <summary>
    /// 检查给定 key 是否存在
    /// </summary>
    Task<bool> ExistsAsync(string key);

    /// <summary>
    /// 为给定 key 设置生存时间（秒）
    /// </summary>
    bool Expire(string key, double sec);

    /// <summary>
    /// 为给定 key 设置生存时间（秒）
    /// </summary>
    Task<bool> ExpireAsync(string key, double sec);

    /// <summary>
    /// 移除给定 key 的生存时间，将这个 key 从『易失的』转换成『持久的』
    /// </summary>
    bool Persist(string key);

    /// <summary>
    /// 移除给定 key 的生存时间
    /// </summary>
    Task<bool> PersistAsync(string key);

    /// <summary>
    /// 返回 key 的剩余生存时间（秒）
    /// </summary>
    double TTL(string key);

    /// <summary>
    /// 返回 key 的剩余生存时间（秒）
    /// </summary>
    Task<double> TTLAsync(string key);

    /// <summary>
    /// 返回 key 所储存的值的类型
    /// </summary>
    string Type(string key);

    /// <summary>
    /// 返回 key 所储存的值的类型
    /// </summary>
    Task<string> TypeAsync(string key);
}

