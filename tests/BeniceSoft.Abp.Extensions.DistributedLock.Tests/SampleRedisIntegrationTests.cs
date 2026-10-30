using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BeniceSoft.Abp.Extensions.DistributedLock.Abstractions;
using BeniceSoft.Abp.Extensions.Redis;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.Extensions.DistributedLock.Tests;

/// <summary>
/// 使用 Sample 的真实 Redis 配置，仅操作本轮生成的独立测试键
/// </summary>
[Trait("Category", "SampleRedis")]
public class SampleRedisIntegrationTests : AbpIntegratedTest<SampleRedisTestModule>, IAsyncLifetime
{
    private readonly List<string> _resources = [];
    private IDatabase _database = null!;

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options) => options.UseAutofac();

    protected override void BeforeAddApplication(IServiceCollection services)
    {
        var path = Environment.GetEnvironmentVariable("BENICESOFT_LOCK_SAMPLE_CONFIG")
            ?? throw new InvalidOperationException("未指定 Sample Redis 配置文件");
        using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        var connection = document.RootElement.GetProperty("DistributedLock").GetProperty("ConnectionString").GetString();
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException("Sample 未配置 DistributedLock:ConnectionString");
        }

        services.ReplaceConfiguration(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DistributedLock:ConnectionString"] = connection
        }).Build());
    }

    public async ValueTask InitializeAsync()
    {
        _database = ServiceProvider.GetRequiredKeyedService<RedisClient>("BeniceSoft_DistributedLock").Database;
        await _database.PingAsync();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            GetRequiredService<IDistributedLockProvider>().Dispose();
            foreach (var resource in _resources)
            {
                var key = "lock:" + resource;
                await _database.KeyDeleteAsync(key);
                Assert.False(await _database.KeyExistsAsync(key));
            }
        }
        finally
        {
            base.Dispose();
        }
    }

    [SampleRedisFact]
    public async Task IndependentConnections_CompeteAndReleaseWithLua()
    {
        using var peer = CreatePeer();
        var provider = GetRequiredService<IDistributedLockProvider>();
        var resource = NewResource();
        var first = await provider.TryAcquireAsync(resource, TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.Zero,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        Assert.Null(await peer.Provider.TryAcquireAsync(resource, TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.Zero,
            cancellationToken: TestContext.Current.CancellationToken));

        await first.DisposeAsync();

        Assert.False(await _database.KeyExistsAsync("lock:" + resource));
        await using var second = await peer.Provider.TryAcquireAsync(resource, TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.Zero,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(second);
        Assert.NotEqual(first.LockId, second.LockId);
    }

    [SampleRedisFact]
    public async Task ActualExpiry_OldHandleCannotDeleteSuccessor()
    {
        using var peer = CreatePeer();
        var provider = GetRequiredService<IDistributedLockProvider>();
        var resource = NewResource();
        var first = await provider.TryAcquireAsync(resource, TimeSpan.FromMilliseconds(600), TimeSpan.Zero, TimeSpan.Zero,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        Assert.False(await _database.KeyExistsAsync("lock:" + resource));
        await using var second = await provider.TryAcquireAsync(resource, TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.Zero,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(second);
        await first.DisposeAsync();

        Assert.Equal(second.LockId, await ReadOwnerAsync(resource));
        Assert.Null(await peer.Provider.TryAcquireAsync(resource, TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.Zero,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [SampleRedisFact]
    public async Task ActualExpiry_OldHandleCannotRenewSuccessor()
    {
        var provider = GetRequiredService<IDistributedLockProvider>();
        var resource = NewResource();
        await using var first = await provider.TryAcquireAsync(resource, TimeSpan.FromMilliseconds(600), TimeSpan.Zero, TimeSpan.Zero,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        await using var second = await provider.TryAcquireAsync(resource, TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.Zero,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(second);

        Assert.False(await first.RenewAsync(TimeSpan.FromSeconds(10)));

        Assert.Equal(second.LockId, await ReadOwnerAsync(resource));
    }

    [SampleRedisFact]
    public async Task AutoRenew_KeepsRealTtlAliveAndStopsOnDispose()
    {
        using var peer = CreatePeer();
        var provider = GetRequiredService<IDistributedLockProvider>();
        var resource = NewResource();
        var handle = await provider.TryAcquireAsync(resource, TimeSpan.FromSeconds(1), TimeSpan.Zero, TimeSpan.Zero, autoRenew: true,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(handle);

        await Task.Delay(2400, TestContext.Current.CancellationToken);

        Assert.Equal(handle.LockId, await ReadOwnerAsync(resource));
        Assert.True(await _database.KeyTimeToLiveAsync("lock:" + resource) > TimeSpan.Zero);
        Assert.Null(await peer.Provider.TryAcquireAsync(resource, TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.Zero,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(await handle.RenewAsync(TimeSpan.FromSeconds(3)));
        await handle.DisposeAsync();
        await Task.Delay(700, TestContext.Current.CancellationToken);
        Assert.False(await _database.KeyExistsAsync("lock:" + resource));
    }

    [SampleRedisFact]
    public async Task WaitingCancellation_DoesNotReleaseRealOwner()
    {
        using var peer = CreatePeer();
        var resource = NewResource();
        await using var owner = await GetRequiredService<IDistributedLockProvider>().TryAcquireAsync(
            resource, TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.Zero,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(owner);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var waiting = peer.Provider.TryAcquireAsync(resource, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(50), cancellationToken: cancellation.Token);
        await Task.Delay(200, TestContext.Current.CancellationToken);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

        Assert.Equal(owner.LockId, await ReadOwnerAsync(resource));
    }

    [SampleRedisFact]
    public async Task RealTransaction_CommitFinishesBeforeRedisLockRelease()
    {
        using var peer = CreatePeer();
        var resource = NewResource();
        var manager = GetRequiredService<IUnitOfWorkManager>();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE LockProbe (Id INTEGER PRIMARY KEY)";
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        using var uow = manager.Begin(requiresNew: true, isTransactional: true);
        var databaseTransaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var transaction = new RedisObservedTransaction(databaseTransaction, () => _database.KeyExists("lock:" + resource));
        uow.AddTransactionApi("sqlite", transaction);
        await GetRequiredService<LockTestService>().RunAsync(resource, async () =>
        {
            command.Transaction = (SqliteTransaction)databaseTransaction;
            command.CommandText = "INSERT INTO LockProbe VALUES (1)";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        });

        // 方法已经返回，数据库尚未提交，独立 Redis 连接仍不能取得同一把锁
        Assert.Null(await peer.Provider.TryAcquireAsync(resource, TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.Zero,
            cancellationToken: TestContext.Current.CancellationToken));
        await uow.CompleteAsync(TestContext.Current.CancellationToken);

        Assert.True(transaction.LockHeldBeforeCommit);
        Assert.True(transaction.LockHeldAfterCommit);
        command.Transaction = null;
        command.CommandText = "SELECT COUNT(*) FROM LockProbe";
        Assert.Equal(1L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        Assert.False(await _database.KeyExistsAsync("lock:" + resource));
        await using var successor = await peer.Provider.TryAcquireAsync(resource, TimeSpan.FromSeconds(10), TimeSpan.Zero, TimeSpan.Zero,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(successor);
    }

    [SampleRedisFact]
    public async Task RealTransaction_RollbackDisposalPrecedesRedisLockRelease()
    {
        var resource = NewResource();
        var manager = GetRequiredService<IUnitOfWorkManager>();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE LockProbe (Id INTEGER PRIMARY KEY)";
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        RedisObservedTransaction transaction;
        using (var uow = manager.Begin(requiresNew: true, isTransactional: true))
        {
            var databaseTransaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            transaction = new RedisObservedTransaction(databaseTransaction, () => _database.KeyExists("lock:" + resource));
            uow.AddTransactionApi("sqlite", transaction);
            await GetRequiredService<LockTestService>().RunAsync(resource, async () =>
            {
                command.Transaction = (SqliteTransaction)databaseTransaction;
                command.CommandText = "INSERT INTO LockProbe VALUES (1)";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            });
            await uow.RollbackAsync(TestContext.Current.CancellationToken);
            Assert.True(await _database.KeyExistsAsync("lock:" + resource));
        }

        Assert.True(transaction.LockHeldAtDispose);
        command.Transaction = null;
        command.CommandText = "SELECT COUNT(*) FROM LockProbe";
        Assert.Equal(0L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        Assert.False(await _database.KeyExistsAsync("lock:" + resource));
    }

    private string NewResource()
    {
        var resource = "benicesoft:distributed-lock-tests:" + Guid.NewGuid().ToString("N");
        _resources.Add(resource);
        return resource;
    }

    private LiveRedisPeer CreatePeer()
    {
        return new LiveRedisPeer(GetRequiredService<IConfiguration>()["DistributedLock:ConnectionString"]!);
    }

    private async Task<string?> ReadOwnerAsync(string resource)
    {
        var value = await _database.StringGetAsync("lock:" + resource);
        return value.IsNull ? null : JsonSerializer.Deserialize<string>(value.ToString());
    }
}

public sealed class SampleRedisFactAttribute : FactAttribute
{
    public SampleRedisFactAttribute([CallerFilePath] string? sourceFilePath = null, [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BENICESOFT_LOCK_SAMPLE_CONFIG")))
        {
            Skip = "需通过 BENICESOFT_LOCK_SAMPLE_CONFIG 显式指定 Sample 配置文件";
        }
    }
}

[DependsOn(typeof(AbpAutofacModule), typeof(BeniceSoftAbpDistributedLockModule))]
public class SampleRedisTestModule : AbpModule
{
}

internal sealed class LiveRedisPeer : IDisposable
{
    private readonly RedisConnection _connection;
    public BeniceSoftDistributedLockProvider Provider { get; }

    public LiveRedisPeer(string connectionString)
    {
        _connection = new RedisConnection(connectionString);
        var client = new RedisClient(_connection, 0);
        client.Database.Ping();
        Provider = new BeniceSoftDistributedLockProvider([client], NullLogger<BeniceSoftDistributedLockProvider>.Instance);
    }

    public void Dispose()
    {
        Provider.Dispose();
        _connection.Dispose();
    }
}

/// <summary>
/// 用真实 SQLite 事务记录 Redis 锁在提交和资源释放时的状态
/// </summary>
internal sealed class RedisObservedTransaction(DbTransaction transaction, Func<bool> lockExists) : ITransactionApi, ISupportsRollback
{
    public bool LockHeldBeforeCommit { get; private set; }
    public bool LockHeldAfterCommit { get; private set; }
    public bool LockHeldAtDispose { get; private set; }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        LockHeldBeforeCommit = lockExists();
        await transaction.CommitAsync(cancellationToken);
        LockHeldAfterCommit = lockExists();
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);

    public void Dispose()
    {
        transaction.Dispose();
        LockHeldAtDispose = lockExists();
    }
}
