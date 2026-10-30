using BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;
using BeniceSoft.Abp.Extensions.DistributedLock.Tests.Mocks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.Extensions.DistributedLock.Tests;

public class DistributedLockInterceptorTests : AbpIntegratedTest<DistributedLockTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options) => options.UseAutofac();

    protected override void BeforeAddApplication(IServiceCollection services)
    {
        services.ReplaceConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DistributedLock:ConnectionString"] = "localhost:6379"
        }).Build());
    }

    [Fact]
    public async Task AutomaticUnitOfWork_HoldsLockUntilCommitFinishes()
    {
        var service = GetRequiredService<LockTestService>();
        var manager = GetRequiredService<IUnitOfWorkManager>();
        var store = GetRequiredService<RedisLockTestStore>();
        var committed = false;
        var transaction = new Mock<ITransactionApi>();
        transaction.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Returns(async () =>
        {
            Assert.NotNull(store.Owner("order"));
            var provider = GetRequiredService<IDistributedLockProvider>();
            Assert.Null(await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken));
            committed = true;
        });

        await service.RunAsync("order", () =>
        {
            Assert.NotNull(manager.Current);
            manager.Current.AddTransactionApi("transaction", transaction.Object);
            Assert.NotNull(store.Owner("order"));
            return Task.CompletedTask;
        });

        Assert.True(committed);
        Assert.Null(store.Owner("order"));
        transaction.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task AmbientUnitOfWork_MethodReturnDoesNotReleaseLock()
    {
        var manager = GetRequiredService<IUnitOfWorkManager>();
        var store = GetRequiredService<RedisLockTestStore>();
        using var uow = manager.Begin(requiresNew: true, isTransactional: true);
        await GetRequiredService<LockTestService>().RunAsync("order", () => Task.CompletedTask);

        Assert.NotNull(store.Owner("order"));
        await uow.CompleteAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(store.Owner("order"));
    }

    [Fact]
    public async Task BusinessException_ReleasesOnlyAfterTransactionDisposal()
    {
        var manager = GetRequiredService<IUnitOfWorkManager>();
        var store = GetRequiredService<RedisLockTestStore>();
        var transaction = new Mock<ITransactionApi>();
        transaction.Setup(x => x.Dispose()).Callback(() => Assert.NotNull(store.Owner("order")));
        using (var uow = manager.Begin(requiresNew: true, isTransactional: true))
        {
            uow.AddTransactionApi("transaction", transaction.Object);
            await Assert.ThrowsAsync<InvalidOperationException>(() => GetRequiredService<LockTestService>()
                .RunAsync("order", () => throw new InvalidOperationException("业务失败")));
            Assert.NotNull(store.Owner("order"));
        }

        Assert.Null(store.Owner("order"));
        transaction.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task ExplicitRollback_HoldsLockUntilTransactionIsDisposed()
    {
        var manager = GetRequiredService<IUnitOfWorkManager>();
        var store = GetRequiredService<RedisLockTestStore>();
        var transaction = new Mock<ITransactionApi>();
        var rollback = transaction.As<ISupportsRollback>();
        rollback.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(() =>
        {
            Assert.NotNull(store.Owner("order"));
            return Task.CompletedTask;
        });
        transaction.Setup(x => x.Dispose()).Callback(() => Assert.NotNull(store.Owner("order")));
        using (var uow = manager.Begin(requiresNew: true, isTransactional: true))
        {
            uow.AddTransactionApi("transaction", transaction.Object);
            await GetRequiredService<LockTestService>().RunAsync("order", () => Task.CompletedTask);
            await uow.RollbackAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(store.Owner("order"));
        }

        rollback.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(store.Owner("order"));
    }

    [Fact]
    public async Task CommitException_DisposalStillReleasesLock()
    {
        var manager = GetRequiredService<IUnitOfWorkManager>();
        var store = GetRequiredService<RedisLockTestStore>();
        var transaction = new Mock<ITransactionApi>();
        transaction.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("提交失败"));
        transaction.Setup(x => x.Dispose()).Callback(() => Assert.NotNull(store.Owner("order")));
        using (var uow = manager.Begin(requiresNew: true, isTransactional: true))
        {
            uow.AddTransactionApi("transaction", transaction.Object);
            await GetRequiredService<LockTestService>().RunAsync("order", () => Task.CompletedTask);
            await Assert.ThrowsAsync<InvalidOperationException>(() => uow.CompleteAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.NotNull(store.Owner("order"));
        }

        Assert.Null(store.Owner("order"));
    }

    [Fact]
    public async Task EarlierCompletionCallbackThrows_DisposalReleasesLock()
    {
        var manager = GetRequiredService<IUnitOfWorkManager>();
        var store = GetRequiredService<RedisLockTestStore>();
        using (var uow = manager.Begin(requiresNew: true, isTransactional: true))
        {
            uow.OnCompleted(() => throw new InvalidOperationException("完成回调失败"));
            await GetRequiredService<LockTestService>().RunAsync("order", () => Task.CompletedTask);
            await Assert.ThrowsAsync<InvalidOperationException>(() => uow.CompleteAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.NotNull(store.Owner("order"));
        }

        Assert.Null(store.Owner("order"));
    }

    [Fact]
    public async Task SameUnitOfWork_NestedAndSequentialCallsReuseLock()
    {
        var manager = GetRequiredService<IUnitOfWorkManager>();
        var store = GetRequiredService<RedisLockTestStore>();
        var service = GetRequiredService<LockTestService>();
        using var uow = manager.Begin(requiresNew: true, isTransactional: true);

        await service.RunAsync("order", () => service.RunAsync("order", () => Task.CompletedTask));
        await service.RunAsync("order", () => Task.CompletedTask);

        Assert.Equal(1, store.Attempts);
        Assert.NotNull(store.Owner("order"));
        await uow.CompleteAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(store.Owner("order"));
    }

    [Fact]
    public async Task RequiresNew_ReleasesInnerLockIndependently()
    {
        var manager = GetRequiredService<IUnitOfWorkManager>();
        var store = GetRequiredService<RedisLockTestStore>();
        var service = GetRequiredService<LockTestService>();
        using var outer = manager.Begin(requiresNew: true, isTransactional: true);
        await service.RunAsync("outer", () => Task.CompletedTask);
        using (var inner = manager.Begin(requiresNew: true, isTransactional: true))
        {
            await service.RunAsync("inner", () => Task.CompletedTask);
            await inner.CompleteAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Null(store.Owner("inner"));
            Assert.NotNull(store.Owner("outer"));
        }

        await outer.CompleteAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Null(store.Owner("outer"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WithoutUnitOfWork_ReleasesOnMethodExit(bool throws)
    {
        var store = GetRequiredService<RedisLockTestStore>();
        var service = GetRequiredService<PlainLockTestService>();
        var call = service.RunAsync(() =>
        {
            Assert.Null(GetRequiredService<IUnitOfWorkManager>().Current);
            Assert.NotNull(store.Owner("plain"));
            if (throws)
            {
                throw new InvalidOperationException("业务失败");
            }

            return Task.CompletedTask;
        });

        if (throws)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => call);
        }
        else
        {
            await call;
        }

        Assert.Null(store.Owner("plain"));
    }

    [Fact]
    public async Task AcquisitionFailure_DoesNotExecuteMethod()
    {
        var provider = GetRequiredService<IDistributedLockProvider>();
        await using var owner = await provider.TryAcquireAsync("order", TimeSpan.FromSeconds(30), TimeSpan.Zero, TimeSpan.Zero, cancellationToken: TestContext.Current.CancellationToken);
        var executed = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => GetRequiredService<LockTestService>().RunAsync("order", () =>
        {
            executed = true;
            return Task.CompletedTask;
        }));

        Assert.False(executed);
        Assert.Equal(owner!.LockId, GetRequiredService<RedisLockTestStore>().Owner("order"));
    }

    [Fact]
    public async Task WithoutAttribute_DoesNotAcquireLock()
    {
        await GetRequiredService<PlainLockTestService>().WithoutLockAsync();
        Assert.Equal(0, GetRequiredService<RedisLockTestStore>().Attempts);
    }
}

[DependsOn(typeof(AbpAutofacModule), typeof(BeniceSoftAbpDistributedLockModule))]
public class DistributedLockTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<RedisLockTestStore>();
        context.Services.Replace(ServiceDescriptor.Singleton<IDistributedLockProvider>(sp =>
            new BeniceSoftDistributedLockProvider([sp.GetRequiredService<RedisLockTestStore>().Client],
                NullLogger<BeniceSoftDistributedLockProvider>.Instance)));
    }
}

public class LockTestService : ITransientDependency, IUnitOfWorkEnabled
{
    [DistributedLock(ResourceId = "{resourceId}", AutoRenew = true)]
    public virtual Task RunAsync(string resourceId, Func<Task> action) => action();
}

public class PlainLockTestService : ITransientDependency
{
    [DistributedLock(ResourceId = "plain")]
    public virtual Task RunAsync(Func<Task> action) => action();

    public virtual Task WithoutLockAsync() => Task.CompletedTask;
}
