using BeniceSoft.Abp.Extensions.DistributedLock.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BeniceSoft.Abp.Extensions.DistributedLock.Tests;

public class ConcurrencyIssueTests
{
    [Fact]
    public async Task ExpiredOwner_DisposalDoesNotReleaseSuccessor()
    {
        var store = new RedisLockTestStore();
        using var provider = new BeniceSoftDistributedLockProvider([store.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        var first = await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        store.Expire("order");
        await using var second = await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(second);

        await first.DisposeAsync();
        first.Dispose();

        Assert.Equal(second.LockId, store.Owner("order"));
        Assert.Null(await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentDispose_IsIdempotent()
    {
        var store = new RedisLockTestStore();
        using var provider = new BeniceSoftDistributedLockProvider([store.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        var handle = await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(handle);

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            i % 2 == 0 ? Task.Run(handle.Dispose, cancellationToken: TestContext.Current.CancellationToken) : handle.DisposeAsync().AsTask()));

        Assert.Null(store.Owner("order"));
        Assert.False(handle.IsAcquired);
        Assert.False(await handle.RenewAsync(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task ProviderDispose_ReleasesAllOwnedHandles()
    {
        var store = new RedisLockTestStore();
        var provider = new BeniceSoftDistributedLockProvider([store.Client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
        await using var first = await provider.TryAcquireAsync("first", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);
        await using var second = await provider.TryAcquireAsync("second", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);

        provider.Dispose();
        provider.Dispose();

        Assert.Null(store.Owner("first"));
        Assert.Null(store.Owner("second"));
        Assert.False(first!.IsAcquired);
        Assert.False(second!.IsAcquired);
    }
}
