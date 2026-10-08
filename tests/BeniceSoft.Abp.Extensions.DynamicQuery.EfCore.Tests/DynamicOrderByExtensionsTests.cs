using BeniceSoft.Abp.Extensions.DynamicQuery.EfCore.Extensions;
using BeniceSoft.Abp.Extensions.DynamicQuery.EfCore.Tests.TestModels;
using BeniceSoft.Core;
using BeniceSoft.Extensions.DynamicQuery;
using Shouldly;
using Xunit;

namespace BeniceSoft.Abp.Extensions.DynamicQuery.EfCore.Tests;

public class DynamicOrderByExtensionsTests
{
    private readonly List<TestEntity> _data;

    public DynamicOrderByExtensionsTests()
    {
        _data =
        [
            new()
            {
                Id = 1, Name = "Alice", Age = 25, IsActive = true,
                CreatedAt = new DateTime(2024, 1, 1),
                OccurredAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                BizDate = new DateOnly(2024, 1, 1),
                CutOffTime = new TimeOnly(9, 0),
                UniqueId = Guid.NewGuid(),
                Nested = new TestNestedEntity { Code = "A", Value = 1 }
            },
            new()
            {
                Id = 2, Name = "Bob", Age = 30, IsActive = false,
                CreatedAt = new DateTime(2024, 2, 1),
                OccurredAt = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero),
                BizDate = new DateOnly(2024, 2, 1),
                CutOffTime = new TimeOnly(12, 0),
                UniqueId = Guid.NewGuid(),
                Nested = new TestNestedEntity { Code = "B", Value = 2 }
            },
            new()
            {
                Id = 3, Name = "Charlie", Age = 35, IsActive = true,
                CreatedAt = new DateTime(2024, 3, 1),
                OccurredAt = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
                BizDate = new DateOnly(2024, 3, 1),
                CutOffTime = new TimeOnly(15, 30),
                UniqueId = Guid.NewGuid(),
                Nested = new TestNestedEntity { Code = "C", Value = 3 }
            },
            new()
            {
                Id = 4, Name = "David", Age = 28, IsActive = true,
                CreatedAt = new DateTime(2024, 4, 1),
                OccurredAt = new DateTimeOffset(2024, 4, 1, 0, 0, 0, TimeSpan.Zero),
                BizDate = new DateOnly(2024, 4, 1),
                CutOffTime = new TimeOnly(18, 0),
                UniqueId = Guid.NewGuid(),
                Nested = new TestNestedEntity { Code = "D", Value = 4 }
            },
            new()
            {
                Id = 5, Name = "Eve", Age = 22, IsActive = false,
                CreatedAt = new DateTime(2024, 5, 1),
                OccurredAt = new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero),
                BizDate = new DateOnly(2024, 5, 1),
                CutOffTime = new TimeOnly(8, 0),
                UniqueId = Guid.NewGuid(),
                Nested = new TestNestedEntity { Code = "E", Value = 5 }
            }
        ];
    }

    [Fact]
    public void OrderByDynamic_SingleField_Asc_ShouldSort()
    {
        var result = Query()
            .OrderByDynamic([new DynamicOrderBy { FieldName = "Age", Direction = "asc" }])
            .Select(x => x.Name)
            .ToList();

        result.ShouldBe(["Eve", "Alice", "David", "Bob", "Charlie"]);
    }

    [Fact]
    public void OrderByDynamic_SingleField_Desc_ShouldSort()
    {
        var result = Query()
            .OrderByDynamic([new DynamicOrderBy { FieldName = "Age", Direction = "desc" }])
            .Select(x => x.Name)
            .ToList();

        result.ShouldBe(["Charlie", "Bob", "David", "Alice", "Eve"]);
    }

    [Fact]
    public void OrderByDynamic_Direction_ShouldBeCaseInsensitive()
    {
        var lower = Query()
            .OrderByDynamic([new DynamicOrderBy { FieldName = "Age", Direction = "DESC" }])
            .Select(x => x.Name)
            .ToList();
        var mixed = Query()
            .OrderByDynamic([new DynamicOrderBy { FieldName = "Age", Direction = "Desc" }])
            .Select(x => x.Name)
            .ToList();

        lower.ShouldBe(["Charlie", "Bob", "David", "Alice", "Eve"]);
        mixed.ShouldBe(lower);
    }

    [Fact]
    public void OrderByDynamic_Direction_DefaultOrNonDesc_ShouldBeAscending()
    {
        var byDefault = Query()
            .OrderByDynamic([new DynamicOrderBy { FieldName = "Age" }])
            .Select(x => x.Name)
            .ToList();
        var byEmpty = Query()
            .OrderByDynamic([new DynamicOrderBy { FieldName = "Age", Direction = "" }])
            .Select(x => x.Name)
            .ToList();
        var byInvalid = Query()
            .OrderByDynamic([new DynamicOrderBy { FieldName = "Age", Direction = "up" }])
            .Select(x => x.Name)
            .ToList();

        var expectedAsc = new[] { "Eve", "Alice", "David", "Bob", "Charlie" };
        byDefault.ShouldBe(expectedAsc);
        byEmpty.ShouldBe(expectedAsc);
        byInvalid.ShouldBe(expectedAsc);
    }

    [Fact]
    public void OrderByDynamic_FieldName_ShouldBeCaseInsensitive()
    {
        var result = Query()
            .OrderByDynamic([new DynamicOrderBy { FieldName = "age", Direction = "asc" }])
            .Select(x => x.Name)
            .ToList();

        result.ShouldBe(["Eve", "Alice", "David", "Bob", "Charlie"]);
    }

    [Fact]
    public void OrderByDynamic_MultiField_ShouldThenBy()
    {
        var result = Query()
            .OrderByDynamic(
            [
                new DynamicOrderBy { FieldName = "IsActive", Direction = "desc" },
                new DynamicOrderBy { FieldName = "Age", Direction = "asc" }
            ])
            .Select(x => x.Name)
            .ToList();

        // 若漏 ThenBy，稳定序会是 Alice, Charlie, David，不会是按 Age
        result.ShouldBe(["Alice", "David", "Charlie", "Eve", "Bob"]);
    }

    [Fact]
    public void OrderByDynamic_MultiField_ThenByDescending_ShouldWork()
    {
        var result = Query()
            .OrderByDynamic(
            [
                new DynamicOrderBy { FieldName = "IsActive", Direction = "asc" },
                new DynamicOrderBy { FieldName = "Age", Direction = "desc" }
            ])
            .Select(x => x.Name)
            .ToList();

        // false 先：Bob 30, Eve 22；true：Charlie 35, David 28, Alice 25
        result.ShouldBe(["Bob", "Eve", "Charlie", "David", "Alice"]);
    }

    [Fact]
    public void OrderByDynamic_NestedField_ShouldSort()
    {
        var byValue = Query()
            .OrderByDynamic([new DynamicOrderBy { FieldName = "Nested.Value", Direction = "desc" }])
            .Select(x => x.Name)
            .ToList();
        var byCode = Query()
            .OrderByDynamic([new DynamicOrderBy { FieldName = "Nested.Code", Direction = "asc" }])
            .Select(x => x.Name)
            .ToList();

        byValue.ShouldBe(["Eve", "David", "Charlie", "Bob", "Alice"]);
        byCode.ShouldBe(["Alice", "Bob", "Charlie", "David", "Eve"]);
    }

    [Fact]
    public void OrderByDynamic_NestedField_CaseInsensitive_ShouldSort()
    {
        var result = Query()
            .OrderByDynamic([new DynamicOrderBy { FieldName = "nested.value", Direction = "asc" }])
            .Select(x => x.Name)
            .ToList();

        result.ShouldBe(["Alice", "Bob", "Charlie", "David", "Eve"]);
    }

    [Fact]
    public void OrderByDynamic_EmptyFieldName_ShouldSkipAndUseNextAsPrimary()
    {
        var result = Query()
            .OrderByDynamic(
            [
                new DynamicOrderBy { FieldName = "", Direction = "desc" },
                new DynamicOrderBy { FieldName = "   ", Direction = "desc" },
                new DynamicOrderBy { FieldName = "Age", Direction = "asc" }
            ])
            .Select(x => x.Name)
            .ToList();

        // 空字段跳过，Age 作为首个 OrderBy（asc）
        result.ShouldBe(["Eve", "Alice", "David", "Bob", "Charlie"]);
        Query()
            .OrderByDynamic(
            [
                new DynamicOrderBy { FieldName = "", Direction = "desc" },
                new DynamicOrderBy { FieldName = "Age", Direction = "asc" }
            ])
            .Expression.ToString()
            .ShouldContain("OrderBy");
    }

    [Fact]
    public void OrderByDynamic_AllEmptyFieldNames_ShouldNotApplyOrderBy()
    {
        var ordered = Query().OrderByDynamic(
        [
            new DynamicOrderBy { FieldName = "" },
            new DynamicOrderBy { FieldName = "  " }
        ]);

        ordered.Expression.ToString().ShouldNotContain("OrderBy");
    }

    [Fact]
    public void OrderByDynamic_UnknownField_ShouldThrow()
    {
        var ex = Should.Throw<DynamicQueryException>(() =>
            Query()
                .OrderByDynamic([new DynamicOrderBy { FieldName = "NotExists", Direction = "asc" }])
                .ToList());
        ex.Message.ShouldContain("NotExists");
        ex.Message.ShouldContain(nameof(TestEntity));
    }

    [Fact]
    public void OrderByDynamic_UnknownNestedField_ShouldThrow()
    {
        var ex = Should.Throw<DynamicQueryException>(() =>
            Query()
                .OrderByDynamic([new DynamicOrderBy { FieldName = "Nested.Missing", Direction = "asc" }])
                .ToList());
        ex.Message.ShouldContain("Nested.Missing");
    }

    [Fact]
    public void OrderByDynamic_NullOrEmptyList_ShouldNotApplyOrderBy()
    {
        var source = Query();
        var nullOrdered = source.OrderByDynamic((IEnumerable<DynamicOrderBy>?)null);
        var emptyOrdered = source.OrderByDynamic([]);

        nullOrdered.Expression.ToString().ShouldNotContain("OrderBy");
        emptyOrdered.Expression.ToString().ShouldNotContain("OrderBy");
    }

    [Fact]
    public void OrderByDynamic_ViaIDynamicOrderByRequest_ShouldSort()
    {
        var request = new TestOrderByRequest
        {
            OrderBys =
            [
                new DynamicOrderBy { FieldName = "Age", Direction = "desc" }
            ]
        };

        var result = Query()
            .OrderByDynamic(request)
            .Select(x => x.Name)
            .ToList();

        result.ShouldBe(["Charlie", "Bob", "David", "Alice", "Eve"]);
    }

    [Fact]
    public void OrderByDynamic_ViaIDynamicOrderByRequest_NullRequest_ShouldNotApplyOrderBy()
    {
        var ordered = Query().OrderByDynamic((IDynamicOrderByRequest?)null);

        ordered.Expression.ToString().ShouldNotContain("OrderBy");
    }

    [Fact]
    public void OrderByDynamic_ViaPagedRequestBase_ShouldSort()
    {
        var request = new TestPagedOrderRequest
        {
            OrderBys =
            [
                new DynamicOrderBy { FieldName = "Name", Direction = "asc" }
            ]
        };

        var result = Query()
            .OrderByDynamic(request)
            .Select(x => x.Name)
            .ToList();

        result.ShouldBe(["Alice", "Bob", "Charlie", "David", "Eve"]);
    }

    [Fact]
    public void OrderByDynamic_DateTimeOffset_ShouldSort()
    {
        var result = Query()
            .OrderByDynamic([new DynamicOrderBy { FieldName = "OccurredAt", Direction = "desc" }])
            .Select(x => x.Name)
            .ToList();

        result.ShouldBe(["Eve", "David", "Charlie", "Bob", "Alice"]);
    }

    private IQueryable<TestEntity> Query() => _data.AsQueryable();

    private sealed class TestOrderByRequest : IDynamicOrderByRequest
    {
        public List<DynamicOrderBy>? OrderBys { get; set; }
    }

    private sealed class TestPagedOrderRequest : PagedRequestBase;
}
