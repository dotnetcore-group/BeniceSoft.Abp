using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// Redis 分布式锁扩展方法
/// </summary>
public static class RedisLockExtensions
{
    #region Single Client - TryLock with Action/Func
    /// <summary>
    /// 尝试获取分布式锁并执行操作
    /// </summary>
    public static async Task<bool> TryLockAsync(
        this RedisClient client,
        string resource,
        Action action,
        int expirySeconds = 10,
        int waitSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var profile = new RedisLockProfile
        {
            Resource = resource,
            ExpiryTime = TimeSpan.FromSeconds(expirySeconds),
            WaitTime = TimeSpan.FromSeconds(waitSeconds)
        };

        return await TryLockAsync(client, profile, action, cancellationToken);
    }

    /// <summary>
    /// 尝试获取分布式锁并执行异步操作
    /// </summary>
    public static async Task<bool> TryLockAsync(
        this RedisClient client,
        string resource,
        Func<Task> task,
        int expirySeconds = 10,
        int waitSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        var profile = new RedisLockProfile
        {
            Resource = resource,
            ExpiryTime = TimeSpan.FromSeconds(expirySeconds),
            WaitTime = TimeSpan.FromSeconds(waitSeconds)
        };

        return await TryLockAsync(client, profile, task, cancellationToken);
    }

    /// <summary>
    /// 尝试获取分布式锁并执行操作
    /// </summary>
    public static async Task<bool> TryLockAsync(
        this RedisClient client,
        RedisLockProfile profile,
        Action action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var redisLock = await CreateLockAsync(new[] { client }, profile, null, cancellationToken);
        if (redisLock.IsAcquired)
        {
            action();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 尝试获取分布式锁并执行异步操作
    /// </summary>
    public static async Task<bool> TryLockAsync(
        this RedisClient client,
        RedisLockProfile profile,
        Func<Task> task,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        using var redisLock = await CreateLockAsync(new[] { client }, profile, null, cancellationToken);
        if (redisLock.IsAcquired)
        {
            await task();
            return true;
        }

        return false;
    }
    #endregion

    #region Single Client - TryLock with Result
    /// <summary>
    /// 尝试获取分布式锁并执行操作，返回结果
    /// </summary>
    public static async Task<(bool Success, T? Result)> TryLockAsync<T>(
        this RedisClient client,
        string resource,
        Func<Task<T>> task,
        int expirySeconds = 10,
        int waitSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        var profile = new RedisLockProfile
        {
            Resource = resource,
            ExpiryTime = TimeSpan.FromSeconds(expirySeconds),
            WaitTime = TimeSpan.FromSeconds(waitSeconds)
        };

        return await TryLockAsync(client, profile, task, cancellationToken);
    }

    /// <summary>
    /// 尝试获取分布式锁并执行操作，返回结果
    /// </summary>
    public static async Task<(bool Success, T? Result)> TryLockAsync<T>(
        this RedisClient client,
        string resource,
        Func<T> func,
        int expirySeconds = 10,
        int waitSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);

        var profile = new RedisLockProfile
        {
            Resource = resource,
            ExpiryTime = TimeSpan.FromSeconds(expirySeconds),
            WaitTime = TimeSpan.FromSeconds(waitSeconds)
        };

        return await TryLockAsync(client, profile, func, cancellationToken);
    }

    /// <summary>
    /// 尝试获取分布式锁并执行异步操作，返回结果
    /// </summary>
    public static async Task<(bool Success, T? Result)> TryLockAsync<T>(
        this RedisClient client,
        RedisLockProfile profile,
        Func<Task<T>> task,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        using var redisLock = await CreateLockAsync(new[] { client }, profile, null, cancellationToken);
        if (redisLock.IsAcquired)
        {
            var result = await task();
            return (true, result);
        }

        return (false, default);
    }

    /// <summary>
    /// 尝试获取分布式锁并执行操作，返回结果
    /// </summary>
    public static async Task<(bool Success, T? Result)> TryLockAsync<T>(
        this RedisClient client,
        RedisLockProfile profile,
        Func<T> func,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);

        using var redisLock = await CreateLockAsync(new[] { client }, profile, null, cancellationToken);
        if (redisLock.IsAcquired)
        {
            var result = func();
            return (true, result);
        }

        return (false, default);
    }
    #endregion

    #region Single Client - CreateLock
    /// <summary>
    /// 创建分布式锁
    /// </summary>
    public static IRedisLock CreateLock(
        this RedisClient client,
        string resource,
        int expirySeconds = 10,
        int waitSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        var profile = new RedisLockProfile
        {
            Resource = resource,
            ExpiryTime = TimeSpan.FromSeconds(expirySeconds),
            WaitTime = TimeSpan.FromSeconds(waitSeconds)
        };

        return CreateLock(new[] { client }, profile, null, cancellationToken);
    }

    /// <summary>
    /// 创建分布式锁
    /// </summary>
    public static Task<IRedisLock> CreateLockAsync(
        this RedisClient client,
        string resource,
        int expirySeconds = 10,
        int waitSeconds = 0,
        CancellationToken cancellationToken = default)
    {
        var profile = new RedisLockProfile
        {
            Resource = resource,
            ExpiryTime = TimeSpan.FromSeconds(expirySeconds),
            WaitTime = TimeSpan.FromSeconds(waitSeconds)
        };

        return CreateLockAsync(new[] { client }, profile, null, cancellationToken);
    }

    /// <summary>
    /// 创建分布式锁
    /// </summary>
    public static IRedisLock CreateLock(
        this RedisClient client,
        RedisLockProfile profile,
        CancellationToken cancellationToken = default)
    {
        return CreateLock(new[] { client }, profile, null, cancellationToken);
    }

    /// <summary>
    /// 创建分布式锁
    /// </summary>
    public static Task<IRedisLock> CreateLockAsync(
        this RedisClient client,
        RedisLockProfile profile,
        CancellationToken cancellationToken = default)
    {
        return CreateLockAsync(new[] { client }, profile, null, cancellationToken);
    }
    #endregion

    #region Multiple Clients - CreateLock
    /// <summary>
    /// 创建分布式锁（支持多个 Redis 实例，实现 RedLock 算法）
    /// </summary>
    public static IRedisLock CreateLock(
        this IEnumerable<RedisClient> clients,
        RedisLockProfile profile,
        ILogger<RedisLock>? logger = null,
        CancellationToken cancellationToken = default)
    {
        return RedisLock.Create(clients, profile, logger, cancellationToken);
    }

    /// <summary>
    /// 创建分布式锁（支持多个 Redis 实例，实现 RedLock 算法）
    /// </summary>
    public static async Task<IRedisLock> CreateLockAsync(
        this IEnumerable<RedisClient> clients,
        RedisLockProfile profile,
        ILogger<RedisLock>? logger = null,
        CancellationToken cancellationToken = default)
    {
        return await RedisLock.CreateAsync(clients, profile, logger, cancellationToken);
    }
    #endregion
}
