namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// Redis Hash 命令接口
/// </summary>
public interface IHashCommand
{
    /// <summary>
    /// 删除一个或多个哈希表字段
    /// </summary>
    long HDel<T>(string key, params T[] hashFields);

    /// <summary>
    /// 删除一个或多个哈希表字段
    /// </summary>
    Task<long> HDelAsync<T>(string key, params T[] hashFields);

    /// <summary>
    /// 查看哈希表 key 中，指定的字段是否存在
    /// </summary>
    bool HExists<T>(string key, T hashField);

    /// <summary>
    /// 查看哈希表 key 中，指定的字段是否存在
    /// </summary>
    Task<bool> HExistsAsync<T>(string key, T hashField);

    /// <summary>
    /// 获取存储在哈希表中指定字段的值
    /// </summary>
    TVal? HGet<TKey, TVal>(string key, TKey hashField);

    /// <summary>
    /// 获取存储在哈希表中指定字段的值
    /// </summary>
    Task<TVal?> HGetAsync<TKey, TVal>(string key, TKey hashField);

    /// <summary>
    /// 获取在哈希表中指定 key 的所有字段和值
    /// </summary>
    Dictionary<TKey, TVal?> HGetAll<TKey, TVal>(string key) where TKey : notnull;

    /// <summary>
    /// 获取在哈希表中指定 key 的所有字段和值
    /// </summary>
    Task<Dictionary<TKey, TVal?>> HGetAllAsync<TKey, TVal>(string key) where TKey : notnull;

    /// <summary>
    /// 获取所有哈希表中的字段
    /// </summary>
    T?[] HKeys<T>(string key);

    /// <summary>
    /// 获取所有哈希表中的字段
    /// </summary>
    Task<T?[]> HKeysAsync<T>(string key);

    /// <summary>
    /// 获取哈希表中字段的数量
    /// </summary>
    long HLen(string key);

    /// <summary>
    /// 获取哈希表中字段的数量
    /// </summary>
    Task<long> HLenAsync(string key);

    /// <summary>
    /// 将哈希表 key 中的字段 field 的值设为 value
    /// </summary>
    bool HSet<TKey, TVal>(string key, TKey hashField, TVal hashVal);

    /// <summary>
    /// 将哈希表 key 中的字段 field 的值设为 value
    /// </summary>
    Task<bool> HSetAsync<TKey, TVal>(string key, TKey hashField, TVal hashVal);

    /// <summary>
    /// 同时将多个 field-value (域-值)对设置到哈希表 key 中
    /// </summary>
    void HSet<TKey, TVal>(string key, Dictionary<TKey, TVal> hashData) where TKey : notnull;

    /// <summary>
    /// 同时将多个 field-value (域-值)对设置到哈希表 key 中
    /// </summary>
    Task HSetAsync<TKey, TVal>(string key, Dictionary<TKey, TVal> hashData) where TKey : notnull;

    /// <summary>
    /// 只有在字段 field 不存在时，设置哈希表字段的值
    /// </summary>
    bool HSetNx<TKey, TVal>(string key, TKey hashField, TVal hashVal);

    /// <summary>
    /// 只有在字段 field 不存在时，设置哈希表字段的值
    /// </summary>
    Task<bool> HSetNxAsync<TKey, TVal>(string key, TKey hashField, TVal hashVal);
}

