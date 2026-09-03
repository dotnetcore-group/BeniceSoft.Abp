namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// Redis String 命令接口
/// </summary>
public interface IStringCommand
{
    /// <summary>
    /// 获取指定 key 的值
    /// </summary>
    T? Get<T>(string key);

    /// <summary>
    /// 获取指定 key 的值
    /// </summary>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// 设置指定 key 的值
    /// </summary>
    bool Set<T>(string key, T value, TimeSpan? ttl = null);

    /// <summary>
    /// 设置指定 key 的值
    /// </summary>
    Task<bool> SetAsync<T>(string key, T value, TimeSpan? ttl = null);

    /// <summary>
    /// 只有在 key 不存在时设置 key 的值
    /// </summary>
    bool SetNx<T>(string key, T value, TimeSpan? ttl = null);

    /// <summary>
    /// 只有在 key 不存在时设置 key 的值
    /// </summary>
    Task<bool> SetNxAsync<T>(string key, T value, TimeSpan? ttl = null);

    /// <summary>
    /// 只有在 key 存在时设置 key 的值
    /// </summary>
    bool SetEx<T>(string key, T value, TimeSpan? ttl = null);

    /// <summary>
    /// 只有在 key 存在时设置 key 的值
    /// </summary>
    Task<bool> SetExAsync<T>(string key, T value, TimeSpan? ttl = null);

    /// <summary>
    /// 将 key 中储存的数字值增一
    /// </summary>
    long Incr(string key);

    /// <summary>
    /// 将 key 中储存的数字值增一
    /// </summary>
    Task<long> IncrAsync(string key);

    /// <summary>
    /// 将 key 所储存的值加上给定的增量值
    /// </summary>
    long IncrBy(string key, long count);

    /// <summary>
    /// 将 key 所储存的值加上给定的增量值
    /// </summary>
    Task<long> IncrByAsync(string key, long count);

    /// <summary>
    /// 将 key 中储存的数字值减一
    /// </summary>
    long Decr(string key);

    /// <summary>
    /// 将 key 中储存的数字值减一
    /// </summary>
    Task<long> DecrAsync(string key);

    /// <summary>
    /// key 所储存的值减去给定的减量值
    /// </summary>
    long DecrBy(string key, long count);

    /// <summary>
    /// key 所储存的值减去给定的减量值
    /// </summary>
    Task<long> DecrByAsync(string key, long count);
}

