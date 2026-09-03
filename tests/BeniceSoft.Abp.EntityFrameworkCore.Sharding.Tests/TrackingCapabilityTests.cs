using BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests.Fixture;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Xunit;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests;

/// <summary>A1–A6：壳 DbContext 追踪 / Executor 语义。</summary>
public class TrackingCapabilityTests : ShardingTestBase
{
    private readonly IRepository<ShardBucket, Guid> _buckets;
    private readonly IUnitOfWorkManager _uow;

    public TrackingCapabilityTests(ShardingTestApplication app) : base(app)
    {
        _buckets = GetRequiredService<IRepository<ShardBucket, Guid>>();
        _uow = GetRequiredService<IUnitOfWorkManager>();
    }

    /// <summary>A1：Attach 后壳上可再次定位到同一 Entry（跨物理 StateManager 查找）。</summary>
    [Fact]
    public async Task TryGetEntry_After_Attach_Should_Find_Same_Instance()
    {
        var id = Guid.NewGuid();
        var batch = NewBatch("trk-a1");
        using (var seed = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            await _buckets.InsertAsync(new ShardBucket(id, "a1", 10, batch), autoSave: true);
            await seed.CompleteAsync();
        }

        using var scope = App.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShardingFixtureDbContext>();
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);

        var entity = await db.Set<ShardBucket>()
            .AsNoTracking()
            .SingleAsync(x => x.Id == id && x.BatchTag == batch);

        db.Attach(entity);
        var entry1 = db.Entry(entity);
        entry1.State.ShouldBe(EntityState.Unchanged);

        // 再次 Entry / 查找应命中已追踪实例，而不是 NotImplemented
        var entry2 = db.Entry(entity);
        entry2.Entity.ShouldBeSameAs(entity);
        db.ChangeTracker.Entries<ShardBucket>().Any(e => ReferenceEquals(e.Entity, entity)).ShouldBeTrue();

        await uow.CompleteAsync();
    }

    /// <summary>A2：AsTracking 查询后实体进入物理追踪，壳 ChangeTracker 可见。</summary>
    [Fact]
    public async Task StartTrackingFromQuery_Via_AsTracking_Should_Appear_In_ChangeTracker()
    {
        var id = Guid.NewGuid();
        var batch = NewBatch("trk-a2");
        using (var seed = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            await _buckets.InsertAsync(new ShardBucket(id, "a2", 11, batch), autoSave: true);
            await seed.CompleteAsync();
        }

        using var scope = App.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShardingFixtureDbContext>();
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);

        var entity = await db.Set<ShardBucket>()
            .AsTracking()
            .Where(x => x.Id == id && x.BucketKey == 11 && x.BatchTag == batch)
            .SingleAsync();

        db.ChangeTracker.Entries<ShardBucket>().Any(e => e.Entity.Id == id).ShouldBeTrue();
        db.Entry(entity).State.ShouldBe(EntityState.Unchanged);

        await uow.CompleteAsync();
    }

    /// <summary>A3：GetOrCreateEntry 按传入类型追踪（同 ClrType 时与 Attach 一致）。</summary>
    [Fact]
    public async Task GetOrCreateEntry_With_EntityType_Should_Track()
    {
        var id = Guid.NewGuid();
        var batch = NewBatch("trk-a3");
        var entity = new ShardBucket(id, "a3", 10, batch);

        using var scope = App.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShardingFixtureDbContext>();
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);

        var entityType = db.Model.FindEntityType(typeof(ShardBucket));
        entityType.ShouldNotBeNull();

        // Add 走 GetOrCreateEntry(entity, entityType) 路径
        db.Add(entity);
        db.Entry(entity).State.ShouldBe(EntityState.Added);
        db.ChangeTracker.Entries<ShardBucket>().Count(e => e.Entity.Id == id).ShouldBe(1);

        await db.SaveChangesAsync();
        await uow.CompleteAsync();
    }

    /// <summary>A4：TrackGraph 在壳上应把根实体挂到物理库，而不是静默空操作。</summary>
    [Fact]
    public async Task TrackGraph_On_Shell_Should_Track_Root()
    {
        var id = Guid.NewGuid();
        var batch = NewBatch("trk-a4");
        var entity = new ShardBucket(id, "a4", 11, batch);

        using var scope = App.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShardingFixtureDbContext>();
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);

        db.ChangeTracker.TrackGraph(entity, node =>
        {
            node.Entry.State = EntityState.Added;
        });

        db.ChangeTracker.Entries<ShardBucket>().Any(e => e.Entity.Id == id && e.State == EntityState.Added)
            .ShouldBeTrue();

        await db.SaveChangesAsync();
        await uow.CompleteAsync();
    }

    /// <summary>A5：Local / Entries / Clear 与物理追踪对齐。</summary>
    [Fact]
    public async Task Local_And_Entries_Should_Reflect_Physical_Tracked_Entities()
    {
        var id = Guid.NewGuid();
        var batch = NewBatch("trk-a5");
        using (var seed = _uow.Begin(requiresNew: true, isTransactional: true))
        {
            await _buckets.InsertAsync(new ShardBucket(id, "a5", 10, batch), autoSave: true);
            await seed.CompleteAsync();
        }

        using var scope = App.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShardingFixtureDbContext>();
        using var uow = _uow.Begin(requiresNew: true, isTransactional: true);

        var entity = await db.Set<ShardBucket>()
            .AsNoTracking()
            .SingleAsync(x => x.Id == id && x.BatchTag == batch);
        db.Attach(entity);

        db.ChangeTracker.HasChanges().ShouldBeFalse();
        db.ChangeTracker.Entries<ShardBucket>().Any(e => e.Entity.Id == id).ShouldBeTrue();
        db.Set<ShardBucket>().Local.Any(e => e.Id == id).ShouldBeTrue();

        entity.Name = "a5-changed";
        db.ChangeTracker.DetectChanges();
        db.ChangeTracker.HasChanges().ShouldBeTrue();

        db.ChangeTracker.Clear();
        db.ChangeTracker.Entries<ShardBucket>().Any(e => e.Entity.Id == id).ShouldBeFalse();
        db.Set<ShardBucket>().Local.Any(e => e.Id == id).ShouldBeFalse();

        await uow.CompleteAsync();
    }

    /// <summary>A6：壳有 Executor 且 IsExecutor=false；物理库无 Executor 且 IsExecutor=true，GetExecutor 抛错。</summary>
    [Fact]
    public void Executor_Semantics_Shell_Vs_Physical()
    {
        using var scope = App.CreateScope();
        var shell = scope.ServiceProvider.GetRequiredService<ShardingFixtureDbContext>();

        shell.TryGetExecutor().ShouldNotBeNull();
        shell.GetExecutor().ShouldNotBeNull();
        shell.IsExecutor.ShouldBeFalse();

        var entity = new ShardBucket(Guid.NewGuid(), "phys", 10, NewBatch("trk-a6"));
        var physical = shell.GetExecutor().Create(entity);
        physical.ShouldBeOfType<ShardingFixtureDbContext>();
        var physicalSharding = (IShardingDbContext)physical;

        physicalSharding.TryGetExecutor().ShouldBeNull();
        physicalSharding.IsExecutor.ShouldBeTrue();
        Should.Throw<ShardingInvalidOperationException>(() => physicalSharding.GetExecutor());
    }
}
