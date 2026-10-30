using BeniceSoft.Abp.Extensions.DistributedLock.Tests.Mocks;
using BeniceSoft.Abp.Extensions.Redis;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BeniceSoft.Abp.Extensions.DistributedLock.Tests;

public class BeniceSoftDistributedLockProviderTests
{
    [Fact]
    public async Task ZeroWait_ContendedResource_AttemptsOnce()
    {
        var store = new RedisLockTestStore();
        using var provider = new BeniceSoftDistributedLockProvider([store.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        await using var owner = await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(owner);
        var attempts = store.Attempts;

        var contender = await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(contender);
        Assert.Equal(attempts + 1, store.Attempts);
        Assert.Equal(owner.LockId, store.Owner("order"));
    }

    [Fact]
    public async Task Waiting_AcquiresAfterOwnerReleases()
    {
        var store = new RedisLockTestStore();
        using var provider = new BeniceSoftDistributedLockProvider([store.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        var owner = await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(owner);
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.AfterSet = () => attempted.TrySetResult();
        var pending = provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(10), cancellationToken: TestContext.Current.CancellationToken);
        await attempted.Task.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken: TestContext.Current.CancellationToken);
        await owner.DisposeAsync();

        await using var successor = await pending;

        Assert.NotNull(successor);
        Assert.NotEqual(owner.LockId, successor.LockId);
        Assert.Equal(successor.LockId, store.Owner("order"));
    }

    [Fact]
    public async Task WaitExhausted_ReturnsNull()
    {
        var store = new RedisLockTestStore();
        using var provider = new BeniceSoftDistributedLockProvider([store.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        await using var owner = await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        var result = await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(80), TimeSpan.FromMilliseconds(10), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.True(store.Attempts >= 2);
        Assert.Equal(owner!.LockId, store.Owner("order"));
    }

    [Fact]
    public async Task CancellationBeforeAcquisition_DoesNotCallRedis()
    {
        var store = new RedisLockTestStore();
        using var provider = new BeniceSoftDistributedLockProvider([store.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.TryAcquireAsync(
            "order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: cancellation.Token));

        Assert.Equal(0, store.Attempts);
    }

    [Fact]
    public async Task CancellationDuringRedisCall_ReleasesAcquiredLock()
    {
        var store = new RedisLockTestStore();
        using var provider = new BeniceSoftDistributedLockProvider([store.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        using var cancellation = new CancellationTokenSource();
        store.AfterSet = cancellation.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.TryAcquireAsync(
            "order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: cancellation.Token));

        Assert.Null(store.Owner("order"));
    }

    [Fact]
    public async Task CancellationWhileWaiting_StopsRetrying()
    {
        var store = new RedisLockTestStore();
        using var provider = new BeniceSoftDistributedLockProvider([store.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        await using var owner = await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.AfterSet = () => attempted.TrySetResult();
        var pending = provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(10), cancellationToken: cancellation.Token);
        await attempted.Task.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken: TestContext.Current.CancellationToken);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.Equal(owner!.LockId, store.Owner("order"));
        Assert.Equal(2, store.Attempts);
    }

    [Fact]
    public async Task DisposeDuringAcquisition_DoesNotLeaveNewLock()
    {
        var store = new RedisLockTestStore();
        var provider = new BeniceSoftDistributedLockProvider([store.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        store.AfterSet = provider.Dispose;

        await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.TryAcquireAsync(
            "order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Null(store.Owner("order"));
    }

    [Fact]
    public async Task FailedQuorum_CleansPartiallyAcquiredLocks()
    {
        var available = new RedisLockTestStore();
        var occupied1 = new RedisLockTestStore();
        var occupied2 = new RedisLockTestStore();
        using var p1 = new BeniceSoftDistributedLockProvider([occupied1.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        using var p2 = new BeniceSoftDistributedLockProvider([occupied2.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        await using var h1 = await p1.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);
        await using var h2 = await p2.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);
        using var provider = new BeniceSoftDistributedLockProvider([available.Client, occupied1.Client, occupied2.Client],
            NullLogger<BeniceSoftDistributedLockProvider>.Instance);

        var result = await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Null(available.Owner("order"));
        Assert.Equal(h1!.LockId, occupied1.Owner("order"));
        Assert.Equal(h2!.LockId, occupied2.Owner("order"));
        Assert.Equal(1, available.Attempts);
    }

    [Fact]
    public async Task InvalidWaitParameters_DoNotCallRedis()
    {
        var store = new RedisLockTestStore();
        using var provider = new BeniceSoftDistributedLockProvider([store.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provider.TryAcquireAsync(
            "order", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(1), TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => provider.TryAcquireAsync(
            "order", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(-1), TimeSpan.FromMilliseconds(10), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(0, store.Attempts);
    }

    [Fact]
    public void SynchronousRedisLock_ZeroWait_AttemptsOnce()
    {
        var store = new RedisLockTestStore();
        using var owner = RedisLock.Create([store.Client], new RedisLockProfile { Resource = "order", KeepLive = false }, cancellationToken: TestContext.Current.CancellationToken);
        using var contender = RedisLock.Create([store.Client], new RedisLockProfile { Resource = "order", KeepLive = false }, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(owner.IsAcquired);
        Assert.False(contender.IsAcquired);
        Assert.Equal(2, store.Attempts);
    }
}
