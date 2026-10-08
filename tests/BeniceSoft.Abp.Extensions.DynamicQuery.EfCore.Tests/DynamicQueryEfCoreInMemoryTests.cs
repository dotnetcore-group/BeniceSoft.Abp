using BeniceSoft.Abp.Extensions.DynamicQuery.EfCore.Extensions;
using BeniceSoft.Abp.Extensions.DynamicQuery.EfCore.Tests.TestModels;
using BeniceSoft.Core;
using BeniceSoft.Core.Constants;
using BeniceSoft.Extensions.DynamicQuery;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace BeniceSoft.Abp.Extensions.DynamicQuery.EfCore.Tests;

/// <summary>
/// 经 EF InMemory 执行，验证动态查询/排序表达式可被 EF 翻译（非纯 LINQ to Objects）。
/// </summary>
public class DynamicQueryEfCoreInMemoryTests : IAsyncLifetime
{
    private TestDynamicQueryDbContext _db = null!;

    public async ValueTask InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TestDynamicQueryDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDynamicQueryDbContext(options);

        _db.Entities.AddRange(
            new TestEntity
            {
                Id = 1, Name = "Alice", Age = 25, TotalCount = 1, Price = 1, IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1),
                OccurredAt = new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero),
                BizDate = new DateOnly(2024, 1, 1),
                CutOffTime = new TimeOnly(9, 0),
                UniqueId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Nested = new TestNestedEntity { Code = "A", Value = 1 }
            },
            new TestEntity
            {
                Id = 2, Name = "Bob", Age = 30, TotalCount = 2, Price = 2, IsActive = false,
                CreatedAt = new DateTime(2024, 2, 1),
                OccurredAt = new DateTimeOffset(2024, 2, 1, 10, 0, 0, TimeSpan.Zero),
                BizDate = new DateOnly(2024, 2, 1),
                CutOffTime = new TimeOnly(12, 0),
                UniqueId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Nested = new TestNestedEntity { Code = "B", Value = 2 }
            },
            new TestEntity
            {
                Id = 3, Name = "Charlie", Age = 35, TotalCount = 3, Price = 3, IsActive = true,
                CreatedAt = new DateTime(2024, 3, 1),
                OccurredAt = new DateTimeOffset(2024, 3, 1, 10, 0, 0, TimeSpan.Zero),
                BizDate = new DateOnly(2024, 3, 1),
                CutOffTime = new TimeOnly(15, 30),
                UniqueId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Nested = new TestNestedEntity { Code = "C", Value = 3 }
            },
            new TestEntity
            {
                Id = 4, Name = "David", Age = 28, TotalCount = 4, Price = 4, IsActive = true,
                CreatedAt = new DateTime(2024, 4, 1),
                OccurredAt = new DateTimeOffset(2024, 4, 1, 10, 0, 0, TimeSpan.Zero),
                BizDate = new DateOnly(2024, 4, 1),
                CutOffTime = new TimeOnly(18, 0),
                UniqueId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Nested = new TestNestedEntity { Code = "D", Value = 4 }
            });

        await _db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Ef_Between_DateTime_HalfOpen_ShouldTranslate()
    {
        var request = CreateRequest("CreatedAt", BeniceSoftTypeNameConstant.DateTime, ExprOperator.Between, "2024-02-01", "2024-04-01");

        var ct = TestContext.Current.CancellationToken;
        var result = await _db.Entities.AsNoTracking().DynamicQueryBy(request).Select(x => x.Name).ToListAsync(ct);

        result.ShouldBe(["Bob", "Charlie"], ignoreOrder: true);
        result.ShouldNotContain("David");
    }

    [Fact]
    public async Task Ef_Between_Integer_Closed_ShouldTranslate()
    {
        var request = CreateRequest("Age", BeniceSoftTypeNameConstant.Integer, ExprOperator.Between, "25", "30");

        var result = await _db.Entities.AsNoTracking().DynamicQueryBy(request).Select(x => x.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(["Alice", "Bob", "David"], ignoreOrder: true);
    }

    [Fact]
    public async Task Ef_Equal_DateOnly_ShouldTranslate()
    {
        var request = CreateRequest("BizDate", BeniceSoftTypeNameConstant.Date, ExprOperator.Equal, "2024-03-01");

        var result = await _db.Entities.AsNoTracking().DynamicQueryBy(request).Select(x => x.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(["Charlie"]);
    }

    [Fact]
    public async Task Ef_Equal_TimeOnly_ShouldTranslate()
    {
        var request = CreateRequest("CutOffTime", BeniceSoftTypeNameConstant.Time, ExprOperator.Equal, "15:30");

        var result = await _db.Entities.AsNoTracking().DynamicQueryBy(request).Select(x => x.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(["Charlie"]);
    }

    [Fact]
    public async Task Ef_Equal_DateTimeOffset_ShouldTranslate()
    {
        var request = CreateRequest("OccurredAt", BeniceSoftTypeNameConstant.DateTime, ExprOperator.Equal, "2024-02-01T10:00:00+00:00");

        var result = await _db.Entities.AsNoTracking().DynamicQueryBy(request).Select(x => x.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(["Bob"]);
    }

    [Fact]
    public async Task Ef_OrderByDynamic_SingleField_ShouldTranslate()
    {
        var result = await _db.Entities.AsNoTracking()
            .OrderByDynamic([new DynamicOrderBy { FieldName = "Age", Direction = "desc" }])
            .Select(x => x.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(["Charlie", "Bob", "David", "Alice"]);
    }

    [Fact]
    public async Task Ef_OrderByDynamic_MultiField_ShouldTranslate()
    {
        var result = await _db.Entities.AsNoTracking()
            .OrderByDynamic(
            [
                new DynamicOrderBy { FieldName = "IsActive", Direction = "desc" },
                new DynamicOrderBy { FieldName = "Age", Direction = "asc" }
            ])
            .Select(x => x.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(["Alice", "David", "Charlie", "Bob"]);
    }

    [Fact]
    public async Task Ef_OrderByDynamic_NestedOwned_ShouldTranslate()
    {
        var result = await _db.Entities.AsNoTracking()
            .OrderByDynamic([new DynamicOrderBy { FieldName = "Nested.Value", Direction = "desc" }])
            .Select(x => x.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(["David", "Charlie", "Bob", "Alice"]);
    }

    [Fact]
    public async Task Ef_OrderByDynamic_ViaRequest_ShouldTranslate()
    {
        IDynamicOrderByRequest request = new TestDynamicQueryOrderRequest
        {
            OrderBys = [new DynamicOrderBy { FieldName = "Name", Direction = "asc" }]
        };

        var result = await _db.Entities.AsNoTracking()
            .OrderByDynamic(request)
            .Select(x => x.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(["Alice", "Bob", "Charlie", "David"]);
    }

    [Fact]
    public async Task Ef_FieldTypeDate_OnDateTimeOffset_ShouldTranslate()
    {
        var request = CreateRequest("OccurredAt", BeniceSoftTypeNameConstant.Date, ExprOperator.Equal, "2024-02-01T10:00:00+00:00");

        var result = await _db.Entities.AsNoTracking().DynamicQueryBy(request).Select(x => x.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(["Bob"]);
    }

    private static TestDynamicQueryRequest CreateRequest(string fieldName, string fieldType, ExprOperator @operator, params string[] values)
    {
        return new TestDynamicQueryRequest
        {
            ConditionGroups =
            [
                new DynamicQueryConditionGroup
                {
                    Relation = BeniceSoftRelationConstant.And,
                    Conditions =
                    [
                        new DynamicQueryCondition
                        {
                            FieldName = fieldName,
                            FieldType = fieldType,
                            Operator = @operator,
                            Value = values.ToList()
                        }
                    ]
                }
            ]
        };
    }

    private sealed class TestDynamicQueryOrderRequest : IDynamicOrderByRequest
    {
        public List<DynamicOrderBy>? OrderBys { get; set; }
    }
}
