using BeniceSoft.Core;
using BeniceSoft.Extensions.DynamicQuery;
using BeniceSoft.Abp.Extensions.DynamicQuery.Sql.Extensions;
using BeniceSoft.Abp.Extensions.DynamicQuery.Sql.Tests.TestModels;
using Shouldly;
using Xunit;
using BeniceSoft.Core.Constants;

namespace BeniceSoft.Abp.Extensions.DynamicQuery.Sql.Tests;

public class DynamicQuerySqlTests
{
    private const string BaseSql = "SELECT * FROM Users";

    #region Equal Operator Tests

    [Fact]
    public void DynamicQueryBy_Equal_String_SqlServer_ShouldGenerateCorrectSql()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.Equal, "Alice");

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("[Name] = @p");
        result.Sql.ShouldContain("__dyq_temp_table");
    }

    [Fact]
    public void DynamicQueryBy_Equal_Integer_SqlServer_ShouldGenerateCorrectSql()
    {
        // Arrange
        var request = CreateRequest("Age", BeniceSoftTypeNameConstant.Integer, ExprOperator.Equal, "30");

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("[Age] = @p");
    }

    [Fact]
    public void DynamicQueryBy_Equal_Long_SqlServer_ShouldGenerateCorrectSql()
    {
        // Arrange
        var request = CreateRequest("TotalCount", BeniceSoftTypeNameConstant.Long, ExprOperator.Equal, "9999999999");

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("[TotalCount] = @p");
    }

    [Fact]
    public void DynamicQueryBy_Equal_DateTime_SqlServer_ShouldGenerateCorrectSql()
    {
        // Arrange
        var request = CreateRequest("CreatedAt", BeniceSoftTypeNameConstant.DateTime, ExprOperator.Equal, "2024-01-01");

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("[CreatedAt] = @p");
    }

    [Fact]
    public void DynamicQueryBy_Equal_Guid_SqlServer_ShouldGenerateCorrectSql()
    {
        // Arrange
        var request = CreateRequest("UniqueId", BeniceSoftTypeNameConstant.Guid, ExprOperator.Equal, "11111111-1111-1111-1111-111111111111");

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("[UniqueId] = @p");
    }

    #endregion

    #region NotEqual Operator Tests

    [Fact]
    public void DynamicQueryBy_NotEqual_String_SqlServer_ShouldGenerateCorrectSql()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.NotEqual, "Alice");

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("[Name] <> @p");
    }

    [Fact]
    public void DynamicQueryBy_NotEqual_Integer_SqlServer_ShouldGenerateCorrectSql()
    {
        // Arrange
        var request = CreateRequest("Age", BeniceSoftTypeNameConstant.Integer, ExprOperator.NotEqual, "25");

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("[Age] <> @p");
    }

    #endregion

    #region In Operator Tests

    [Fact]
    public void DynamicQueryBy_In_Integer_SqlServer_ShouldGenerateCorrectSql()
    {
        // Arrange
        var request = CreateRequest("Age", BeniceSoftTypeNameConstant.Integer, ExprOperator.In, "25", "30", "35");

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("[Age] IN (@p0, @p1, @p2)");
    }

    [Fact]
    public void DynamicQueryBy_In_String_SqlServer_ShouldGenerateCorrectSql()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.In, "Alice", "Bob");

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("[Name] IN (@p0, @p1)");
    }

    #endregion

    #region Multiple Compiler Tests

    [Fact]
    public void DynamicQueryBy_Equal_MySql_ShouldGenerateCorrectSql()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.Equal, "Alice");

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.MySql);

        // Assert
        result.Sql.ShouldContain("`Name` = @p");
    }

    [Fact]
    public void DynamicQueryBy_Equal_Postgres_ShouldGenerateCorrectSql()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.Equal, "Alice");

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.Postgres);

        // Assert
        result.Sql.ShouldContain("\"Name\" = @p");
    }

    [Fact]
    public void DynamicQueryBy_Equal_Sqlite_ShouldGenerateCorrectSql()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.Equal, "Alice");

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.Sqlite);

        // Assert
        result.Sql.ShouldContain("\"Name\" = @p");
    }

    #endregion

    #region Multiple Conditions Tests

    [Fact]
    public void DynamicQueryBy_MultipleConditions_And_ShouldGenerateCorrectSql()
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
                        new() { Relation = BeniceSoftRelationConstant.And, FieldName = "Age", FieldType = BeniceSoftTypeNameConstant.Integer, Operator = ExprOperator.Equal, Value = new List<string> { "25" } }
                    }
                }
            }
        };

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("[Name] = @p");
        result.Sql.ShouldContain("[Age] = @p");
        result.Sql.ShouldContain("AND");
    }

    [Fact]
    public void DynamicQueryBy_MultipleConditions_Or_ShouldGenerateCorrectSql()
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
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("[Name] = @p");
        result.Sql.ShouldContain("OR");
    }

    #endregion

    #region Multiple Condition Groups Tests

    [Fact]
    public void DynamicQueryBy_MultipleConditionGroups_ShouldGenerateCorrectSql()
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
                        new() { FieldName = "Name", FieldType = BeniceSoftTypeNameConstant.String, Operator = ExprOperator.Equal, Value = new List<string> { "Alice" } }
                    }
                },
                new()
                {
                    Relation = BeniceSoftRelationConstant.Or,
                    Conditions = new List<DynamicQueryCondition>
                    {
                        new() { FieldName = "Age", FieldType = BeniceSoftTypeNameConstant.Integer, Operator = ExprOperator.Equal, Value = new List<string> { "30" } }
                    }
                }
            }
        };

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("[Name] = @p");
        result.Sql.ShouldContain("[Age] = @p");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void DynamicQueryBy_EmptyConditionGroups_ShouldGenerateBaseSql()
    {
        // Arrange
        var request = new TestDynamicQueryRequest { ConditionGroups = new List<DynamicQueryConditionGroup>() };

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("1=1");
        result.Sql.ShouldContain("__dyq_temp_table");
    }

    [Fact]
    public void DynamicQueryBy_NullConditionGroups_ShouldGenerateBaseSql()
    {
        // Arrange
        var request = new TestDynamicQueryRequest { ConditionGroups = null };

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("1=1");
    }

    [Fact]
    public void DynamicQueryBy_ComplexSql_ShouldWrapAsSubquery()
    {
        // Arrange
        const string complexSql = "SELECT u.*, o.OrderCount FROM Users u LEFT JOIN (SELECT UserId, COUNT(*) as OrderCount FROM Orders GROUP BY UserId) o ON u.Id = o.UserId";
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.Equal, "Alice");

        // Act
        var result = complexSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.Sql.ShouldContain("__dyq_temp_table");
        result.Sql.ShouldContain("[Name] = @p");
    }

    #endregion

    #region Parameter Binding Tests

    [Fact]
    public void DynamicQueryBy_ShouldBindParametersCorrectly()
    {
        // Arrange
        var request = CreateRequest("Name", BeniceSoftTypeNameConstant.String, ExprOperator.Equal, "Alice");

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.NamedBindings.ShouldNotBeEmpty();
        result.NamedBindings.Values.ShouldContain("'Alice'");
    }

    [Fact]
    public void DynamicQueryBy_In_ShouldBindMultipleParametersCorrectly()
    {
        // Arrange
        var request = CreateRequest("Age", BeniceSoftTypeNameConstant.Integer, ExprOperator.In, "25", "30", "35");

        // Act
        var result = BaseSql.DynamicQueryBy(request, SqlCompilerType.SqlServer);

        // Assert
        result.NamedBindings.Count.ShouldBe(3);
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

