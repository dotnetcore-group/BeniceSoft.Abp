using BeniceSoft.Core;
using BeniceSoft.Core.Reflector;
using BeniceSoft.Core.Strategy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal static class InternalExtensions
{
    #region Common
    internal static int SafeCompare(this IComparable? value, IComparable? other, SortDirection direction)
    {
        if (direction == SortDirection.Ascending)
        {
            return SafeCompare(value, other);
        }

        return SafeCompare(other, value);
    }

    private static int SafeCompare(IComparable? value, IComparable? other)
    {
        if (value == null && other == null)
        {
            return 0;
        }

        if (value == null)
        {
            return -1;
        }

        if (other == null)
        {
            return 1;
        }

        return value.CompareTo(other);
    }

    internal static bool SupportMerge(this IShardingDbContext shardingDbContext)
    {
        var ctx = (DbContext)shardingDbContext;
        return ctx.GetService<IDbContextServices>().ContextOptions.FindExtension<MergeOptionsExtension>() is not null;
    }

    /// <summary>
    /// 按size分区,每个区size个数目
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="elements"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    internal static IEnumerable<List<T>> Partition<T>(this IEnumerable<T> elements, int size)
    {
        return elements.Select((o, i) => new { Element = o, Index = i / size }).GroupBy(o => o.Index).Select(o => o.Select(g => g.Element).ToList());
    }

    internal static object CreateInstance(this IShardingProvider shardingProvider, Type serviceType)
    {
        var constructors = serviceType.DeclaredConstructors(c => !c.IsStatic && c.IsPublic).ToArray();

        if (constructors.Length != 1)
        {
            throw new ArgumentException($"type :[{serviceType}] found more than one declared constructor ");
        }

        var @params = constructors[0].GetParameters().Select(x => shardingProvider.GetService(x.ParameterType)).ToArray();
        return Activator.CreateInstance(serviceType, @params)
               ?? throw new InvalidOperationException($"Failed to create instance of type [{serviceType}]");
    }

    internal static bool TryGetTableRoute<T>(this IShardingRouteOptions routeOptions, [NotNullWhen(true)] out Type? routeType)
        where T : class
    {
        if (routeOptions.HasTableRoute(typeof(T)))
        {
            routeType = routeOptions.GetTableRoute(typeof(T));
            return routeType != null;
        }

        routeType = null;
        return false;
    }

    internal static bool TryGetDataSourceRoute<T>(this IShardingRouteOptions routeOptions, [NotNullWhen(true)] out Type? routeType)
        where T : class
    {
        if (routeOptions.HasDataSourceRoute(typeof(T)))
        {
            routeType = routeOptions.GetDataSourceRoute(typeof(T));
            return routeType != null;
        }

        routeType = null;
        return false;
    }

    internal static bool IsDataSourceRoute(this Type routeType)
    {
        ArgumentNullException.ThrowIfNull(routeType);

        return typeof(IDataSourceRoute).IsAssignableFrom(routeType);
    }

    internal static bool IsTableRoute(this Type routeType)
    {
        ArgumentNullException.ThrowIfNull(routeType);

        return typeof(ITableRoute).IsAssignableFrom(routeType);
    }

    private static readonly string _tailPrefix = $"sharding_{RandomUtils.GuidString()}_";

    internal static string RouteTail(this string originalTail)
    {
        return $"{_tailPrefix}{originalTail}";
    }

    private static Type? TryGetElementType(this Type type, Type baseType)
    {
        if (type.IsGenericTypeDefinition)
        {
            return null;
        }

        var types = GetBaseTypes(type, baseType);

        Type? singleImpl = null;
        foreach (var impl in types)
        {
            if (singleImpl == null)
            {
                singleImpl = impl;
            }
            else
            {
                singleImpl = null;
                break;
            }
        }

        return singleImpl?.GenericTypeArguments.FirstOrDefault();
    }

    private static IEnumerable<Type> GetBaseTypes(Type type, Type baseType)
    {
        if (!type.IsGenericTypeDefinition)
        {
            var baseTypes = baseType.IsInterface ? type.GetInterfaces() : type.GetBaseTypes();
            foreach (var basicType in baseTypes)
            {
                if (basicType.IsGenericType && basicType.GetGenericTypeDefinition() == baseType)
                {
                    yield return basicType;
                }
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == baseType)
            {
                yield return type;
            }
        }
    }

    internal static bool HasImplemented(this Type type, Type generic)

    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(generic);

        // 测试接口。
        var rawType = type.GetInterfaces().Exists(IsTheRawGenericType);
        if (rawType)
        {
            return true;
        }

        // 测试类型。
        Type? current = type;
        while (current != null && current != typeof(object))
        {
            rawType = IsTheRawGenericType(current);
            if (rawType)
            {
                return true;
            }

            current = current.BaseType;
        }

        // 没有找到任何匹配的接口或类型。
        return false;

        // 测试某个类型是否是指定的原始接口。
        bool IsTheRawGenericType(Type test)
        {
            return generic == (test.IsGenericType ? test.GetGenericTypeDefinition() : test);
        }
    }
    #endregion

    #region PropertyValue
    private static readonly BindingFlags _bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static;

    internal static object? GetFieldValue(this Type type, object obj, string fieldName)
    {
        var field = type.GetField(fieldName, _bindingFlags)
                    ?? throw new ShardingNotFoundException($"type:{type} not found [{fieldName}] field");
        return field.GetReflector().GetValue(obj);
    }

    internal static object? GetFieldValue(this object obj, string fieldName)
    {
        return obj.GetType().GetFieldValue(obj, fieldName);
    }

    internal static PropertyInfo? GetShadowingProperty(this Type type, string name)
    {
        return type.GetShadowingProperty(name, _bindingFlags);
    }

    internal static PropertyInfo? GetShadowingProperty(this object obj, string name)
    {
        return obj.GetType().GetShadowingProperty(name, _bindingFlags);
    }

    internal static object? GetPropertyValue(this Type type, object obj, string propertyName)
    {
        var property = type.GetShadowingProperty(propertyName);
        if (property != null)
        {
            return property.GetReflector().GetValue(obj);
        }
        else
        {
            return null;
        }
    }

    internal static object? GetPropertyValue(this object obj, string propertyName)
    {
        return obj.GetType().GetPropertyValue(obj, propertyName);
    }

    internal static bool ContainsProperty(this Type type, string propertyName)
    {
        var property = type.GetShadowingProperty(propertyName);
        return property != null;
    }

    internal static void SetPropertyValue<T>(this T t, string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(t);

        var type = t.GetType();
        var p = type.GetShadowingProperty(name);
        if (p == null)
        {
            throw new ShardingNotFoundException($"type:{typeof(T)} not found [{name}] properity ");
        }

        //获取设置属性的值的方法
        var setMethod = p.GetSetMethod(true);

        //如果只是只读,则setMethod==null
        if (setMethod != null)
        {
            var param_obj = Expression.Parameter(type);
            var param_val = Expression.Parameter(typeof(object));
            var body_obj = Expression.Convert(param_obj, type);
            var body_val = Expression.Convert(param_val, p.PropertyType);
            var body = Expression.Call(param_obj, setMethod, body_val);
            var setValue = Expression.Lambda<Action<T, object?>>(body, param_obj, param_val).Compile();
            setValue(t, value);
        }
        else
        {
            var backingField = t.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Find(o => o.Name == $"<{name}>i__Field")
                               ?? throw new ShardingNotFoundException($"type:{typeof(T)} not found [{name}] backing field");
            backingField.SetValue(t, value);
        }
    }

    internal static (Type propertyType, object? value) GetPropertyType(this object obj, string expr)
    {
        var entityType = obj.GetType();
        PropertyInfo? property;

        if (expr.Contains('.'))
        {
            var childProperties = expr.Split('.');
            property = entityType.GetShadowingProperty(childProperties[0]);

            for (var i = 1; i < childProperties.Length; i++)
            {
                if (property == null)
                {
                    throw new ShardingException($"property:[{expr}] not in type:[{entityType}]");
                }

                property = property.PropertyType.GetShadowingProperty(childProperties[i]);
            }
        }
        else
        {
            property = entityType.GetShadowingProperty(expr);
        }

        if (property == null)
        {
            throw new ShardingException($"property:[{expr}] not in type:[{entityType}]");
        }

        return (property.PropertyType, property.GetValue(obj));
    }

    private static MethodCallExpression CreateSumExpression(this IQueryable source, PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(property);

        if (!property.PropertyType.IsNumeric())
        {
            throw new ShardingInvalidOperationException(
                $"method sum cant calc type :[{property.PropertyType}]");
        }

        var parameter = Expression.Parameter(source.ElementType, "s");
        var getter = Expression.MakeMemberAccess(parameter, property);
        var selector = Expression.Lambda(getter, parameter);

        var sumMethod = typeof(Queryable).GetMethods().Find(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == property.PropertyType && m.IsGenericMethod)
                        ?? throw new ShardingInvalidOperationException($"method sum not found for type :[{property.PropertyType}]");
        var genericSumMethod = sumMethod.MakeGenericMethod([source.ElementType]);

        var callExpression = Expression.Call(null, genericSumMethod, [source.Expression, Expression.Quote(selector)]);
        return callExpression;
    }

    private static object? CreateExpression(this IQueryable source, PropertyInfo property, string methodName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(property);

        var parameter = Expression.Parameter(source.ElementType, "s");
        var getter = Expression.MakeMemberAccess(parameter, property);
        var selector = Expression.Lambda(getter, parameter);

        var maxMethod = typeof(Queryable).GetMethods().Find(m => m.Name == methodName && m.GetParameters().Length == 2 && typeof(Expression).IsAssignableFrom(m.GetParameters()[1].ParameterType) && m.IsGenericMethod)
                        ?? throw new ShardingInvalidOperationException($"method {methodName} not found for type :[{property.PropertyType}]");
        var genericMaxMethod = maxMethod.MakeGenericMethod([source.ElementType, selector.Body.Type]);

        var callExpression = Expression.Call(null, genericMaxMethod, [source.Expression, Expression.Quote(selector)]);

        return source.Provider.Execute(callExpression);
    }

    internal static object? SumBy(this IQueryable source, PropertyInfo property)
    {
        var callExpression = CreateSumExpression(source, property);
        return source.Provider.Execute(callExpression);
    }

    internal static T SumBy<T>(this IQueryable source, PropertyInfo property)
    {
        var callExpression = CreateSumExpression(source, property);
        return source.Provider.Execute<T>(callExpression);
    }

    internal static object? SumBy(this IQueryable source, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(propertyName);

        var property = source.ElementType.GetShadowingProperty(propertyName)
                       ?? throw new ShardingNotFoundException($"type:{source.ElementType} not found [{propertyName}] property");
        return source.SumBy(property);
    }

    internal static T SumBy<T>(this IQueryable source, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(propertyName);

        var property = source.ElementType.GetShadowingProperty(propertyName)
                       ?? throw new ShardingNotFoundException($"type:{source.ElementType} not found [{propertyName}] property");
        return source.SumBy<T>(property);
    }

    [ExcludeFromCodeCoverage]
    internal static object? MaxBy(this IQueryable source, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(propertyName);

        var property = source.ElementType.GetShadowingProperty(propertyName)
                       ?? throw new ShardingNotFoundException($"type:{source.ElementType} not found [{propertyName}] property");

        return source.MaxBy(property);
    }

    internal static object? MaxBy(this IQueryable source, PropertyInfo property)
    {
        return source.CreateExpression(property, nameof(Queryable.Max));
    }

    [ExcludeFromCodeCoverage]
    internal static object? MinBy(this IQueryable source, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(propertyName);

        var property = source.ElementType.GetShadowingProperty(propertyName)
                       ?? throw new ShardingNotFoundException($"type:{source.ElementType} not found [{propertyName}] property");

        return source.MinBy(property);
    }

    internal static object? MinBy(this IQueryable source, PropertyInfo property)
    {
        return source.CreateExpression(property, nameof(Queryable.Min));
    }

    internal static object? AverageConstant(this object? sum, object? count, Type resultType)
    {
        ArgumentNullException.ThrowIfNull(sum);
        ArgumentNullException.ThrowIfNull(count);

        Expression constantSum = Expression.Constant(sum);
        //如果计算类型和返回类型不一致先转成一致
        if (sum.GetType() != resultType)
        {
            constantSum = Expression.Convert(constantSum, resultType);
        }

        Expression constantCount = Expression.Constant(count);
        //如果计算类型和返回类型不一致先转成一致
        if (count.GetType() != resultType)
        {
            constantCount = Expression.Convert(constantCount, resultType);
        }

        var binaryExpression = Expression.Divide(constantSum, constantCount);
        return Expression.Lambda(binaryExpression).Compile().DynamicInvoke();
    }

    internal static TResult AverageConstant<TSum, TCount, TResult>(this TSum sum, TCount count)
    {
        var resultType = typeof(TResult);
        var sumType = (sum as object)?.GetType() ?? typeof(TSum);
        var countType = (count as object)?.GetType() ?? typeof(TCount);

        Expression constantSum = Expression.Constant(sum, typeof(TSum));
        //如果计算类型和返回类型不一致先转成一致
        if (sumType != resultType)
        {
            constantSum = Expression.Convert(constantSum, resultType);
        }

        Expression constantCount = Expression.Constant(count, typeof(TCount));
        //如果计算类型和返回类型不一致先转成一致
        if (countType != resultType)
        {
            constantCount = Expression.Convert(constantCount, resultType);
        }

        var binaryExpression = Expression.Divide(constantSum, constantCount);
        var invoke = Expression.Lambda<Func<TResult>>(binaryExpression).Compile()();
        return invoke;
    }

    private static object? AverageSum(this IQueryable source, PropertyInfo averageProperty, PropertyInfo countProperty)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(averageProperty);
        ArgumentNullException.ThrowIfNull(countProperty);

        //o=>
        var parameter = Expression.Parameter(source.ElementType, "s");
        //o.avg
        var averageMember = Expression.MakeMemberAccess(parameter, averageProperty);
        //o.count
        var countMember = Expression.MakeMemberAccess(parameter, countProperty);
        //Convert(o.count,o.avg.GetType()) 必须要同类型才能计算
        var countConvertExpression = Expression.Convert(countMember, averageProperty.PropertyType);
        //o.avg*Convert(o.count,o.avg.GetType())
        var multiply = Expression.Multiply(averageMember, countConvertExpression);

        //o=>o.avg*Convert(o.count,o.avg.GetType())
        Expression selector = Expression.Lambda(multiply, parameter);
        var sumMethod = typeof(Queryable).GetMethods().Find(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == averageProperty.PropertyType && m.IsGenericMethod)
                        ?? throw new ShardingInvalidOperationException($"method sum not found for type :[{averageProperty.PropertyType}]");
        var genericSumMethod = sumMethod.MakeGenericMethod([source.ElementType]);

        var callExpression = Expression.Call(null, genericSumMethod, [source.Expression, Expression.Quote(selector)]);

        return source.Provider.Execute(callExpression);
    }

    internal static object? AverageSum(this IQueryable source, PropertyInfo averageProperty, PropertyInfo sumProperty, Type resultType)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(averageProperty);
        ArgumentNullException.ThrowIfNull(sumProperty);

        var count = source.AverageCount(averageProperty, sumProperty);
        var sum = source.SumBy(sumProperty);
        return AverageConstant(sum, count, resultType);
    }

    private static object? AverageCount(this IQueryable source, PropertyInfo averageProperty, PropertyInfo sumProperty)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(averageProperty);
        ArgumentNullException.ThrowIfNull(sumProperty);

        //o=>
        var parameter = Expression.Parameter(source.ElementType, "s");
        //o.avg
        var averageMember = Expression.MakeMemberAccess(parameter, averageProperty);
        //o.sum
        var sumMember = Expression.MakeMemberAccess(parameter, sumProperty);
        //Convert(o.sum,o.avg.GetType()) 必须要同类型才能计算
        var sumConvertExpression = Expression.Convert(sumMember, averageProperty.PropertyType);
        //Convert(o.sum,o.avg.GetType())/o.avg
        var divide = Expression.Divide(sumConvertExpression, averageMember);

        //o=>Convert(o.sum,o.avg.GetType())/o.avg
        Expression selector = Expression.Lambda(divide, parameter);
        var sumMethod = typeof(Queryable).GetMethods().Find(m => m.Name == nameof(Queryable.Sum) && m.ReturnType == averageProperty.PropertyType && m.IsGenericMethod)
                        ?? throw new ShardingInvalidOperationException($"method sum not found for type :[{averageProperty.PropertyType}]");
        var genericSumMethod = sumMethod.MakeGenericMethod([source.ElementType]);

        var callExpression = Expression.Call(null, genericSumMethod, [source.Expression, Expression.Quote(selector)]);

        return source.Provider.Execute(callExpression);
    }

    internal static object? AverageCount(this IQueryable source, PropertyInfo averageProperty, PropertyInfo countProperty, Type resultType)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(averageProperty);
        ArgumentNullException.ThrowIfNull(countProperty);

        //获取sum
        var sum = source.AverageSum(averageProperty, countProperty);
        var count = source.SumBy(countProperty);
        return sum.AverageConstant(count, resultType);
    }

    internal static IQueryable<Tuple<long, T>> BuildExpression<T>(this IQueryable<IGrouping<int, T>> queryable)
    {
        var sourceParameter = Expression.Parameter(typeof(IQueryable<IGrouping<int, T>>));
        var selectCall = BuildSelect<T>(sourceParameter);
        var lambda = Expression.Lambda<Func<IQueryable<IGrouping<int, T>>, IQueryable<Tuple<long, T>>>>(selectCall, sourceParameter);
        var compile = lambda.Compile();
        return compile(queryable);
    }

    private static MethodCallExpression BuildSelect<T>(this ParameterExpression sourceParameter)
    {
        var groupingType = typeof(IGrouping<int, T>);
        var selectMethod = ShardingQueryableMethods.SelectMethod.MakeGenericMethod(groupingType, typeof(Tuple<long, T>));
        var resultParameter = Expression.Parameter(groupingType);

        var longCountCall = BuildLongCount<T>(resultParameter);
        var sumCall = BuildSum<T>(resultParameter);
        var resultSelector = Expression.New(typeof(Tuple<long, T>).GetConstructors()[0], longCountCall, sumCall);
        //queryable.Expression,
        return Expression.Call(selectMethod, sourceParameter, Expression.Lambda(resultSelector, resultParameter));
    }

    private static MethodCallExpression BuildLongCount<T>(ParameterExpression resultParameter)
    {
        var asQueryableMethod = ShardingQueryableMethods.AsQueryable.MakeGenericMethod(typeof(T));
        var longCountMethod = ShardingQueryableMethods.LongCounMethod.MakeGenericMethod(typeof(T));

        return Expression.Call(longCountMethod, Expression.Call(asQueryableMethod, resultParameter));
    }
    private static MethodCallExpression BuildSum<T>(ParameterExpression resultParameter)
    {
        var asQueryableMethod = ShardingQueryableMethods.AsQueryable.MakeGenericMethod(typeof(T));
        var sumMethod = ShardingQueryableMethods.GetSumMethod(typeof(T));

        return Expression.Call(sumMethod, Expression.Call(asQueryableMethod, resultParameter));
    }
    #endregion

    #region IQueryable
    internal static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source, int? take = null, CancellationToken cancellationToken = default)
    {
        var list = new List<T>(take ?? 4);
        await foreach (var element in source.WithCancellation(cancellationToken))
        {
            list.Add(element);
        }

        return list;
    }

    internal static IOrderedEnumerable<T> ThenByIf<T, TKey>(this IOrderedEnumerable<T> source, Func<T, TKey> keySelector, bool condition, IComparer<TKey> comparer)
    {
        return condition ? source.ThenBy(keySelector, comparer) : source;
    }

    internal static IOrderedEnumerable<T> ThenByDescendingIf<T, TKey>(this IOrderedEnumerable<T> source, Func<T, TKey> keySelector, bool condition, IComparer<TKey> comparer)
    {
        return condition ? source.ThenByDescending(keySelector, comparer) : source;
    }

    internal static IQueryable<T> ReplaceDbContextQueryable<T>(this IQueryable<T> source, DbContext ctx)
    {
        var replaceQueryableVisitor = new DbContextReplaceQueryableVisitor(ctx);
        var newExpression = replaceQueryableVisitor.Visit(source.Expression);
        return (IQueryable<T>)replaceQueryableVisitor.Source.Provider.CreateQuery(newExpression);
    }

    internal static IQueryable ReplaceDbContextQueryable(this IQueryable source, DbContext ctx)
    {
        var replaceQueryableVisitor = new DbContextReplaceQueryableVisitor(ctx);
        var newExpression = replaceQueryableVisitor.Visit(source.Expression);
        return replaceQueryableVisitor.Source.Provider.CreateQuery(newExpression);
    }

    internal static bool IsMemberQueryable(this MemberExpression memberExpression)
    {
        ArgumentNullException.ThrowIfNull(memberExpression);

        return (memberExpression.Type.FullName?.StartsWith("System.Linq.IQueryable`1") ?? false) || typeof(IQueryable).IsAssignableFrom(memberExpression.Type) || typeof(DbContext).IsAssignableFrom(memberExpression.Type);
    }

    internal static bool IsQueryable(this Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return typeof(IQueryable).IsAssignableFrom(type);
    }

    internal static IEnumerable<object?> GetPrimaryKeys<T>(T entity, IKey primaryKey)
        where T : class
    {
        return primaryKey.Properties.Select(o => entity.GetPropertyValue(o.Name));
    }

    internal static T? GetAttachedEntity<T>(this DbContext ctx, T entity)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(entity);

        var entityPrimaryKey = ctx.Model.FindRuntimeEntityType(entity.GetType())?.FindPrimaryKey();

        if (entityPrimaryKey == null)
        {
            return entity;
        }

        var keys = GetPrimaryKeys(entity, entityPrimaryKey).ToArray();
        if (keys.IsNull())
        {
            return null;
        }

        var dependencies = (IDbContextDependencies)(typeof(DbContext).GetPropertyValue(ctx, "DbContextDependencies")
                            ?? throw new ShardingInvalidOperationException("cant resolve DbContextDependencies"));
        var stateManager = dependencies.StateManager;

        //var entityIKey = ShardingKeyUtil.GetEntityIKey(entity);
        var internalEntityEntry = stateManager.TryGetEntry(entityPrimaryKey, keys);

        if (internalEntityEntry == null)
        {
            return null;
        }

        return (T)internalEntityEntry.Entity;
    }

    internal static bool? GetIsNoTracking(this Expression expression)
    {
        var visitor = new TrackingDiscoveryVisitor();
        visitor.Visit(expression);
        return visitor.IsNoTracking;
    }

    internal static ShardingRouteExpression GetRouteExpression(this IQueryable queryable, EntityMetadata entityMetadata, Func<object, ShardingOperator, string?, Func<string, bool>> keyToTailExpression, Func<object, string?, object> compareValue, bool tableRoute)
    {
        var visitor = new TableRouteDiscoveryVisitor(entityMetadata, keyToTailExpression, compareValue, tableRoute);

        visitor.Visit(queryable.Expression);

        return visitor.GetRouteExpression();
    }
    #endregion

    #region Visitor
    internal static IQueryable RemoveVisitor(this IQueryable source, params string[] names)
    {
        var expression = new RemoveExpressionVisitor(names).Visit(source.Expression);
        return source.Provider.CreateQuery(expression);
    }

    internal static IQueryable RemoveAnyOrderBy(this IQueryable source)
    {
        return source.RemoveVisitor(nameof(Queryable.OrderBy), nameof(Queryable.OrderByDescending), nameof(Queryable.ThenBy), nameof(Queryable.ThenByDescending));
    }

    private static readonly MethodInfo _skipMethod = typeof(Queryable).GetMethod(nameof(Queryable.Skip))
                                                     ?? throw new InvalidOperationException("Queryable.Skip method not found");
    private static readonly MethodInfo _takeMethod = typeof(Queryable).GetMethods().Find(m => m.Name == nameof(Queryable.Take) && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(int))
                                                     ?? throw new InvalidOperationException("Queryable.Take method not found");

    internal static IQueryable ReSkip(this IQueryable source, int skip)
    {
        var method = _skipMethod.MakeGenericMethod(source.ElementType);
        var expression = Expression.Call(method, source.Expression, Expression.Constant(skip));
        return source.Provider.CreateQuery(expression);
    }

    internal static IQueryable ReTake(this IQueryable source, int take)
    {
        var method = _takeMethod.MakeGenericMethod(source.ElementType);
        var expression = Expression.Call(method, source.Expression, Expression.Constant(take));
        return source.Provider.CreateQuery(expression);
    }
    #endregion

    #region Expression
    internal static Type GetEntityType(this MethodCallExpression expression)
    {
        var rootQuery = expression.Arguments.FirstOrDefault(o => typeof(IQueryable).IsAssignableFrom(o.Type));
        if (rootQuery == null)
        {
            throw new ShardingException("expression error");
        }

        var type = rootQuery.Type;

        return type.TryGetElementType(typeof(IEnumerable<>))
               ?? type.TryGetElementType(typeof(IAsyncEnumerable<>))
               ?? throw new ShardingException("expression error");
    }

    public static Type GetResultType(this MethodCallExpression expression)
    {
        if (expression.Arguments.Count == 1)
        {
            return expression.GetEntityType();
        }

        var otherExpression = expression.Arguments.FirstOrDefault(o => !typeof(IQueryable).IsAssignableFrom(o.Type));
        if (otherExpression is UnaryExpression unaryExpression && unaryExpression.Operand is LambdaExpression lambdaExpression)
        {
            return lambdaExpression.ReturnType;
        }

        throw new ShardingException("expression error");
    }

    private static LambdaExpression GenerateSelector(Type entityType, string propertyName, out Type resultType)
    {
        PropertyInfo? property;
        Expression propertyAccess;
        var parameter = Expression.Parameter(entityType, "o");

        if (propertyName.Contains('.'))
        {
            var childProperties = propertyName.Split('.');
            property = entityType.GetShadowingProperty(childProperties[0])
                       ?? throw new ShardingException($"property:[{propertyName}] not in type:[{entityType}]");
            propertyAccess = Expression.MakeMemberAccess(parameter, property);
            for (var i = 1; i < childProperties.Length; i++)
            {
                property = property.PropertyType.GetShadowingProperty(childProperties[i])
                           ?? throw new ShardingException($"property:[{propertyName}] not in type:[{entityType}]");
                propertyAccess = Expression.MakeMemberAccess(propertyAccess, property);
            }
        }
        else
        {
            property = entityType.GetShadowingProperty(propertyName)
                       ?? throw new ShardingException($"property:[{propertyName}] not in type:[{entityType}]");
            propertyAccess = Expression.MakeMemberAccess(parameter, property);
        }

        resultType = property.PropertyType;

        return Expression.Lambda(propertyAccess, parameter);
    }

    private static MethodCallExpression GenerateMethodCall(IQueryable source, string methodName, string fieldName, IShardingComparer? shardingComparer = null)
    {
        var type = source.ElementType;
        var selector = GenerateSelector(type, fieldName, out var selectorResultType);

        MethodCallExpression resultExp;
        if (shardingComparer == null)
        {
            resultExp = Expression.Call(typeof(Queryable), methodName, [type, selectorResultType], source.Expression, Expression.Quote(selector));
        }
        else
        {
            var comparer = shardingComparer.CreateComparer(selectorResultType);
            resultExp = Expression.Call(typeof(Queryable), methodName, [type, selectorResultType], source.Expression, Expression.Quote(selector), Expression.Constant(comparer));
        }

        return resultExp;
    }

    internal static IOrderedQueryable OrderBy(this IQueryable source, string fieldName, IShardingComparer? shardingComparer = null)
    {
        var resultExp = GenerateMethodCall(source, nameof(Queryable.OrderBy), fieldName, shardingComparer);
        return (IOrderedQueryable)source.Provider.CreateQuery(resultExp);
    }

    internal static IOrderedQueryable OrderByDescending(this IQueryable source, string fieldName, IShardingComparer? shardingComparer = null)
    {
        var resultExp = GenerateMethodCall(source, nameof(Queryable.OrderByDescending), fieldName, shardingComparer);
        return (IOrderedQueryable)source.Provider.CreateQuery(resultExp);
    }

    internal static IOrderedQueryable ThenBy(this IOrderedQueryable source, string fieldName, IShardingComparer? shardingComparer = null)
    {
        var resultExp = GenerateMethodCall(source, nameof(Queryable.ThenBy), fieldName, shardingComparer);
        return (IOrderedQueryable)source.Provider.CreateQuery(resultExp);
    }

    internal static IOrderedQueryable ThenByDescending(this IOrderedQueryable source, string fieldName, IShardingComparer? shardingComparer = null)
    {
        var resultExp = GenerateMethodCall(source, nameof(Queryable.ThenByDescending), fieldName, shardingComparer);
        return (IOrderedQueryable)source.Provider.CreateQuery(resultExp);
    }

    /// <summary>
    /// 排序利用表达式
    /// </summary>
    /// <param name="source"></param>
    /// <param name="sortExpression">"child.name asc,child.age desc"</param>
    /// <param name="shardingComparer"></param>
    /// <returns></returns>
    internal static IOrderedQueryable WithSort(this IQueryable source, string sortExpression, IShardingComparer? shardingComparer = null)
    {
        var orderFields = sortExpression.Split(',');
        IOrderedQueryable? result = null;

        for (var index = 0; index < orderFields.Length; index++)
        {
            var parts = orderFields[index].Trim().Split(' ');
            var sortField = parts[0];
            var sortDescending = parts.Length == 2 && parts[1].EqualsTo("DESC");

            if (sortDescending)
            {
                result = index == 0 ? source.OrderByDescending(sortField, shardingComparer) : result!.ThenByDescending(sortField, shardingComparer);
            }
            else
            {
                result = index == 0 ? source.OrderBy(sortField, shardingComparer) : result!.ThenBy(sortField, shardingComparer);
            }
        }

        return result ?? throw new ArgumentException("sortExpression is empty", nameof(sortExpression));
    }

    internal static IOrderedQueryable<T> WithSort<T>(this IQueryable<T> source, IEnumerable<PropertySorting> sorts, IShardingComparer? shardingComparer = null)
    {
        return (IOrderedQueryable<T>)WithSort((IQueryable)source, sorts, shardingComparer);
    }

    internal static IOrderedQueryable WithSort(this IQueryable source, IEnumerable<PropertySorting> sorts, IShardingComparer? shardingComparer = null)
    {
        IOrderedQueryable? result = null;
        var currentIndex = 0;
        foreach (var sort in sorts)
        {
            var sortField = sort.Expression;
            if (sort.Direction == SortDirection.Ascending)
            {
                result = currentIndex == 0 ? source.OrderBy(sortField, shardingComparer) : result!.ThenBy(sortField, shardingComparer);
            }
            else
            {
                result = currentIndex == 0 ? source.OrderByDescending(sortField, shardingComparer) : result!.ThenByDescending(sortField, shardingComparer);
            }

            currentIndex++;
        }

        return result ?? throw new ArgumentException("sorts is empty", nameof(sorts));
    }

    internal static bool IsStringMethod(this MethodCallExpression express, string name)
    {
        var methodName = express.Method.Name;
        return methodName == name && express.Method.DeclaringType == typeof(string);
    }

    internal static bool IsEnumerableMethod(this MethodCallExpression express, string name)
    {
        var methodName = express.Method.Name;
        return methodName == name && (express.Method.DeclaringType?.Namespace.In("System.Linq", "System.Collections.Generic") ?? false);
    }

    internal static bool IsNamedComparison(this MethodCallExpression express)
    {
        return express.Method.Name.In(nameof(string.Compare), nameof(string.CompareTo));
    }

    internal static bool IsNamedComparison(this BinaryExpression express, [NotNullWhen(true)] out MethodCallExpression? methodCallExpression)
    {
        if (express.Left is MethodCallExpression m1 && m1.IsNamedComparison())
        {
            methodCallExpression = m1;
            return true;
        }

        if (express.Right is MethodCallExpression m2 && m2.IsNamedComparison())
        {
            methodCallExpression = m2;
            return true;
        }

        methodCallExpression = null;
        return false;
    }

    internal static bool GetComparison(this MethodCallExpression express, out (Expression? Left, Expression? Right) comparisonValue)
    {

        if (express.IsNamedComparison())
        {
            if (express.Arguments.Count == 2)
            {
                comparisonValue = (express.Arguments[0], express.Arguments[1]);
                return true;
            }
            else if (express.Arguments.Count == 1 && express.Object != null)
            {
                comparisonValue = (express.Object, express.Arguments[0]);
                return true;
            }
        }

        comparisonValue = (null, null);
        return false;
    }
    #endregion

    #region IShardingDbContext
    internal static bool IsShellDbContext(this DbContext ctx)
    {
        return ctx.GetService<IDbContextOptions>().FindExtension<ShardingWrapOptionsExtension>() != null;
    }

    internal static DbContext GetWriteDbContext(this IShardingDbContext ctx, string dataSource, IRouteTail routeTail)
    {
        return ctx.GetExecutor().Create(CreateDbStrategy.ParallelWrite, dataSource, routeTail);
    }

    internal static IShardingRuntimeContext GetRuntimeContext(this DbContext ctx)
    {
        var shardingRuntimeContext = ctx.GetService<IShardingRuntimeContext>();

        if (shardingRuntimeContext == null)
        {
            throw new ShardingInvalidOperationException($"cant resolve:[{typeof(IShardingRuntimeContext)}],context:[{ctx}]");
        }

        return shardingRuntimeContext;
    }

    internal static bool IsShardingTableDbContext(this Type dbType)
    {
        ArgumentNullException.ThrowIfNull(dbType);

        if (!typeof(DbContext).IsAssignableFrom(dbType))
        {
            return false;
        }

        return typeof(IShardingTableDbContext).IsAssignableFrom(dbType);
    }

    /// <summary>
    /// 移除所有的分表关系的模型
    /// </summary>
    /// <param name="db"></param>
    internal static void RemoveShardingTable(this DbContext db)
    {
        var model = db.GetService<IDesignTimeModel>().Model;
        var context = db.GetRuntimeContext();
        var entityMetadataManager = context.EntityMetadataManager;

        var entityTypes = model.GetEntityTypes();
        foreach (var entityType in entityTypes)
        {
            if (entityType.GetFieldValue("_data") is List<object> data)
            {
                data.Clear();
            }
        }

        var relationalModel = model.GetRelationalModel() as RelationalModel
                              ?? throw new ShardingInvalidOperationException("cant resolve RelationalModel");
        var valueTuples = relationalModel.Tables.Where(o => o.Value.EntityTypeMappings.Any(m => entityMetadataManager.IsShardingTable(m.TypeBase.ClrType))).Select(o => o.Key).ToList();

        for (var i = 0; i < valueTuples.Count; i++)
        {
            relationalModel.Tables.Remove(valueTuples[i]);
        }
    }

    /// <summary>
    /// 移除所有除了仅分库的
    /// </summary>
    /// <param name="db"></param>
    internal static void RemoveShardingOnlyDataSource(this DbContext db)
    {
        var model = db.GetService<IDesignTimeModel>().Model;
        var context = db.GetRuntimeContext();
        var entityMetadataManager = context.EntityMetadataManager;

        var entityTypes = model.GetEntityTypes();
        foreach (var entityType in entityTypes)
        {
            if (entityType.GetFieldValue("_data") is List<object> data)
            {
                data.Clear();
            }
        }

        var relationalModel = model.GetRelationalModel() as RelationalModel
                              ?? throw new ShardingInvalidOperationException("cant resolve RelationalModel");
        var valueTuples = relationalModel.Tables.Where(o => o.Value.EntityTypeMappings.Any(m => entityMetadataManager.IsShardingOnlyDataSource(m.TypeBase.ClrType))).Select(o => o.Key).ToList();

        for (var i = 0; i < valueTuples.Count; i++)
        {
            relationalModel.Tables.Remove(valueTuples[i]);
        }
    }

    /// <summary>
    /// 移除所有的除了我指定的那个类型
    /// </summary>
    /// <param name="db"></param>
    /// <param name="shardingType"></param>
    internal static void RemoveShardingModel(this DbContext db, Type shardingType)
    {
        var contextModel = db.GetService<IDesignTimeModel>().Model;
        var entityTypes = contextModel.GetEntityTypes();
        foreach (var entityType in entityTypes)
        {
            if (entityType.GetFieldValue("_data") is List<object> data)
            {
                data.Clear();
            }
        }

        var model = contextModel.GetRelationalModel() as RelationalModel
                    ?? throw new ShardingInvalidOperationException("cant resolve RelationalModel");
        var values = model.Tables.Where(o => o.Value.EntityTypeMappings.All(m => m.TypeBase.ClrType != shardingType)).Select(o => o.Key).ToList();
        for (var i = 0; i < values.Count; i++)
        {
            model.Tables.Remove(values[i]);
        }
    }

    internal static bool TryGetAssertDataSource<T>(this ShardingRouteContext? routeContext, [NotNullWhen(true)] out IEnumerable<IDataSourceRouteAssert>? dataSources)
        where T : class
    {
        if (routeContext == null)
        {
            dataSources = null;
            return false;
        }

        var entityType = typeof(T);
        if (!routeContext.AssertDataSource.TryGetValue(entityType, out var value))
        {
            dataSources = null;
            return false;
        }

        dataSources = value;
        return true;
    }

    internal static bool TryGetAssertTail<T>(this ShardingRouteContext? routeContext, [NotNullWhen(true)] out IEnumerable<ITableRouteAssert>? tail)
    where T : class
    {
        if (routeContext == null)
        {
            tail = null;
            return false;
        }

        var entityType = typeof(T);
        if (!routeContext.AssertTable.TryGetValue(entityType, out var value))
        {
            tail = null;
            return false;
        }

        tail = value;
        return true;
    }

    internal static bool TryGetMustDataSource<T>(this ShardingRouteContext? routeContext, [NotNullWhen(true)] out HashSet<string>? dataSources)
        where T : class
    {
        if (routeContext == null)
        {
            dataSources = null;
            return false;
        }

        var entityType = typeof(T);
        if (!routeContext.MustDataSource.TryGetValue(entityType, out var value))
        {
            dataSources = null;
            return false;
        }

        dataSources = value;
        return true;
    }

    internal static bool TryGetHintDataSource<T>(this ShardingRouteContext? routeContext, [NotNullWhen(true)] out HashSet<string>? dataSources)
        where T : class
    {
        if (routeContext == null)
        {
            dataSources = null;
            return false;
        }

        var entityType = typeof(T);
        if (!routeContext.HintDataSource.TryGetValue(entityType, out var value))
        {
            dataSources = null;
            return false;
        }

        dataSources = value;
        return true;
    }

    internal static bool TryGetMustTail<T>(this ShardingRouteContext? routeContext, [NotNullWhen(true)] out HashSet<string>? tail)
    {
        if (routeContext == null)
        {
            tail = null;
            return false;
        }

        var entityType = typeof(T);
        if (!routeContext.MustTable.TryGetValue(entityType, out var value))
        {
            tail = null;
            return false;
        }

        tail = value;
        return true;
    }

    internal static bool TryGetHintTail<T>(this ShardingRouteContext? routeContext, [NotNullWhen(true)] out HashSet<string>? tail)
    {
        if (routeContext == null)
        {
            tail = null;
            return false;
        }

        var entityType = typeof(T);
        if (!routeContext.HintTable.TryGetValue(entityType, out var value))
        {
            tail = null;
            return false;
        }

        tail = value;
        return true;
    }

    internal static DbContextOptions CreateShellDbContextOptions(this IShardingRuntimeContext context, string dataSource)
    {
        var builder = context.DbContextOptionsBuilderCreator.Create(shellDbContext: null);
        var connectionString = context.VirtualDataSource.GetConnectionString(dataSource);
        context.VirtualDataSource.UseDbContextOptionsBuilder(connectionString, builder);
        context.Options.MigrationFactory?.Invoke(builder);
        //迁移
        builder.UseShardingOptions(context);
        return builder.Options;
    }
    #endregion
}
