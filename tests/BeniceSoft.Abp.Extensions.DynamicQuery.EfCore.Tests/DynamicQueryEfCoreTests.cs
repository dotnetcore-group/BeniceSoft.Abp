using BeniceSoft.Core;
using BeniceSoft.Extensions.DynamicQuery;
using BeniceSoft.Abp.Extensions.DynamicQuery.EfCore.Extensions;
using BeniceSoft.Abp.Extensions.DynamicQuery.EfCore.Tests.TestModels;
using Shouldly;
using Xunit;
using BeniceSoft.Core.Constants;

namespace BeniceSoft.Abp.Extensions.DynamicQuery.EfCore.Tests;

public class DynamicQueryEfCoreTests
{
    private readonly List<TestEntity> _testData;

    public DynamicQueryEfCoreTests()
    {
        _testData = CreateTestData();
    }

    private static List<TestEntity> CreateTestData()
    {
        return new List<TestEntity>
        {
            new() { Id = 1, Name = "Alice", Age = 25, TotalCount = 1000000000L, Price = 100.5, IsActive = true, CreatedAt = new DateTime(2024, 1, 1), UniqueId = Guid.Parse("11111111-1111-1111-1111-111111111111"), Tags = new List<string> { "tag1", "tag2" } },
            new() { Id = 2, Name = "Bob", Age = 30, TotalCount = 2000000000L, Price = 200.0, IsActive = false, CreatedAt = new DateTime(2024, 2, 1), UniqueId = Guid.Parse("22222222-2222-2222-2222-222222222222"), Tags = new List<string> { "tag2", "tag3" } },
            new() { Id = 3, Name = "Charlie", Age = 35, TotalCount = 3000000000L, Price = 150.75, IsActive = true, CreatedAt = new DateTime(2024, 3, 1), UniqueId = Guid.Parse("33333333-3333-3333-3333-333333333333"), Tags = new List<string> { "tag1" } },
            new() { Id = 4, Name = "David", Age = 28, TotalCount = 4000000000L, Price = 300.0, IsActive = true, CreatedAt = new DateTime(2024, 4, 1), UniqueId = Guid.Parse("44444444-4444-4444-4444-444444444444"), Tags = new List<string> { "tag3", "tag4" } },
            new() { Id = 5, Name = "Eve", Age = 22, TotalCount = 5000000000L, Price = 50.25, IsActive = false, CreatedAt = new DateTime(2024, 5, 1), UniqueId = Guid.Parse("55555555-5555-5555-5555-555555555555"), Tags = new List<string>() }
        };
    }

    #region Equal Operator Tests

    [Fact]
    public void DynamicQueryBy_Equal_String_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.Equal, "Alice");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Alice");
    }

    [Fact]
    public void DynamicQueryBy_Equal_Integer_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Age", BeniceSoftTypeNameConstant.Integer, ExprOperator.Equal, "30");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Bob");
    }

    [Fact]
    public void DynamicQueryBy_Equal_Boolean_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("IsActive", BeniceSoftTypeNameConstant.Boolean, ExprOperator.Equal, "true");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(3);
        result.All(x => x.IsActive).ShouldBeTrue();
    }

    [Fact]
    public void DynamicQueryBy_Equal_Long_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("TotalCount", BeniceSoftTypeNameConstant.Long, ExprOperator.Equal, "2000000000");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Bob");
    }

    [Fact]
    public void DynamicQueryBy_GreaterThan_Long_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("TotalCount", BeniceSoftTypeNameConstant.Long, ExprOperator.GreaterThan, "3000000000");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(2); // David (4000000000), Eve (5000000000)
        result.All(x => x.TotalCount > 3000000000L).ShouldBeTrue();
    }

    [Fact]
    public void DynamicQueryBy_In_Long_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("TotalCount", BeniceSoftTypeNameConstant.Long, ExprOperator.In, "1000000000", "3000000000", "5000000000");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(3);
        result.All(x => new[] { 1000000000L, 3000000000L, 5000000000L }.Contains(x.TotalCount)).ShouldBeTrue();
    }

    [Fact]
    public void DynamicQueryBy_Between_Long_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("TotalCount", BeniceSoftTypeNameConstant.Long, ExprOperator.Between, "2000000000", "4000000000");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(3); // Bob, Charlie, David
        result.All(x => x.TotalCount >= 2000000000L && x.TotalCount <= 4000000000L).ShouldBeTrue();
    }

    #endregion

    #region NotEqual Operator Tests

    [Fact]
    public void DynamicQueryBy_NotEqual_String_ShouldReturnNonMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.NotEqual, "Alice");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(4);
        result.Any(x => x.Name == "Alice").ShouldBeFalse();
    }

    #endregion

    #region GreaterThan / LessThan Operator Tests

    [Fact]
    public void DynamicQueryBy_GreaterThan_Integer_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Age", BeniceSoftTypeNameConstant.Integer, ExprOperator.GreaterThan, "28");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(2);
        result.All(x => x.Age > 28).ShouldBeTrue();
    }

    [Fact]
    public void DynamicQueryBy_GreaterThanOrEqual_Integer_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Age", BeniceSoftTypeNameConstant.Integer, ExprOperator.GreaterThanOrEqual, "28");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(3);
        result.All(x => x.Age >= 28).ShouldBeTrue();
    }

    [Fact]
    public void DynamicQueryBy_LessThan_Double_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Price", BeniceSoftTypeNameConstant.Double, ExprOperator.LessThan, "150");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(2);
        result.All(x => x.Price < 150).ShouldBeTrue();
    }

    [Fact]
    public void DynamicQueryBy_LessThanOrEqual_Double_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Price", BeniceSoftTypeNameConstant.Double, ExprOperator.LessThanOrEqual, "150.75");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(3);
        result.All(x => x.Price <= 150.75).ShouldBeTrue();
    }

    #endregion

    #region Contains / StartsWith / EndsWith Tests

    [Fact]
    public void DynamicQueryBy_Contains_String_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.Contains, "li");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(2); // Alice, Charlie
        result.All(x => x.Name.ToLower().Contains("li")).ShouldBeTrue();
    }

    [Fact]
    public void DynamicQueryBy_StartsWith_String_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.StartsWith, "a");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Alice");
    }

    [Fact]
    public void DynamicQueryBy_EndsWith_String_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.EndsWith, "e");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(3); // Alice, Charlie, Eve
        result.All(x => x.Name.ToLower().EndsWith("e")).ShouldBeTrue();
    }

    [Fact]
    public void DynamicQueryBy_NotContains_String_ShouldReturnNonMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.NotContains, "li");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(3); // Bob, David, Eve
        result.All(x => !x.Name.ToLower().Contains("li")).ShouldBeTrue();
    }

    #endregion

    #region In / NotIn Operator Tests

    [Fact]
    public void DynamicQueryBy_In_Integer_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Age", BeniceSoftTypeNameConstant.Integer, ExprOperator.In, "25", "30", "35");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(3);
        result.All(x => new[] { 25, 30, 35 }.Contains(x.Age)).ShouldBeTrue();
    }

    [Fact]
    public void DynamicQueryBy_In_String_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.In, "Alice", "Bob");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(2);
        result.All(x => new[] { "Alice", "Bob" }.Contains(x.Name)).ShouldBeTrue();
    }

    [Fact]
    public void DynamicQueryBy_NotIn_Integer_ShouldReturnNonMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Age", BeniceSoftTypeNameConstant.Integer, ExprOperator.NotIn, "25", "30");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(3);
        result.All(x => !new[] { 25, 30 }.Contains(x.Age)).ShouldBeTrue();
    }

    #endregion

    #region Between Operator Tests

    [Fact]
    public void DynamicQueryBy_Between_Integer_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Age", BeniceSoftTypeNameConstant.Integer, ExprOperator.Between, "25", "30");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(3); // Age 25, 28, 30
        result.All(x => x.Age >= 25 && x.Age <= 30).ShouldBeTrue();
    }

    [Fact]
    public void DynamicQueryBy_Between_DateTime_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("CreatedAt", BeniceSoftTypeNameConstant.DateTime, ExprOperator.Between, "2024-02-01", "2024-04-01");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(3); // Feb, Mar, Apr
        result.All(x => x.CreatedAt >= new DateTime(2024, 2, 1) && x.CreatedAt <= new DateTime(2024, 4, 1)).ShouldBeTrue();
    }

    #endregion

    #region DateTime Tests

    [Fact]
    public void DynamicQueryBy_Equal_DateTime_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("CreatedAt", BeniceSoftTypeNameConstant.DateTime, ExprOperator.Equal, "2024-01-01");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Alice");
    }

    [Fact]
    public void DynamicQueryBy_GreaterThan_DateTime_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("CreatedAt", BeniceSoftTypeNameConstant.DateTime, ExprOperator.GreaterThan, "2024-03-01");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(2); // Apr, May
        result.All(x => x.CreatedAt > new DateTime(2024, 3, 1)).ShouldBeTrue();
    }

    #endregion

    #region Guid Tests

    [Fact]
    public void DynamicQueryBy_Equal_Guid_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("UniqueId", BeniceSoftTypeNameConstant.Guid, ExprOperator.Equal, "11111111-1111-1111-1111-111111111111");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Alice");
    }

    #endregion

    #region Multiple Conditions Tests

    [Fact]
    public void DynamicQueryBy_MultipleConditions_And_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = new TestDynamicQueryRequest
        {
            ConditionGroups = new List<DynamicQueryConditionGroup>
            {
                new()
                {
                    Relation = BeniceSoftRelationConstant.And,
                    Conditions = new List<DynamicQueryCondition>
                    {
                        new() { FieldName = "IsActive", FieldType = BeniceSoftTypeNameConstant.Boolean, Operator = ExprOperator.Equal, Value = new List<string> { "true" } },
                        new() { Relation = BeniceSoftRelationConstant.And, FieldName = "Age", FieldType = BeniceSoftTypeNameConstant.Integer, Operator = ExprOperator.GreaterThan, Value = new List<string> { "25" } }
                    }
                }
            }
        };

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(2); // Charlie (35), David (28)
        result.All(x => x.IsActive && x.Age > 25).ShouldBeTrue();
    }

    [Fact]
    public void DynamicQueryBy_MultipleConditions_Or_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = new TestDynamicQueryRequest
        {
            ConditionGroups = new List<DynamicQueryConditionGroup>
            {
                new()
                {
                    Relation = BeniceSoftRelationConstant.And,
                    Conditions = new List<DynamicQueryCondition>
                    {
                        new() { FieldName = "Name", FieldType = BeniceSoftTypeNameConstant.String, Operator = ExprOperator.Equal, Value = new List<string> { "Alice" } },
                        new() { Relation = BeniceSoftRelationConstant.Or, FieldName = "Name", FieldType = BeniceSoftTypeNameConstant.String, Operator = ExprOperator.Equal, Value = new List<string> { "Bob" } }
                    }
                }
            }
        };

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(2);
        result.Select(x => x.Name).ShouldContain("Alice");
        result.Select(x => x.Name).ShouldContain("Bob");
    }

    #endregion

    #region Multiple Condition Groups Tests

    [Fact]
    public void DynamicQueryBy_MultipleConditionGroups_Or_ShouldReturnMatchingRecords()
    {
        // Arrange: (IsActive = true AND Age > 30) OR (IsActive = false AND Age < 25)
        var request = new TestDynamicQueryRequest
        {
            ConditionGroups = new List<DynamicQueryConditionGroup>
            {
                new()
                {
                    Relation = BeniceSoftRelationConstant.And,
                    Conditions = new List<DynamicQueryCondition>
                    {
                        new() { FieldName = "IsActive", FieldType = BeniceSoftTypeNameConstant.Boolean, Operator = ExprOperator.Equal, Value = new List<string> { "true" } },
                        new() { Relation = BeniceSoftRelationConstant.And, FieldName = "Age", FieldType = BeniceSoftTypeNameConstant.Integer, Operator = ExprOperator.GreaterThan, Value = new List<string> { "30" } }
                    }
                },
                new()
                {
                    Relation = BeniceSoftRelationConstant.Or,
                    Conditions = new List<DynamicQueryCondition>
                    {
                        new() { FieldName = "IsActive", FieldType = BeniceSoftTypeNameConstant.Boolean, Operator = ExprOperator.Equal, Value = new List<string> { "false" } },
                        new() { Relation = BeniceSoftRelationConstant.And, FieldName = "Age", FieldType = BeniceSoftTypeNameConstant.Integer, Operator = ExprOperator.LessThan, Value = new List<string> { "25" } }
                    }
                }
            }
        };

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(2); // Charlie (active, 35), Eve (inactive, 22)
        result.Select(x => x.Name).ShouldContain("Charlie");
        result.Select(x => x.Name).ShouldContain("Eve");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void DynamicQueryBy_EmptyConditionGroups_ShouldReturnAllRecords()
    {
        // Arrange
        var request = new TestDynamicQueryRequest { ConditionGroups = new List<DynamicQueryConditionGroup>() };

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(5);
    }

    [Fact]
    public void DynamicQueryBy_NullConditionGroups_ShouldReturnAllRecords()
    {
        // Arrange
        var request = new TestDynamicQueryRequest { ConditionGroups = null };

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(5);
    }

    [Fact]
    public void DynamicQueryBy_CaseInsensitive_String_ShouldReturnMatchingRecords()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.Equal, "ALICE");

        // Act
        var result = _testData.DynamicQueryBy(request).ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Alice");
    }

    #endregion

    #region Helper Methods

    private static TestDynamicQueryRequest CreateRequest(string fieldName, string fieldType, ExprOperator @operator, params string[] values)
    {
        return new TestDynamicQueryRequest
        {
            ConditionGroups = new List<DynamicQueryConditionGroup>
            {
                new()
                {
                    Relation = BeniceSoftRelationConstant.And,
                    Conditions = new List<DynamicQueryCondition>
                    {
                        new()
                        {
                            FieldName = fieldName,
                            FieldType = fieldType,
                            Operator = @operator,
                            Value = values.ToList()
                        }
                    }
                }
            }
        };
    }

    #endregion
}
