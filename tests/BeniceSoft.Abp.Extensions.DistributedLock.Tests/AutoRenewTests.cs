using BeniceSoft.Abp.Extensions.DistributedLock.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BeniceSoft.Abp.Extensions.DistributedLock.Tests;

public class AutoRenewTests
{
    [Fact]
    public async Task AutoRenew_ContinuesUntilHandleIsDisposed()
    {
        var store = new RedisLockTestStore();
        using var provider = new BeniceSoftDistributedLockProvider([store.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        var handle = await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(1), TimeSpan.Zero, TimeSpan.Zero, autoRenew: true, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(handle);
        await store.Renewed.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(handle.LockId, store.Owner("order"));

        await handle.DisposeAsync();
        var renewals = store.Renewals;
        await Task.Delay(700, TestContext.Current.CancellationToken);

        Assert.Equal(renewals, store.Renewals);
        Assert.Null(store.Owner("order"));
    }

    [Fact]
    public async Task AutoRenewDisabled_DoesNotStartTimer()
    {
        var store = new RedisLockTestStore();
        using var provider = new BeniceSoftDistributedLockProvider([store.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        await using var handle = await provider.TryAcquireAsync("order", TimeSpan.FromMilliseconds(200), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Equal(0, store.Renewals);
    }

    [Fact]
    public async Task ManualRenew_UsesOriginalOwnership()
    {
        var store = new RedisLockTestStore();
        using var provider = new BeniceSoftDistributedLockProvider([store.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        await using var first = await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        Assert.True(await first.RenewAsync(TimeSpan.FromSeconds(60)));
        store.Expire("order");
        await using var second = await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(second);

        Assert.False(await first.RenewAsync(TimeSpan.FromSeconds(60)));

        Assert.Equal(second.LockId, store.Owner("order"));
    }
}
