using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BeniceSoft.Abp.Extensions.Redis;

/// <summary>
/// 分布式锁是一个全局性的稀有资源，一旦加锁或者锁的粒度较大，对于分布式的应用程序来说性能是非常差的。
/// 对于业务来说，我们优先考虑不采用锁，通过业务协调方式解决。
/// 在一些必须，强一致性的情况下才考虑锁的使用。需要加锁的业务逻辑应该是细小的，加锁前要过滤一部分的重复业务逻辑，这样锁才不会很频繁。
/// </summary>
public class RedisLock : IRedisLock
{
    #region Lua Scripts
    private static readonly string UnlockScript = GetEmbeddedResource("BeniceSoft.Abp.Extensions.Redis.Lock.Lua.Unlock.lua");
    private static readonly string ExtendScript = GetEmbeddedResource("BeniceSoft.Abp.Extensions.Redis.Lock.Lua.Extend.lua");
    #endregion

    #region Members
    private readonly object _locker = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly CancellationTokenSource _unlockSource = new();
    private readonly int _quorum;
    private readonly int _quorumRetryCount = 5;
    private readonly int _quorumRetryDelayMs = 200;
    private readonly int _retryInterval;
    private readonly int _minimumRetryInterval = 10;
    private readonly double _clockDriftFactor = 0.01;
    private readonly TimeSpan _expiryTime;
    private readonly TimeSpan? _waitTime;
    private readonly TimeSpan _minimumExpiryTime = TimeSpan.FromMilliseconds(10);
    private readonly bool _keepLive;
    private readonly bool _forceLock;
    private readonly CancellationToken _cancellationToken;
    private readonly IEnumerable<RedisClient> _redisClients;
    private readonly string _keyFormat = "lock:{0}";
    private readonly ILogger<RedisLock>? _logger;

    private bool _disposed;
    private PeriodicTimer? _keepaliveTimer;

    public string Resource { get; }
    public string LockId { get; }
    public bool IsAcquired => Status == RedisLockStatus.Acquired;
    public RedisLockStatus Status { get; private set; }
    public RedisLockSummary InstanceSummary { get; private set; }
    public int ExtendCount { get; private set; }
    #endregion

    #region Constructors
    private RedisLock(
        IEnumerable<RedisClient> redisClients,
        RedisLockProfile profile,
        ILogger<RedisLock>? logger = null,
        CancellationToken cancellationToken = default)
    {
        _logger = logger;

        if (profile.ExpiryTime < _minimumExpiryTime)
        {
            _logger?.LogWarning("Expiry time {Expiry}ms too low, setting to {Min}ms",
                profile.ExpiryTime.TotalMilliseconds, _minimumExpiryTime.TotalMilliseconds);
            profile.ExpiryTime = _minimumExpiryTime;
        }

        if (profile.RetryInterval < _minimumRetryInterval)
        {
            _logger?.LogWarning("Retry time {Retry}ms too low, setting to {Min}ms",
                profile.RetryInterval, _minimumRetryInterval);
            profile.RetryInterval = _minimumRetryInterval;
        }

        _redisClients = redisClients;
        _quorum = redisClients.Count() / 2 + 1;
        Resource = profile.Resource;
        LockId = profile.LockId ?? Guid.NewGuid().ToString("N");
        _forceLock = !string.IsNullOrEmpty(profile.LockId);
        _expiryTime = profile.ExpiryTime;


        if (profile.WaitTime > TimeSpan.Zero)
        {
            _waitTime = profile.WaitTime;
        }

        _retryInterval = profile.RetryInterval;
        _keepLive = profile.KeepLive;
        _cancellationToken = cancellationToken;

        if (!string.IsNullOrEmpty(profile.KeyFormat))
        {
            _keyFormat = profile.KeyFormat;
        }
    }
    #endregion

    #region Static Factory Methods
    /// <summary>
    /// 从嵌入式资源加载 Lua 脚本
    /// </summary>
    private static string GetEmbeddedResource(string name)
    {
        var assembly = typeof(RedisLock).Assembly;
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream == null)
        {
            throw new InvalidOperationException($"Embedded resource '{name}' not found.");
        }
        using var streamReader = new StreamReader(stream);
        return streamReader.ReadToEnd();
    }

    /// <summary>
    /// 创建分布式锁（同步）
    /// </summary>
    public static RedisLock Create(
        IEnumerable<RedisClient> redisClients,
        RedisLockProfile profile,
        ILogger<RedisLock>? logger = null,
        CancellationToken cancellationToken = default)
    {
        var redisLock = new RedisLock(redisClients, profile, logger, cancellationToken);
        redisLock.Start();
        return redisLock;
    }

    /// <summary>
    /// 创建分布式锁（异步）
    /// </summary>
    public static async Task<RedisLock> CreateAsync(
        IEnumerable<RedisClient> redisClients,
        RedisLockProfile profile,
        ILogger<RedisLock>? logger = null,
        CancellationToken cancellationToken = default)
    {
        var redisLock = new RedisLock(redisClients, profile, logger, cancellationToken);
        await redisLock.StartAsync();
        return redisLock;
    }
    #endregion

    #region Private Methods
    private long GetRemainingValidityTicks(Stopwatch sw)
    {
        var driftTicks = (long)(_expiryTime.Ticks * _clockDriftFactor) + TimeSpan.FromMilliseconds(2).Ticks;
        var elapsedTimeSpanTicks = sw.ElapsedTicks * TimeSpan.TicksPerSecond / Stopwatch.Frequency;
        var validityTicks = _expiryTime.Ticks - elapsedTimeSpanTicks - driftTicks;
        return validityTicks;
    }


    private RedisLockStatus GetFailedLockStatus(RedisLockSummary lockResult)
    {
        if (lockResult.Acquired >= _quorum)
        {
            // 如果达到了法定数量但仍然失败，说明有效期已过期
            return RedisLockStatus.Expired;
        }

        if (lockResult.Acquired + lockResult.Conflicted >= _quorum)
        {
            // 有足够的实例，但部分被其他 LockId 锁定
            return RedisLockStatus.Conflicted;
        }

        return RedisLockStatus.NoQuorum;
    }

    private static RedisLockSummary PopulateLockResult(IEnumerable<RedisLockResult> instanceResults)
    {
        var acquired = 0;
        var conflicted = 0;
        var error = 0;

        foreach (var instanceResult in instanceResults)
        {
            switch (instanceResult)
            {
                case RedisLockResult.Success:
                    acquired++;
                    break;
                case RedisLockResult.Conflicted:
                    conflicted++;
                    break;
                case RedisLockResult.Error:
                    error++;
                    break;
            }
        }

        return new RedisLockSummary(acquired, conflicted, error);
    }

    private static string GetHost(RedisClient client)
    {
        var connection = client.Connection;
        var result = new StringBuilder();

        foreach (var endPoint in connection.GetEndPoints())
        {
            var server = connection.GetServer(endPoint);

            result.Append(server.EndPoint);
            result.Append(" (");
            result.Append(server.IsReplica ? "slave" : "master");
            result.Append(server.IsConnected ? string.Empty : ", disconnected");
            result.Append("), ");
        }

        return result.ToString().TrimEnd(' ', ',');
    }

    private RedisLockResult LockInstance(RedisClient client)
    {
        var redisKey = string.Format(_keyFormat, Resource);

        RedisLockResult result;
        try
        {
            if (!_forceLock)
            {
                var redisResult = client.String.SetNx(redisKey, LockId, _expiryTime);
                result = redisResult ? RedisLockResult.Success : RedisLockResult.Conflicted;
            }
            else
            {
                result = ExtendInstance(client, _expiryTime);
            }
        }
        catch (Exception ex)
        {
            var host = GetHost(client);
            _logger?.LogError(ex, "Error locking lock instance {Host}: {Exception}", host, ex.Message);
            result = RedisLockResult.Error;
        }

        return result;
    }

    private async Task<RedisLockResult> LockInstanceAsync(RedisClient client)
    {
        var redisKey = string.Format(_keyFormat, Resource);

        RedisLockResult result;
        try
        {
            if (!_forceLock)
            {
                var redisResult = await client.String.SetNxAsync(redisKey, LockId, _expiryTime);
                result = redisResult ? RedisLockResult.Success : RedisLockResult.Conflicted;
            }
            else
            {
                result = await ExtendInstanceAsync(client, _expiryTime);
            }
        }
        catch (Exception ex)
        {
            var host = GetHost(client);
            _logger?.LogError(ex, "Error locking lock instance {Host}: {Exception}", host, ex.Message);
            result = RedisLockResult.Error;
        }

        return result;
    }

    private RedisLockSummary Lock()
    {
        var lockResults = new ConcurrentBag<RedisLockResult>();
        Parallel.ForEach(_redisClients, client => lockResults.Add(LockInstance(client)));
        return PopulateLockResult(lockResults);
    }

    private async Task<RedisLockSummary> LockAsync()
    {
        var lockTasks = _redisClients.Select(LockInstanceAsync);
        var lockResults = await Task.WhenAll(lockTasks);
        return PopulateLockResult(lockResults);
    }

    private void UnlockInstance(RedisClient client)
    {
        var redisKey = string.Format(_keyFormat, Resource);

        try
        {
            var result = (long)client.ScriptEvaluate(UnlockScript, new[] { redisKey }, new[] { LockId });
            _logger?.LogDebug("Unlock instance {Host}, result: {Result}", GetHost(client), result);
        }
        catch (Exception ex)
        {
            var host = GetHost(client);
            _logger?.LogError(ex, "Error unlocking lock instance {Host}: {Exception}", host, ex.Message);
        }
    }

    private async Task UnlockInstanceAsync(RedisClient client)
    {
        var redisKey = string.Format(_keyFormat, Resource);

        try
        {
            var result = (long)await client.ScriptEvaluateAsync(UnlockScript, new[] { redisKey }, new[] { LockId });
            _logger?.LogDebug("Unlock instance {Host}, result: {Result}", GetHost(client), result);
        }
        catch (Exception ex)
        {
            var host = GetHost(client);
            _logger?.LogError(ex, "Error unlocking lock instance {Host}: {Exception}", host, ex.Message);
        }
    }

    private void UnlockAll()
    {
        using (_semaphore.Lock())
        {
            Parallel.ForEach(_redisClients, UnlockInstance);
        }
    }

    private async Task UnlockAllAsync()
    {
        using (await _semaphore.LockAsync())
        {
            var unlockTasks = _redisClients.Select(UnlockInstanceAsync);
            await Task.WhenAll(unlockTasks);
        }
    }

    private (RedisLockStatus, RedisLockSummary) Acquire()
    {
        var lockSummary = new RedisLockSummary();

        for (var i = 0; i < _quorumRetryCount; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            var stopwatch = Stopwatch.StartNew();
            lockSummary = Lock();
            var validityTicks = GetRemainingValidityTicks(stopwatch);
            stopwatch.Stop();

            if (lockSummary.Acquired >= _quorum && validityTicks > 0)
            {
                return (RedisLockStatus.Acquired, lockSummary);
            }

            // 未能获取足够的锁，释放已写入 Redis 的实例（不经过 Unlock 的 IsAcquired 检查）
            UnlockAll();

            // 仅在还有重试次数时休眠
            if (i < _quorumRetryCount - 1)
            {
                var sleepMs = Random.Shared.Next(_quorumRetryDelayMs);
                Task.Delay(sleepMs, _cancellationToken).Wait(_cancellationToken);
            }
        }

        var status = GetFailedLockStatus(lockSummary);
        return (status, lockSummary);
    }

    private async Task<(RedisLockStatus, RedisLockSummary)> AcquireAsync()
    {
        var lockSummary = new RedisLockSummary();

        for (var i = 0; i < _quorumRetryCount; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            var stopwatch = Stopwatch.StartNew();
            lockSummary = await LockAsync();
            var validityTicks = GetRemainingValidityTicks(stopwatch);
            stopwatch.Stop();

            if (lockSummary.Acquired >= _quorum && validityTicks > 0)
            {
                return (RedisLockStatus.Acquired, lockSummary);
            }

            // 未能获取足够的锁，释放已写入 Redis 的实例（不经过 UnlockAsync 的 IsAcquired 检查）
            await UnlockAllAsync();

            // 仅在还有重试次数时休眠
            if (i < _quorumRetryCount - 1)
            {
                var sleepMs = Random.Shared.Next(_quorumRetryDelayMs);
                await Task.Delay(sleepMs, _cancellationToken);
            }
        }

        var status = GetFailedLockStatus(lockSummary);
        return (status, lockSummary);
    }

    private RedisLockResult ExtendInstance(RedisClient client, TimeSpan expiryTime)
    {
        var redisKey = string.Format(_keyFormat, Resource);

        RedisLockResult result;
        try
        {
            // 返回值: 1=成功, 0=创建失败, -1=冲突
            var extendResult = (long)client.ScriptEvaluate(
                ExtendScript,
                new[] { redisKey },
                new object[] { LockId, (long)expiryTime.TotalMilliseconds });


            result = extendResult == 1
                ? RedisLockResult.Success
                : extendResult == -1
                    ? RedisLockResult.Conflicted
                    : RedisLockResult.Error;

            _logger?.LogDebug("Extend instance {Host}, result: {Result}", GetHost(client), extendResult);
        }
        catch (Exception ex)
        {
            var host = GetHost(client);
            _logger?.LogError(ex, "Error extending lock instance {Host}: {Exception}", host, ex.Message);
            result = RedisLockResult.Error;
        }

        return result;
    }

    private async Task<RedisLockResult> ExtendInstanceAsync(RedisClient client, TimeSpan expiryTime)
    {
        var redisKey = string.Format(_keyFormat, Resource);

        RedisLockResult result;
        try
        {
            // 返回值: 1=成功, 0=创建失败, -1=冲突
            var extendResult = (long)await client.ScriptEvaluateAsync(
                ExtendScript,
                new[] { redisKey },
                new object[] { LockId, (long)expiryTime.TotalMilliseconds });


            result = extendResult == 1
                ? RedisLockResult.Success
                : extendResult == -1
                    ? RedisLockResult.Conflicted
                    : RedisLockResult.Error;

            _logger?.LogDebug("Extend instance {Host}, result: {Result}", GetHost(client), extendResult);
        }
        catch (Exception ex)
        {
            var host = GetHost(client);
            _logger?.LogError(ex, "Error extending lock instance {Host}: {Exception}", host, ex.Message);
            result = RedisLockResult.Error;
        }

        return result;
    }

    private async Task<RedisLockSummary> ExtendAllAsync(TimeSpan expiryTime)
    {
        var extendResults = new ConcurrentBag<RedisLockResult>();
        await Parallel.ForEachAsync(_redisClients, async (client, ct) =>
            extendResults.Add(await ExtendInstanceAsync(client, expiryTime)));
        return PopulateLockResult(extendResults);
    }

    private RedisLockSummary ExtendAll(TimeSpan expiryTime)
    {
        var extendResults = new ConcurrentBag<RedisLockResult>();
        Parallel.ForEach(_redisClients, client => extendResults.Add(ExtendInstance(client, expiryTime)));
        return PopulateLockResult(extendResults);
    }

    private async Task StartAutoExtendTimerAsync()
    {
        var interval = (int)_expiryTime.TotalMilliseconds / 2;
        _keepaliveTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(interval));

        try
        {
            while (await _keepaliveTimer.WaitForNextTickAsync(_unlockSource.Token))
            {
                using var lockResult = await _semaphore.LockAsync(10, _unlockSource.Token);

                try
                {
                    if (!lockResult.IsAcquired)
                    {
                        continue;
                    }

                    var stopwatch = Stopwatch.StartNew();
                    var extendSummary = await ExtendAllAsync(_expiryTime);
                    var validityTicks = GetRemainingValidityTicks(stopwatch);
                    stopwatch.Stop();

                    if (extendSummary.Acquired >= _quorum && validityTicks > 0)
                    {
                        Status = RedisLockStatus.Acquired;
                        InstanceSummary = extendSummary;
                        ExtendCount++;
                        _logger?.LogDebug("Auto extend lock success: {Resource}, extend count: {ExtendCount}",
                            Resource, ExtendCount);
                    }
                    else
                    {
                        Status = GetFailedLockStatus(extendSummary);
                        InstanceSummary = extendSummary;
                        _logger?.LogWarning("Failed to extend lock, status: {Status}, summary: {Summary}",
                            Status, InstanceSummary);
                    }
                }
                catch (Exception exception)
                {
                    _logger?.LogError(exception, "Lock renewal timer thread failed: {Resource} ({Id})",
                        Resource, LockId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Auto extend timer error: {Resource} ({Id})", Resource, LockId);
        }
    }

    private void StartKeepAliveInBackground()
    {
        if (!IsAcquired || !_keepLive)
        {
            return;
        }

        _ = StartAutoExtendTimerAsync();
    }

    private void Start()
    {
        if (_waitTime.HasValue && _waitTime.Value.TotalMilliseconds > 0)
        {
            var stopwatch = Stopwatch.StartNew();

            while (!IsAcquired && stopwatch.Elapsed <= _waitTime.Value)
            {
                (Status, InstanceSummary) = Acquire();

                if (!IsAcquired)
                {
                    Task.Delay(_retryInterval, _cancellationToken).Wait(_cancellationToken);
                }
            }

            stopwatch.Stop();
        }
        else
        {
            (Status, InstanceSummary) = Acquire();
        }

        StartKeepAliveInBackground();
    }

    private async Task StartAsync()
    {
        if (_waitTime.HasValue && _waitTime.Value.TotalMilliseconds > 0)
        {
            var stopwatch = Stopwatch.StartNew();

            while (!IsAcquired && stopwatch.Elapsed <= _waitTime.Value)
            {
                (Status, InstanceSummary) = await AcquireAsync();

                if (!IsAcquired)
                {
                    await Task.Delay(_retryInterval, _cancellationToken);
                }
            }

            stopwatch.Stop();
        }
        else
        {
            (Status, InstanceSummary) = await AcquireAsync();
        }

        StartKeepAliveInBackground();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 释放锁（同步）
    /// </summary>
    public bool Unlock()
    {
        if (!IsAcquired || _disposed)
        {
            return false;
        }

        _unlockSource.Cancel();

        try
        {
            UnlockAll();
            Status = RedisLockStatus.Unlocked;
            InstanceSummary = RedisLockSummary.Empty;
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to unlock: {Resource}", Resource);
            return false;
        }
    }

    /// <summary>
    /// 释放锁（异步）
    /// </summary>
    public async Task<bool> UnlockAsync()
    {
        if (!IsAcquired || _disposed)
        {
            return false;
        }

        _unlockSource.Cancel();

        try
        {
            await UnlockAllAsync();
            Status = RedisLockStatus.Unlocked;
            InstanceSummary = RedisLockSummary.Empty;
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to unlock: {Resource}", Resource);
            return false;
        }
    }

    /// <summary>
    /// 续期锁（同步）
    /// </summary>
    public RedisLockResult Extend(TimeSpan? expiryTime = null)
    {
        if (_disposed || !IsAcquired)
        {
            return RedisLockResult.Error;
        }

        try
        {
            var effectiveExpiryTime = expiryTime.GetValueOrDefault(_expiryTime);
            if (effectiveExpiryTime < _minimumExpiryTime)
            {
                effectiveExpiryTime = _minimumExpiryTime;
            }

            var summary = ExtendAll(effectiveExpiryTime);
            if (summary.Acquired >= _quorum)
            {
                ExtendCount++;
                InstanceSummary = summary;
                _logger?.LogDebug("Extend lock success: {Resource}, extend count: {ExtendCount}", Resource, ExtendCount);
                return RedisLockResult.Success;
            }

            Status = GetFailedLockStatus(summary);
            InstanceSummary = summary;
            return RedisLockResult.Conflicted;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to extend lock: {Resource}", Resource);
            return RedisLockResult.Error;
        }
    }


    /// <summary>
    /// 续期锁（异步）
    /// </summary>
    public async Task<RedisLockResult> ExtendAsync(TimeSpan? expiryTime = null)
    {
        if (_disposed || !IsAcquired)
        {
            return RedisLockResult.Error;
        }

        try
        {
            var effectiveExpiryTime = expiryTime.GetValueOrDefault(_expiryTime);
            if (effectiveExpiryTime < _minimumExpiryTime)
            {
                effectiveExpiryTime = _minimumExpiryTime;
            }

            var summary = await ExtendAllAsync(effectiveExpiryTime);
            if (summary.Acquired >= _quorum)
            {
                ExtendCount++;
                InstanceSummary = summary;
                _logger?.LogDebug("Extend lock success: {Resource}, extend count: {ExtendCount}", Resource, ExtendCount);
                return RedisLockResult.Success;
            }

            Status = GetFailedLockStatus(summary);
            InstanceSummary = summary;
            return RedisLockResult.Conflicted;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to extend lock: {Resource}", Resource);
            return RedisLockResult.Error;
        }
    }

    #endregion

    #region IDisposable
    public void Dispose()
    {
        Dispose(true);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsync(true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            lock (_locker)
            {
                if (_keepaliveTimer != null)
                {
                    _keepaliveTimer.Dispose();
                    _keepaliveTimer = null;
                }
            }
        }

        _unlockSource.Cancel();
        Unlock();

        _disposed = true;
        Status = RedisLockStatus.Unlocked;
        InstanceSummary = RedisLockSummary.Empty;
        _unlockSource.Dispose();
        _semaphore.Dispose();
    }


    protected virtual async ValueTask DisposeAsync(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            lock (_locker)
            {
                if (_keepaliveTimer != null)
                {
                    _keepaliveTimer.Dispose();
                    _keepaliveTimer = null;
                }
            }
        }

        _unlockSource.Cancel();
        await UnlockAsync();

        _disposed = true;
        Status = RedisLockStatus.Unlocked;
        InstanceSummary = RedisLockSummary.Empty;
        _unlockSource.Dispose();
        _semaphore.Dispose();
    }

    #endregion
}

/// <summary>
/// SemaphoreSlim 扩展方法
/// </summary>
internal static class SemaphoreSlimExtensions
{
    public static SemaphoreLock Lock(this SemaphoreSlim semaphore, int millisecondsTimeout = -1, CancellationToken cancellationToken = default)
    {
        var acquired = semaphore.Wait(millisecondsTimeout, cancellationToken);
        return new SemaphoreLock(semaphore, acquired);
    }

    public static async Task<SemaphoreLock> LockAsync(this SemaphoreSlim semaphore, int millisecondsTimeout = -1, CancellationToken cancellationToken = default)
    {
        var acquired = await semaphore.WaitAsync(millisecondsTimeout, cancellationToken);
        return new SemaphoreLock(semaphore, acquired);
    }
}

/// <summary>
/// 信号量锁包装器
/// </summary>
internal readonly struct SemaphoreLock : IDisposable
{
    private readonly SemaphoreSlim? _semaphore;
    private readonly bool _acquired;

    public SemaphoreLock(SemaphoreSlim semaphore, bool acquired)
    {
        _semaphore = semaphore;
        _acquired = acquired;
    }

    public bool IsAcquired => _acquired;

    public void Dispose()
    {
        if (_acquired && _semaphore != null)
        {
            _semaphore.Release();
        }
    }
}
