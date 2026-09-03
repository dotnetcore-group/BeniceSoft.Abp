using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// Redis 客户端
/// 由于Redis多路复用的原因，会导致连接不足，尽量使用异步方法
/// </summary>
public class RedisClient : IKeyCommand, IListCommand, IStringCommand, IHashCommand, ISetCommand, ISortedSetCommand
{
    private readonly IRedisConnection _connection;
    private readonly int _dbIndex;
    private readonly ILogger<RedisClient>? _logger;

    public RedisClient(IRedisConnection connection, int dbIndex = -1, ILogger<RedisClient>? logger = null)
    {
        _connection = connection;
        _dbIndex = dbIndex;
        _logger = logger;
    }

    #region Members
    public IConnectionMultiplexer Connection => _connection.TryConnect();

    public IDatabase Database => Connection.GetDatabase(_dbIndex);

    /// <summary>
    /// current database index
    /// </summary>
    public int DatabaseIndex { get; set; }

    public IKeyCommand Key => this;

    public IStringCommand String => this;

    public IListCommand List => this;

    public IHashCommand Hash => this;

    public ISetCommand HashSet => this;

    public ISortedSetCommand SortedSet => this;
    #endregion

    #region Key Commands
    public long Del(params string[] keys)
    {
        return Database.KeyDelete(keys.ToKeys());
    }

    public async Task<long> DelAsync(params string[] keys)
    {
        return await Database.KeyDeleteAsync(keys.ToKeys());
    }

    public bool Exists(string key)
    {
        return Database.KeyExists(key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await Database.KeyExistsAsync(key);
    }

    public bool Expire(string key, double sec)
    {
        return Database.KeyExpire(key, TimeSpan.FromSeconds(sec));
    }

    public async Task<bool> ExpireAsync(string key, double sec)
    {
        return await Database.KeyExpireAsync(key, TimeSpan.FromSeconds(sec));
    }

    public bool Persist(string key)
    {
        return Database.KeyPersist(key);
    }

    public async Task<bool> PersistAsync(string key)
    {
        return await Database.KeyPersistAsync(key);
    }

    public double TTL(string key)
    {
        return Database.KeyTimeToLive(key).GetValueOrDefault().TotalSeconds;
    }

    public async Task<double> TTLAsync(string key)
    {
        var ts = await Database.KeyTimeToLiveAsync(key);
        return ts.GetValueOrDefault().TotalSeconds;
    }

    public string Type(string key)
    {
        return Database.KeyType(key).ToString();
    }

    public async Task<string> TypeAsync(string key)
    {
        var type = await Database.KeyTypeAsync(key);
        return type.ToString();
    }
    #endregion

    #region List Commands
    public T? LIndex<T>(string key, long index)
    {
        return Database.ListGetByIndex(key, index).ToObject<T>();
    }

    public long LLen(string key)
    {
        return Database.ListLength(key);
    }

    public T? LPop<T>(string key)
    {
        return Database.ListLeftPop(key).ToObject<T>();
    }

    public long LPush<T>(string key, params T[] values)
    {
        return Database.ListLeftPush(key, values.ToValues(), When.Always);
    }

    public T?[] LRange<T>(string key, long start, long stop)
    {
        return Database.ListRange(key, start, stop).ToObjects<T>();
    }

    public long LRem<T>(string key, T value, long count = 0)
    {
        return Database.ListRemove(key, value.ToValue(), count);
    }

    public void LSet<T>(string key, T value, long index)
    {
        Database.ListSetByIndex(key, index, value.ToValue());
    }

    public void LTrim(string key, long start, long stop)
    {
        Database.ListTrim(key, start, stop);
    }

    public T? RPop<T>(string key)
    {
        return Database.ListRightPop(key).ToObject<T>();
    }

    public long RPush<T>(string key, params T[] values)
    {
        return Database.ListRightPush(key, values.ToValues(), When.Always);
    }

    public async Task<T?> LIndexAsync<T>(string key, long index)
    {
        var value = await Database.ListGetByIndexAsync(key, index);
        return value.ToObject<T>();
    }

    public Task<long> LLenAsync(string key)
    {
        return Database.ListLengthAsync(key);
    }

    public async Task<T?> LPopAsync<T>(string key)
    {
        var value = await Database.ListLeftPopAsync(key);
        return value.ToObject<T>();
    }

    public Task<long> LPushAsync<T>(string key, params T[] values)
    {
        return Database.ListLeftPushAsync(key, values.ToValues(), When.Always);
    }

    public async Task<T?[]> LRangeAsync<T>(string key, long start, long stop)
    {
        var value = await Database.ListRangeAsync(key, start, stop);
        return value.ToObjects<T>();
    }

    public async Task<long> LRemAsync<T>(string key, T value, long count = 0)
    {
        var result = await Database.ListRemoveAsync(key, value.ToValue(), count);
        return result;
    }

    public Task LSetAsync<T>(string key, T value, long index)
    {
        return Database.ListSetByIndexAsync(key, index, value.ToValue());
    }

    public Task LTrimAsync(string key, long start, long stop)
    {
        return Database.ListTrimAsync(key, start, stop);
    }

    public async Task<T?> RPopAsync<T>(string key)
    {
        var value = await Database.ListRightPopAsync(key);
        return value.ToObject<T>();
    }

    public Task<long> RPushAsync<T>(string key, params T[] values)
    {
        return Database.ListRightPushAsync(key, values.ToValues(), When.Always);
    }
    #endregion

    #region String Commands
    public T? Get<T>(string key)
    {
        return Database.StringGet(key).ToObject<T>();
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await Database.StringGetAsync(key);
        return value.ToObject<T>();
    }

    public bool Set<T>(string key, T value, TimeSpan? ttl = null)
    {
        return Database.StringSet(key, value.ToValue(), ttl, false);
    }

    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        return await Database.StringSetAsync(key, value.ToValue(), ttl, false);
    }

    public bool SetNx<T>(string key, T value, TimeSpan? ttl = null)
    {
        return Database.StringSet(key, value.ToValue(), ttl, false, When.NotExists);
    }

    public async Task<bool> SetNxAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        return await Database.StringSetAsync(key, value.ToValue(), ttl, false, When.NotExists);
    }

    public bool SetEx<T>(string key, T value, TimeSpan? ttl = null)
    {
        return Database.StringSet(key, value.ToValue(), ttl, false, When.Exists);
    }

    public async Task<bool> SetExAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        return await Database.StringSetAsync(key, value.ToValue(), ttl, false, When.Exists);
    }

    public long Incr(string key)
    {
        return Database.StringIncrement(key);
    }

    public async Task<long> IncrAsync(string key)
    {
        return await Database.StringIncrementAsync(key);
    }

    public long IncrBy(string key, long count)
    {
        return Database.StringIncrement(key, count);
    }

    public async Task<long> IncrByAsync(string key, long count)
    {
        return await Database.StringIncrementAsync(key, count);
    }

    public long Decr(string key)
    {
        return Database.StringDecrement(key);
    }

    public async Task<long> DecrAsync(string key)
    {
        return await Database.StringDecrementAsync(key);
    }

    public long DecrBy(string key, long count)
    {
        return Database.StringDecrement(key, count);
    }

    public async Task<long> DecrByAsync(string key, long count)
    {
        return await Database.StringDecrementAsync(key, count);
    }
    #endregion

    #region Hash Commands
    public long HDel<T>(string key, params T[] hashFields)
    {
        return Database.HashDelete(key, hashFields.ToValues());
    }

    public async Task<long> HDelAsync<T>(string key, params T[] hashFields)
    {
        return await Database.HashDeleteAsync(key, hashFields.ToValues());
    }

    public bool HExists<T>(string key, T hashField)
    {
        return Database.HashExists(key, hashField.ToValue());
    }

    public async Task<bool> HExistsAsync<T>(string key, T hashField)
    {
        return await Database.HashExistsAsync(key, hashField.ToValue());
    }

    public TVal? HGet<TKey, TVal>(string key, TKey hashField)
    {
        return Database.HashGet(key, hashField.ToValue()).ToObject<TVal>();
    }

    public async Task<TVal?> HGetAsync<TKey, TVal>(string key, TKey hashField)
    {
        var value = await Database.HashGetAsync(key, hashField.ToValue());
        return value.ToObject<TVal>();
    }

    public Dictionary<TKey, TVal?> HGetAll<TKey, TVal>(string key) where TKey : notnull
    {
        return Database.HashGetAll(key).ToDictionary(t => t.Name.ToObject<TKey>()!, t => t.Value.ToObject<TVal>());
    }

    public async Task<Dictionary<TKey, TVal?>> HGetAllAsync<TKey, TVal>(string key) where TKey : notnull
    {
        var value = await Database.HashGetAllAsync(key);
        return value.ToDictionary(t => t.Name.ToObject<TKey>()!, t => t.Value.ToObject<TVal>());
    }

    public T?[] HKeys<T>(string key)
    {
        return Database.HashKeys(key).ToObjects<T>();
    }

    public async Task<T?[]> HKeysAsync<T>(string key)
    {
        var value = await Database.HashKeysAsync(key);
        return value.ToObjects<T>();
    }

    public long HLen(string key)
    {
        return Database.HashLength(key);
    }

    public async Task<long> HLenAsync(string key)
    {
        return await Database.HashLengthAsync(key);
    }

    public bool HSet<TKey, TVal>(string key, TKey hashField, TVal hashVal)
    {
        return Database.HashSet(key, hashField.ToValue(), hashVal.ToValue());
    }

    public async Task<bool> HSetAsync<TKey, TVal>(string key, TKey hashField, TVal hashVal)
    {
        return await Database.HashSetAsync(key, hashField.ToValue(), hashVal.ToValue());
    }

    public void HSet<TKey, TVal>(string key, Dictionary<TKey, TVal> hashData) where TKey : notnull
    {
        Database.HashSet(key, hashData.Select(p => new HashEntry(p.Key.ToValue(), p.Value.ToValue())).ToArray());
    }

    public async Task HSetAsync<TKey, TVal>(string key, Dictionary<TKey, TVal> hashData) where TKey : notnull
    {
        await Database.HashSetAsync(key, hashData.Select(p => new HashEntry(p.Key.ToValue(), p.Value.ToValue())).ToArray());
    }

    public bool HSetNx<TKey, TVal>(string key, TKey hashField, TVal hashVal)
    {
        return Database.HashSet(key, hashField.ToValue(), hashVal.ToValue(), When.NotExists);
    }

    public async Task<bool> HSetNxAsync<TKey, TVal>(string key, TKey hashField, TVal hashVal)
    {
        return await Database.HashSetAsync(key, hashField.ToValue(), hashVal.ToValue(), When.NotExists);
    }
    #endregion

    #region Set Commands
    public long SAdd<T>(string key, params T[] members)
    {
        return Database.SetAdd(key, members.ToValues());
    }

    public async Task<long> SAddAsync<T>(string key, params T[] members)
    {
        return await Database.SetAddAsync(key, members.ToValues());
    }

    public long SCard(string key)
    {
        return Database.SetLength(key);
    }

    public async Task<long> SCardAsync(string key)
    {
        return await Database.SetLengthAsync(key);
    }

    public HashSet<T?> SMembers<T>(string key)
    {
        return Database.SetMembers(key).ToSetObject<T>();
    }

    public async Task<HashSet<T?>> SMembersAsync<T>(string key)
    {
        var value = await Database.SetMembersAsync(key);
        return value.ToSetObject<T>();
    }

    public bool SIsMember<T>(string key, T member)
    {
        return Database.SetContains(key, member.ToValue());
    }

    public async Task<bool> SIsMemberAsync<T>(string key, T member)
    {
        return await Database.SetContainsAsync(key, member.ToValue());
    }

    public long SRem<T>(string key, params T[] members)
    {
        return Database.SetRemove(key, members.ToValues());
    }

    public async Task<long> SRemAsync<T>(string key, params T[] members)
    {
        return await Database.SetRemoveAsync(key, members.ToValues());
    }

    public T? SPop<T>(string key)
    {
        return Database.SetPop(key).ToObject<T>();
    }

    public async Task<T?> SPopAsync<T>(string key)
    {
        var value = await Database.SetPopAsync(key);
        return value.ToObject<T>();
    }

    public T? SRandMember<T>(string key)
    {
        return Database.SetRandomMember(key).ToObject<T>();
    }

    public async Task<T?> SRandMemberAsync<T>(string key)
    {
        var value = await Database.SetRandomMemberAsync(key);
        return value.ToObject<T>();
    }

    public HashSet<T?> SDiff<T>(params string[] keys)
    {
        return Database.SetCombine(SetOperation.Difference, keys.ToKeys()).ToSetObject<T>();
    }

    public async Task<HashSet<T?>> SDiffAsync<T>(params string[] keys)
    {
        var value = await Database.SetCombineAsync(SetOperation.Difference, keys.ToKeys());
        return value.ToSetObject<T>();
    }

    public HashSet<T?> SInter<T>(params string[] keys)
    {
        return Database.SetCombine(SetOperation.Intersect, keys.ToKeys()).ToSetObject<T>();
    }

    public async Task<HashSet<T?>> SInterAsync<T>(params string[] keys)
    {
        var value = await Database.SetCombineAsync(SetOperation.Intersect, keys.ToKeys());
        return value.ToSetObject<T>();
    }

    public HashSet<T?> SUnion<T>(params string[] keys)
    {
        return Database.SetCombine(SetOperation.Union, keys.ToKeys()).ToSetObject<T>();
    }

    public async Task<HashSet<T?>> SUnionAsync<T>(params string[] keys)
    {
        var value = await Database.SetCombineAsync(SetOperation.Union, keys.ToKeys());
        return value.ToSetObject<T>();
    }

    public bool SMove<T>(string source, string destination, T member)
    {
        return Database.SetMove(source, destination, member.ToValue());
    }

    public async Task<bool> SMoveAsync<T>(string source, string destination, T member)
    {
        return await Database.SetMoveAsync(source, destination, member.ToValue());
    }

    /// <summary>
    /// 执行脚本（Lua）
    /// </summary>
    public RedisResult ScriptEvaluate<T>(string script, string[] key, T[] value)
    {
        var result = Database.ScriptEvaluate(script, key.ToKeys(), value.ToValues(), CommandFlags.DemandMaster);
        return result;
    }

    /// <summary>
    /// 执行脚本（Lua）
    /// </summary>
    public async Task<RedisResult> ScriptEvaluateAsync<T>(string script, string[] key, T[] value)
    {
        var result = await Database.ScriptEvaluateAsync(script, key.ToKeys(), value.ToValues(), CommandFlags.DemandMaster);
        return result;
    }
    #endregion

    #region SortSet Commands

    public long ZAdd<T>(string key, Dictionary<T, double> members) where T : notnull
    {
        return Database.SortedSetAdd(key, members.Select(p => new SortedSetEntry(p.Key.ToValue(), p.Value)).ToArray());
    }

    public long ZCard(string key)
    {
        return Database.SortedSetLength(key);
    }

    public long ZCount(string key, double min, double max)
    {
        return Database.SortedSetLength(key, min, max, Exclude.Both);
    }

    public double ZIncrBy<T>(string key, T member, double score)
    {
        return Database.SortedSetIncrement(key, member.ToValue(), score);
    }

    public Dictionary<T, double> ZRange<T>(string key, long start, long stop) where T : notnull
    {
        return Database.SortedSetRangeByRankWithScores(key, start, stop).ToDictionary(t => t.Element.ToObject<T>()!, t => t.Score);
    }

    public Dictionary<T, double> ZRangeByScore<T>(string key, double min, double max) where T : notnull
    {
        return Database.SortedSetRangeByScoreWithScores(key, min, max).ToDictionary(t => t.Element.ToObject<T>()!, t => t.Score);
    }

    public long? ZRank<T>(string key, T member)
    {
        var result = Database.SortedSetRank(key, member.ToValue());
        if (!result.HasValue)
        {
            return null;
        }

        return result.Value;
    }

    public long ZRem<T>(string key, params T[] members)
    {
        return Database.SortedSetRemove(key, members.ToValues());
    }

    public long ZRemRangeByRank(string key, long start, long stop)
    {
        return Database.SortedSetRemoveRangeByRank(key, start, stop);
    }

    public long ZRemRangeByScore(string key, double min, double max)
    {
        return Database.SortedSetRemoveRangeByScore(key, min, max);
    }

    public Dictionary<T, double> ZRevRange<T>(string key, long start, long stop) where T : notnull
    {
        return Database.SortedSetRangeByRankWithScores(key, start, stop, Order.Descending).ToDictionary(t => t.Element.ToObject<T>()!, t => t.Score);
    }

    public Dictionary<T, double> ZRevRangeByScore<T>(string key, double min, double max) where T : notnull
    {
        return Database.SortedSetRangeByScoreWithScores(key, min, max, Exclude.Both, Order.Descending).ToDictionary(t => t.Element.ToObject<T>()!, t => t.Score);
    }

    public long? ZRevRank<T>(string key, T member)
    {
        var result = Database.SortedSetRank(key, member.ToValue(), Order.Descending);
        if (!result.HasValue)
        {
            return null;
        }

        return result.Value;
    }

    public double? ZScore<T>(string key, T member)
    {
        return Database.SortedSetScore(key, member.ToValue());
    }

    public Task<long> ZAddAsync<T>(string key, Dictionary<T, double> members) where T : notnull
    {
        return Database.SortedSetAddAsync(key, members.Select(p => new SortedSetEntry(p.Key.ToValue(), p.Value)).ToArray());
    }

    public Task<long> ZCardAsync(string key)
    {
        return Database.SortedSetLengthAsync(key);
    }

    public Task<long> ZCountAsync(string key, double min, double max)
    {
        return Database.SortedSetLengthAsync(key, min, max, Exclude.Both);
    }

    public Task<double> ZIncrByAsync<T>(string key, T member, double score)
    {
        return Database.SortedSetIncrementAsync(key, member.ToValue(), score);
    }

    public async Task<Dictionary<T, double>> ZRangeAsync<T>(string key, long start, long stop) where T : notnull
    {
        var value = await Database.SortedSetRangeByRankWithScoresAsync(key, start, stop);
        return value.ToDictionary(t => t.Element.ToObject<T>()!, t => t.Score);
    }

    public async Task<Dictionary<T, double>> ZRangeByScoreAsync<T>(string key, double min, double max) where T : notnull
    {
        var value = await Database.SortedSetRangeByScoreWithScoresAsync(key, min, max);
        return value.ToDictionary(t => t.Element.ToObject<T>()!, t => t.Score);
    }

    public async Task<long?> ZRankAsync<T>(string key, T member)
    {
        var result = await Database.SortedSetRankAsync(key, member.ToValue());
        if (!result.HasValue)
        {
            return null;
        }

        return result.Value;
    }

    public Task<long> ZRemAsync<T>(string key, params T[] members)
    {
        return Database.SortedSetRemoveAsync(key, members.ToValues());
    }

    public Task<long> ZRemRangeByRankAsync(string key, long start, long stop)
    {
        return Database.SortedSetRemoveRangeByRankAsync(key, start, stop);
    }

    public Task<long> ZRemRangeByScoreAsync(string key, double min, double max)
    {
        return Database.SortedSetRemoveRangeByScoreAsync(key, min, max);
    }

    public async Task<Dictionary<T, double>> ZRevRangeAsync<T>(string key, long start, long stop) where T : notnull
    {
        var value = await Database.SortedSetRangeByRankWithScoresAsync(key, start, stop, Order.Descending);
        return value.ToDictionary(t => t.Element.ToObject<T>()!, t => t.Score);
    }

    public async Task<Dictionary<T, double>> ZRevRangeByScoreAsync<T>(string key, double min, double max) where T : notnull
    {
        var value = await Database.SortedSetRangeByScoreWithScoresAsync(key, min, max, Exclude.Both, Order.Descending);
        return value.ToDictionary(t => t.Element.ToObject<T>()!, t => t.Score);
    }

    public async Task<long?> ZRevRankAsync<T>(string key, T member)
    {
        var result = await Database.SortedSetRankAsync(key, member.ToValue(), Order.Descending);
        if (!result.HasValue)
        {
            return null;
        }

        return result.Value;
    }

    public Task<double?> ZScoreAsync<T>(string key, T member)
    {
        return Database.SortedSetScoreAsync(key, member.ToValue());
    }

    #endregion

    #region Distributed Lock
    /// <summary>
    /// 尝试获取分布式锁
    /// </summary>
    /// <param name="key">锁的键名</param>
    /// <param name="expirySeconds">锁的过期时间（秒）</param>
    /// <param name="value">锁的值（可选，默认使用 GUID）</param>
    /// <returns>锁对象，如果获取失败返回 null</returns>
    public IRedisLock? Lock(string key, int expirySeconds, string? value = null)
    {
        var profile = new RedisLockProfile
        {
            Resource = key,
            ExpiryTime = TimeSpan.FromSeconds(expirySeconds),
            LockId = value,
            WaitTime = TimeSpan.Zero
        };

        var redisLock = RedisLock.Create(new[] { this }, profile, _logger as ILogger<RedisLock>);
        return redisLock.IsAcquired ? redisLock : null;
    }

    /// <summary>
    /// 异步尝试获取分布式锁
    /// </summary>
    /// <param name="key">锁的键名</param>
    /// <param name="expirySeconds">锁的过期时间（秒）</param>
    /// <param name="value">锁的值（可选，默认使用 GUID）</param>
    /// <returns>锁对象，如果获取失败返回 null</returns>
    public async Task<IRedisLock?> LockAsync(string key, int expirySeconds, string? value = null)
    {
        var profile = new RedisLockProfile
        {
            Resource = key,
            ExpiryTime = TimeSpan.FromSeconds(expirySeconds),
            LockId = value,
            WaitTime = TimeSpan.Zero
        };

        var redisLock = await RedisLock.CreateAsync(new[] { this }, profile, _logger as ILogger<RedisLock>);
        return redisLock.IsAcquired ? redisLock : null;
    }
    #endregion
}

