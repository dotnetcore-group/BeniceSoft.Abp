using BeniceSoft.Core;
using BeniceSoft.Core.Reflector;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>
/// EF Core 内部反射基建
/// <para>
/// 供 QueryFuture / Hint 等能力：从 <see cref="IQueryable"/> 还原 DbContext、编译 SQL 命令、创建挂事务的 StoreCommand。
/// </para>
/// </summary>
[SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "QueryFuture/Hint require EF Core internal query pipeline access.")]
public static class InternalExtensions
{
    #region IQueryable

    private static QueryCompiler GetQueryCompiler(this IQueryable query)
    {
        var compilerField = typeof(EntityQueryProvider).GetField("_queryCompiler", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var compiler = compilerField.GetReflector().GetValue(query.Provider);
        if (compiler is QueryCompiler queryCompiler)
        {
            return queryCompiler;
        }

        var compilerType = compiler!.GetType();
        if (!compilerType.IsGenericType || compilerType.Name != "EntityQueryCompiler`1")
        {
            throw new NotSupportedException($"IQueryCompiler type {compilerType}");
        }

        return (QueryCompiler)compilerType
            .GetField("_innerCompiler", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetReflector().GetValue(compiler)!;
    }

    /// <summary>从 IQueryable 反查所属 DbContext。</summary>
    internal static DbContext GetDbContext(this IQueryable query)
    {
        var compiler = query.GetQueryCompiler();

        var queryContextFactory = (RelationalQueryContextFactory)typeof(QueryCompiler)
            .GetField("_queryContextFactory", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetReflector().GetValue(compiler)!;

        var dependencies = typeof(RelationalQueryContextFactory)
            .GetProperty("Dependencies", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetReflector().GetValue(queryContextFactory)!;

        var stateManagerProperty = typeof(DbContext).Assembly
            .GetType("Microsoft.EntityFrameworkCore.Query.QueryContextDependencies")!
            .GetProperty("StateManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;

        var stateManagerDynamic = stateManagerProperty.GetReflector().GetValue(dependencies)!;
        if (stateManagerDynamic is not IStateManager stateManager)
        {
            stateManager = ((dynamic)stateManagerDynamic).Value;
        }

        return stateManager.Context;
    }

    /// <summary>
    /// 编译查询并得到可执行的 <see cref="IRelationalCommand"/>（含参数上下文）。
    /// EF10：通过 <see cref="RelationalCommandResolver"/> 委托 + <see cref="QueryContext.Parameters"/> 解析命令。
    /// </summary>
    internal static IRelationalCommand CreateCommand(this IQueryable query, out RelationalQueryContext queryContext)
    {
        query.GetExpression(out queryContext, out var relationalCommand, out _);
        return relationalCommand;
    }

    /// <summary>同上，并返回编译后的 QueryingEnumerable（供 Future 注入 DataReader 物化）。</summary>
    internal static IRelationalCommand CreateCommand(this IQueryable query, out RelationalQueryContext queryContext, out object compiledQuery)
    {
        query.GetExpression(out queryContext, out var relationalCommand, out compiledQuery);
        return relationalCommand;
    }

    /// <summary>解析 SelectExpression（Hint / Future 共用）。</summary>
    public static SelectExpression GetSelectExpression(this IQueryable query)
    {
        return query.GetSelectExpression(out _, out _, out _)
               ?? throw new InvalidDataException("Unable to resolve SelectExpression from query.");
    }

    internal static SelectExpression? GetSelectExpression(
        this IQueryable query,
        out RelationalQueryContext queryContext,
        out QueryCompilationContext queryCompilationContext,
        out Expression expression)
    {
        var queryCompiler = query.GetQueryCompiler();

        var database = (RelationalDatabase)typeof(QueryCompiler)
            .GetField("_database", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetReflector().GetValue(queryCompiler)!;

        var queryContextFactory = (IQueryContextFactory)typeof(QueryCompiler)
            .GetField("_queryContextFactory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetReflector().GetValue(queryCompiler)!;

        queryContext = (RelationalQueryContext)queryContextFactory.Create();

        var evaluatableExpressionFilter = (IEvaluatableExpressionFilter)typeof(QueryCompiler)
            .GetField("_evaluatableExpressionFilter", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetReflector().GetValue(queryCompiler)!;

        var databaseDependencies = (DatabaseDependencies)typeof(Database)
            .GetProperty("Dependencies", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetReflector().GetValue(database)!;

        queryCompilationContext = databaseDependencies.QueryCompilationContextFactory.Create(false);

        // EF10：ExpressionTreeFuncletizer + QueryContext.Parameters
        expression = new ExpressionTreeFuncletizer(
                queryCompilationContext.Model,
                evaluatableExpressionFilter,
                queryCompilationContext.ContextType,
                true,
                queryCompilationContext.Logger)
            .ExtractParameters(query.Expression, queryContext.Parameters, true, false);

        var queryTranslationPreprocessorFactory = (IQueryTranslationPreprocessorFactory)typeof(QueryCompilationContext)
            .GetField("_queryTranslationPreprocessorFactory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetReflector().GetValue(queryCompilationContext)!;

        var queryableMethodTranslating = (IQueryableMethodTranslatingExpressionVisitorFactory)typeof(QueryCompilationContext)
            .GetField("_queryableMethodTranslatingExpressionVisitorFactory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetReflector().GetValue(queryCompilationContext)!;

        expression = queryTranslationPreprocessorFactory.Create(queryCompilationContext).Process(expression);
        expression = queryableMethodTranslating.Create(queryCompilationContext).Translate(expression);

        var shapedQueryExpression = (ShapedQueryExpression)expression;
        if (shapedQueryExpression.ResultCardinality != 0)
        {
            shapedQueryExpression.UpdateResultCardinality(0);
        }

        return shapedQueryExpression.QueryExpression as SelectExpression;
    }

    /// <summary>
    /// 完整编译管线 → QueryingEnumerable + IRelationalCommand。
    /// 原理：走 EF 内部 Translate/Compile，再 Invoke RelationalCommandResolver(Parameters) 得到命令文本与参数。
    /// </summary>
    internal static SelectExpression? GetExpression(
        this IQueryable query,
        out RelationalQueryContext queryContext,
        out IRelationalCommand relationalCommand,
        out object compiledQuery)
    {
        var expr = query.GetSelectExpression(out queryContext, out var queryCompilationContext, out var expression);

        var queryTranslationPostprocessorFactory = (IQueryTranslationPostprocessorFactory)typeof(QueryCompilationContext)
            .GetField("_queryTranslationPostprocessorFactory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetReflector().GetValue(queryCompilationContext)!;

        var shapedQueryCompiling = (IShapedQueryCompilingExpressionVisitorFactory)typeof(QueryCompilationContext)
            .GetField("_shapedQueryCompilingExpressionVisitorFactory", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetReflector().GetValue(queryCompilationContext)!;

        var insertRuntimeParameters = typeof(QueryCompilationContext)
            .GetMethod("InsertRuntimeParameters", BindingFlags.Instance | BindingFlags.NonPublic)!;

        expression = queryTranslationPostprocessorFactory.Create(queryCompilationContext).Process(expression);
        expression = shapedQueryCompiling.Create(queryCompilationContext).Visit(expression);
        expression = (Expression)insertRuntimeParameters.GetReflector().Invoke(queryCompilationContext, [expression])!;

        // EF10：可提升常量内联
        var dependencies = (QueryCompilationContextDependencies)typeof(QueryCompilationContext)
            .GetProperty("Dependencies", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .GetReflector().GetValue(queryCompilationContext)!;
        expression = dependencies.LiftableConstantProcessor.InlineConstants(expression, false);

        var selfCompile = typeof(InternalExtensions)
            .GetMethod(nameof(SelfCompile), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(expression.Type);
        var func = selfCompile.GetReflector().Invoke(null!, [expression, queryCompilationContext])!;
        var result = ((Delegate)func).DynamicInvoke(queryContext)!;
        compiledQuery = result;

        // EF10：RelationalCommandResolver 是委托 Func&lt;Dictionary, IRelationalCommandTemplate&gt;
        var relationalCommandResolver = (RelationalCommandResolver)result.GetType()
            .GetField("_relationalCommandResolver", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetReflector().GetValue(result)!;

        relationalCommand = (IRelationalCommand)relationalCommandResolver.Invoke(queryContext.Parameters);
        return expr;
    }

    #endregion

    #region IQueryable&lt;T&gt;

    internal static DbContext GetInMemoryContext<T>(this IQueryable<T> query)
    {
        var compiler = query.GetQueryCompiler();

        var queryContextFactory = typeof(QueryCompiler)
            .GetField("_queryContextFactory", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetReflector().GetValue(compiler)!;

        var dependencies = typeof(RelationalQueryContextFactory)
            .GetProperty("Dependencies", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetReflector().GetValue(queryContextFactory)!;

        var stateManagerProperty = typeof(DbContext).Assembly
            .GetType("Microsoft.EntityFrameworkCore.Query.QueryContextDependencies")!
            .GetProperty("StateManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;

        var stateManagerDynamic = stateManagerProperty.GetReflector().GetValue(dependencies)!;
        if (stateManagerDynamic is not IStateManager stateManager)
        {
            stateManager = ((dynamic)stateManagerDynamic).Value;
        }

        return stateManager.Context;
    }

    internal static bool IsInMemoryQueryContext<T>(this IQueryable<T> query)
    {
        var compiler = query.GetQueryCompiler();
        var queryContextFactory = typeof(QueryCompiler)
            .GetField("_queryContextFactory", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetReflector().GetValue(compiler)!;

        return queryContextFactory is not RelationalQueryContextFactory
               && queryContextFactory.GetType().Name == "InMemoryQueryContextFactory";
    }

    #endregion

    #region DbSet / DbContext / DbParameter

    internal static DbContext GetDbContext<T>(this DbSet<T> set)
        where T : class
    {
        var contextField = set.GetType().GetField("_context", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (DbContext)contextField.GetReflector().GetValue(set)!;
    }

    internal static string[] GetKeyNames<T>(this DbContext ctx)
        where T : class
    {
        var entityType = ctx.Model.FindEntityType(typeof(T))
                         ?? throw new InvalidOperationException($"Entity type {typeof(T).Name} not found.");
        return entityType.GetKeys().SelectMany(x => x.Properties.Select(y => y.Name)).ToArray();
    }

    /// <summary>创建绑定当前事务与超时的 ADO Command（批 SQL 执行入口）。</summary>
    internal static DbCommand CreateStoreCommand(this DbContext ctx)
    {
        DbCommand command;
        if (ctx.Database.ProviderName == "Devart.Data.Oracle.Entity.EFCore")
        {
            var oracleExtensions = ctx.Database.GetDbConnection().GetType().Assembly
                .GetType("Microsoft.EntityFrameworkCore.OracleRelationalDatabaseFacadeExtensions")!;
            var getOracleConnection = oracleExtensions.GetMethod("GetOracleConnection", BindingFlags.Static | BindingFlags.Public)!;
            var entityConnection = getOracleConnection.GetReflector().Invoke(oracleExtensions, [ctx.Database])!;
            command = ((dynamic)entityConnection).CreateCommand();
        }
        else
        {
            command = ctx.Database.GetDbConnection().CreateCommand();
        }

        var entityTransaction = ctx.Database.GetService<IRelationalConnection>().CurrentTransaction;
        if (entityTransaction != null)
        {
            command.Transaction = entityTransaction.GetDbTransaction();
        }

        var commandTimeout = ctx.Database.GetCommandTimeout();
        if (commandTimeout.HasValue)
        {
            command.CommandTimeout = commandTimeout.Value;
        }

        return command;
    }

    internal static void CopyFrom(this DbParameter param, IRelationalParameter from, object? value, string newParameterName)
    {
        param.ParameterName = newParameterName;

        if (from is TypeMappedRelationalParameter)
        {
            var relationalTypeMappingProperty = typeof(TypeMappedRelationalParameter)
                .GetProperty("RelationalTypeMapping", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (relationalTypeMappingProperty != null)
            {
                var relationalTypeMapping = (RelationalTypeMapping?)relationalTypeMappingProperty.GetReflector().GetValue(from);
                if (relationalTypeMapping?.DbType != null)
                {
                    param.DbType = relationalTypeMapping.DbType.Value;
                }
            }
        }

        param.Value = value ?? DBNull.Value;
    }

    #endregion

    #region Expression helpers

    private static Func<QueryContext, TResult> SelfCompile<TResult>(Expression expression, QueryCompilationContext queryCompilationContext)
    {
        var lambda = Expression.Lambda<Func<QueryContext, TResult>>(expression, [QueryCompilationContext.QueryContextParameter]);
        try
        {
            return lambda.Compile();
        }
        finally
        {
            CoreLoggerExtensions.QueryExecutionPlanned(queryCompilationContext.Logger, null, new ExpressionPrinter(), lambda);
        }
    }

    private static Expression RemoveConvert(Expression expression)
    {
        while (expression.NodeType.In(ExpressionType.Convert, ExpressionType.ConvertChecked))
        {
            expression = ((UnaryExpression)expression).Operand;
        }

        return expression;
    }

    internal static PropertyOrFieldAccessor GetPropertyOrFieldAccess(this Expression expression, ParameterExpression parameterExpression)
    {
        var paths = new List<MemberInfo>();
        var current = expression;

        MemberExpression? memberExpression;
        do
        {
            memberExpression = RemoveConvert(current) as MemberExpression;
            if (memberExpression == null)
            {
                throw new InvalidOperationException("invalid expression.");
            }

            if (memberExpression.Member is PropertyInfo propertyInfo)
            {
                paths.Add(propertyInfo);
            }

            if (memberExpression.Member is FieldInfo fieldInfo)
            {
                paths.Add(fieldInfo);
            }

            current = memberExpression.Expression!;
        }
        while (memberExpression.Expression != parameterExpression);

        paths.Reverse();
        return new PropertyOrFieldAccessor(paths.AsReadOnly());
    }

    internal static PropertyOrFieldAccessor[] GetPropertyOrFieldAccessors(this LambdaExpression lambda)
    {
        var parameterExpression = lambda.Parameters.Single();
        if (RemoveConvert(lambda.Body) is NewExpression newExpression)
        {
            return newExpression.Arguments.Select(x => x.GetPropertyOrFieldAccess(parameterExpression)).ToArray();
        }

        return [lambda.Body.GetPropertyOrFieldAccess(parameterExpression)];
    }

    #endregion
}
