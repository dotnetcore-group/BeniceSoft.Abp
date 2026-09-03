using BeniceSoft.Abp.Extensions.RateLimiting.Abstractions;
using BeniceSoft.Abp.Extensions.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Volo.Abp.DependencyInjection;

namespace BeniceSoft.Abp.Extensions.RateLimiting;

/// <summary>
/// 基于 Redis 的令牌桶限流器
/// </summary>
public class TokenBucketRateLimiter : IRateLimiter, ISingletonDependency
{
    private readonly IRedisConnection _redisConnection;
    private readonly ILogger<TokenBucketRateLimiter> _logger;
    private readonly RateLimitOptions _options;

    /// <summary>
    /// 令牌桶 Lua 脚本（原子操作）
    /// 使用 @参数名 语法配合 LuaScript.Prepare
    /// </summary>
    private const string TokenBucketScript = """
        local capacity = tonumber(@capacity)
        local tokens_per_second = tonumber(@tokens_per_second)
        local now = tonumber(@now)
        local requested = tonumber(@requested)

        local bucket = redis.call('HMGET', @key, 'tokens', 'last_time')
        local tokens = tonumber(bucket[1]) or capacity
        local last_time = tonumber(bucket[2]) or now

        -- 计算补充的令牌
        local elapsed = (now - last_time) / 1000
        local refill = elapsed * tokens_per_second
        tokens = math.min(capacity, tokens + refill)

        -- 检查是否有足够令牌
        if tokens >= requested then
            tokens = tokens - requested
            redis.call('HMSET', @key, 'tokens', tokens, 'last_time', now)
            redis.call('EXPIRE', @key, math.ceil(capacity / tokens_per_second) + 60)
            return {1, math.floor(tokens)}
        else
            redis.call('HMSET', @key, 'tokens', tokens, 'last_time', now)
            redis.call('EXPIRE', @key, math.ceil(capacity / tokens_per_second) + 60)
            return {0, math.floor(tokens)}
        end
        """;

    private LoadedLuaScript? _loadedScript;
    private readonly object _scriptLock = new();

    public TokenBucketRateLimiter(
        IRedisConnection redisConnection,
        ILogger<TokenBucketRateLimiter> logger,
        IOptions<RateLimitOptions> options)
    {
        _redisConnection = redisConnection;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<RateLimitResult> TryAcquireAsync(
        string key,
        int permitLimit,
        int windowSeconds,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return RateLimitResult.Allowed(permitLimit, permitLimit);
        }

        var tokensPerSecond = (double)permitLimit / windowSeconds;
        var bucketCapacity = permitLimit;

        try
        {
            var db = _redisConnection.TryConnect().GetDatabase();
            var script = GetOrLoadScript(db);

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var redisKey = $"{_options.KeyPrefix}:{key}";

            var result = (RedisValue[]?)await script.EvaluateAsync(db, new
            {
                key = (RedisKey)redisKey,
                capacity = bucketCapacity,
                tokens_per_second = tokensPerSecond,
                now,
                requested = 1
            });

            if (result == null || result.Length < 2)
            {
                _logger.LogWarning("令牌桶脚本返回结果异常，放行: Key={Key}", key);
                return RateLimitResult.Allowed(permitLimit, permitLimit);
            }

            var isAllowed = (int)result[0] == 1;
            var remaining = (long)result[1];

            if (isAllowed)
            {
                return RateLimitResult.Allowed(remaining, permitLimit);
            }


            var retryAfter = Math.Max(1, (int)Math.Ceiling(1.0 / tokensPerSecond));
            return RateLimitResult.Rejected(retryAfter, permitLimit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "令牌桶限流器执行出错，放行: Key={Key}", key);

            return RateLimitResult.Allowed(permitLimit, permitLimit);
        }
    }

    private LoadedLuaScript GetOrLoadScript(IDatabase db)
    {
        if (_loadedScript != null) return _loadedScript;

        lock (_scriptLock)
        {
            if (_loadedScript != null) return _loadedScript;

            var prepared = LuaScript.Prepare(TokenBucketScript);
            var server = db.Multiplexer.GetServer(db.Multiplexer.GetEndPoints()[0]);
            _loadedScript = prepared.Load(server);
        }

        return _loadedScript;
    }
}

