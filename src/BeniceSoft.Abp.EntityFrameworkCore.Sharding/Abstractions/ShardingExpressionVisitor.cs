using BeniceSoft.Core;
using BeniceSoft.Core.Strategy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal class ShardingExpressionVisitor : ExpressionVisitor
{
    protected object? GetExpressionValue(Expression? expression)
    {
        if (expression == null)
        {
            return null;
        }

        switch (expression)
        {
            case ConstantExpression e:
                {
                    return e.Value;
                }

            case NewExpression e:
                {
                    return e.Constructor?.Invoke(e.Arguments.Select(GetExpressionValue).ToArray());
                }

            case MemberExpression e when e.Member is FieldInfo field:
                {
                    return field.GetValue(GetExpressionValue(e.Expression) ?? throw new InvalidOperationException($"cant get expression value,{e.Expression?.Print()} may be null reference"));
                }

            case MemberExpression e when e.Member is PropertyInfo property:
                {
                    if (e.Expression == null)
                    {
                        if (property.DeclaringType == typeof(DateTime) && property.Name == nameof(DateTime.Now))
                        {
                            return DateTime.Now;
                        }

                        if (property.DeclaringType == typeof(DateTimeOffset) &&
                            property.Name == nameof(DateTimeOffset.Now))
                        {
                            return DateTimeOffset.Now;
                        }
                    }

                    return property.GetValue(GetExpressionValue(e.Expression) ?? throw new InvalidOperationException($"cant get expression value,{e.Expression?.Print()} may be null reference"));
                }

            case ListInitExpression e when e.NewExpression.Arguments.Count == 0:
                {
                    var collection = e.NewExpression.Constructor?.Invoke([])
                                     ?? throw new InvalidOperationException("ListInitExpression constructor is null");

                    foreach (var i in e.Initializers)
                    {
                        i.AddMethod.Invoke(collection, i.Arguments.Select(GetExpressionValue).ToArray());
                    }

                    return collection;
                }

            case NewArrayExpression e when e.NodeType == ExpressionType.NewArrayInit && e.Expressions.Count > 0:
                {
                    var collection = new List<object?>(e.Expressions.Count);
                    foreach (var arrayItemExpression in e.Expressions)
                    {
                        collection.Add(GetExpressionValue(arrayItemExpression));
                    }

                    return collection;
                }

            case MethodCallExpression e:
                {
                    var expressionValue = GetExpressionValue(e.Object);

                    return e.Method.Invoke(expressionValue, e.Arguments.Select(GetExpressionValue).ToArray());
                }

            case UnaryExpression e when e.NodeType == ExpressionType.Convert:
                {
                    return GetExpressionValue(e.Operand);
                }

            default:
                {
                    if (expression is BinaryExpression binaryExpression &&
                        expression.NodeType == ExpressionType.ArrayIndex)
                    {
                        var index = GetExpressionValue(binaryExpression.Right);
                        if (index is int i)
                        {
                            var arrayObject = GetExpressionValue(binaryExpression.Left);
                            if (arrayObject is IList list)
                            {
                                return list[i];
                            }
                        }
                    }

                    throw new ShardingException("cant get value " + expression);
                }
        }
    }
}

internal class ReplaceQueryableVisitor(DbContext ctx) : ShardingExpressionVisitor
{
    protected DbContext DbContext { get; } = ctx;

    protected bool VisitedRoot { get; set; }

    protected override Expression VisitMember(MemberExpression memberExpression)
    {
        // Recurse down to see if we can simplify...
        if (memberExpression.IsMemberQueryable()) //2x,3x 路由 单元测试 分表和不分表
        {
            var expressionValue = GetExpressionValue(memberExpression);
            if (expressionValue is IQueryable queryable)
            {
                return ReplaceMemberExpression(queryable);
            }

            if (expressionValue is DbContext ctx)
            {
                return ReplaceMemberExpression(ctx);
            }
        }

        return base.VisitMember(memberExpression);
    }

    private MemberExpression ReplaceMemberExpression(IQueryable queryable)
    {
        var visitor = new DbContextReplaceQueryableVisitor(DbContext);
        var newExpression = visitor.Visit(queryable.Expression);
        var newQueryable = visitor.Source.Provider.CreateQuery(newExpression);

        var tempType = typeof(TempVariable<>).MakeGenericType(queryable.ElementType);
        var tempVariable = Activator.CreateInstance(tempType, newQueryable);

        var expr = Expression.Property(ConstantExpression.Constant(tempVariable), nameof(TempVariable<object>.Queryable));
        return expr;
    }

    private MemberExpression ReplaceMemberExpression(DbContext ctx)
    {
        var tempVariableGenericType = typeof(TempDbVariable<>).MakeGenericType(ctx.GetType());
        var tempVariable = Activator.CreateInstance(tempVariableGenericType, DbContext);
        var expr = Expression.Property(ConstantExpression.Constant(tempVariable), nameof(TempDbVariable<object>.DbContext));
        return expr;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (VisitedRoot && node.Method.ReturnType.IsQueryable() && node.Method.ReturnType.IsGenericType)
        {
            var notRoot = node.Arguments.IsNull();

            if (notRoot)
            {
                var entityType = node.Method.ReturnType.GenericTypeArguments[0];

                var whereCallExpression = ReplaceMethodCallExpression(node, entityType);
                return whereCallExpression;
            }
        }

        return base.VisitMethodCall(node);
    }

    private MethodCallExpression ReplaceMethodCallExpression(MethodCallExpression methodCallExpression, Type entityType)
    {
        var expr = Expression.Call(typeof(InternalExtensions), nameof(InternalExtensions.ReplaceDbContextQueryable), [entityType], methodCallExpression, Expression.Constant(DbContext)
        );

        return expr;
    }

    internal sealed class TempVariable<T>(IQueryable<T> queryable)
    {
        public IQueryable<T> Queryable { get; } = queryable;

        public IQueryable<T> GetQueryable()
        {
            return Queryable;
        }
    }

    internal sealed class TempDbVariable<T>(T ctx)
    {
        public T DbContext { get; } = ctx;
    }
}

internal sealed class DbContextReplaceQueryableVisitor(DbContext ctx) : ReplaceQueryableVisitor(ctx)
{
    // Set by VisitExtension before consumers read Source.
    public IQueryable Source { get; private set; } = null!;

    protected override Expression VisitExtension(Expression node)
    {
        if (node is QueryRootExpression root)
        {
            var dependencies = typeof(DbContext).GetPropertyValue(DbContext, "DbContextDependencies") as IDbContextDependencies
                               ?? throw new ShardingInvalidOperationException("cant resolve DbContextDependencies");

            var query = (IQueryable)((IDbSetCache)DbContext).GetOrAddSet(dependencies.SetSource, root.ElementType);

            var newQueryable = query.Provider.CreateQuery(query.Expression);
            Source ??= newQueryable;
            VisitedRoot = true;
            if (root is FromSqlQueryRootExpression expression)
            {
                var rootExpression = new FromSqlQueryRootExpression((IAsyncQueryProvider)newQueryable.Provider, expression.EntityType, expression.Sql, expression.Argument);

                return base.VisitExtension(rootExpression);
            }
            else
            {
                var replaceQueryRoot = new ReplaceQueryRootVisitor();
                replaceQueryRoot.Visit(newQueryable.Expression);
                return base.VisitExtension(replaceQueryRoot.RootExpression);
            }
        }

        return base.VisitExtension(node);
    }
}

internal sealed class ReplaceQueryRootVisitor : ExpressionVisitor
{
    // Set by VisitExtension before consumers read RootExpression.
    public QueryRootExpression RootExpression { get; private set; } = null!;

    protected override Expression VisitExtension(Expression node)
    {
        if (node is QueryRootExpression expression)
        {
            if (RootExpression != null)
            {
                throw new ShardingException("replace query root more than one query root");
            }

            RootExpression = expression;
        }

        return base.VisitExtension(node);
    }
}

internal sealed class RemoveExpressionVisitor : ExpressionVisitor
{
    private readonly string[] _names;

    public RemoveExpressionVisitor(params string[] names)
    {
        if (names.IsNull())
        {
            throw new ArgumentNullException(nameof(names));
        }

        _names = names;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        foreach (var name in _names)
        {
            if (node.Method.Name == name)
            {
                return base.Visit(node.Arguments[0]);
            }
        }

        return base.VisitMethodCall(node);
    }
}

internal sealed class RouteParseVisitor : ExpressionVisitor
{
    private bool _hasOrElse;
    private int _andAlsoCount;
    private int _equalCount;

    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (node.NodeType == ExpressionType.OrElse)
        {
            if (!_hasOrElse)
            {
                _hasOrElse = true;
            }
        }
        else if (node.NodeType == ExpressionType.AndAlso)
        {
            _andAlsoCount++;
        }
        else if (node.NodeType == ExpressionType.Equal)
        {
            _equalCount++;
        }

        return base.VisitBinary(node);
    }

    public bool HasOrElse()
    {
        return _hasOrElse;
    }

    public int AndAlsoCount()
    {
        return _andAlsoCount;
    }

    public int EqualCount()
    {
        return _equalCount;
    }
}

internal sealed class SelectDiscoveryVisitor(SelectContext selectContext) : ExpressionVisitor
{
    private PropertyInfo? GetAggregate(MethodCallExpression callExpression)
    {
        if (callExpression.Arguments.Count > 1)
        {
            if (callExpression.Arguments[1] is not LambdaExpression selector)
            {
                return null;
            }

            if (selector.Body is not MemberExpression memberExpression)
            {
                return null;
            }

            if (memberExpression.Member.DeclaringType == null)
            {
                return null;
            }

            var fromProperty = memberExpression.Member.DeclaringType.GetShadowingProperty(memberExpression.Member.Name);
            return fromProperty;
        }

        throw new ShardingException($"cant {nameof(GetAggregate)},{callExpression.Print()}");
    }

    protected override Expression VisitNew(NewExpression node)
    {
        if (node.Members == null)
        {
            for (var i = 0; i < node.Arguments.Count; i++)
            {
                var arg = node.Arguments[i];
                if (arg is MemberExpression memberExpression)
                {
                    var declaringType = memberExpression.Member.DeclaringType
                                        ?? throw new ShardingInvalidOperationException("select member DeclaringType is null");
                    var memberName = memberExpression.Member.Name;
                    var propertyInfo = declaringType.GetShadowingProperty(memberName)
                                       ?? throw new ShardingNotFoundException($"type:{declaringType} not found [{memberName}] property");
                    selectContext.Properties.Add(new SelectOwnerProperty(declaringType, propertyInfo));
                }
            }
        }
        else
        {
            //select 对象的数据和参数必须一致
            if (node.Members.Count != node.Arguments.Count)
            {
                throw new ShardingInvalidOperationException("cant parse select members length not eq arguments length");
            }

            for (var i = 0; i < node.Members.Count; i++)
            {
                var declaringType = node.Members[i].DeclaringType
                                    ?? throw new ShardingInvalidOperationException("select member DeclaringType is null");
                var memberName = node.Members[i].Name;
                var propertyInfo = declaringType.GetShadowingProperty(memberName)
                                   ?? throw new ShardingNotFoundException($"type:{declaringType} not found [{memberName}] property");
                if (node.Arguments[i] is MethodCallExpression methodCallExpression)
                {
                    var method = methodCallExpression.Method;

                    SelectOwnerProperty? selectOwnerProperty = null;

                    switch (method.Name)
                    {
                        case nameof(Queryable.Average):
                            {
                                var fromProperty = GetAggregate(methodCallExpression);

                                selectOwnerProperty = new SelectAverageProperty(method.Name, declaringType, propertyInfo, fromProperty);
                                break;
                            }

                        case nameof(Queryable.Count):
                            {
                                selectOwnerProperty = new SelectCountProperty(method.Name, declaringType, propertyInfo);
                                break;
                            }

                        case nameof(Queryable.Sum):
                            {
                                var fromProperty = GetAggregate(methodCallExpression);

                                selectOwnerProperty = new SelectSumProperty(method.Name, declaringType, propertyInfo, fromProperty);
                                break;
                            }

                        case nameof(Queryable.Max):
                            {
                                selectOwnerProperty = new SelectMaxProperty(method.Name, declaringType, propertyInfo);
                                break;
                            }

                        case nameof(Queryable.Min):
                            {
                                selectOwnerProperty = new SelectMinProperty(method.Name, declaringType, propertyInfo);
                                break;
                            }

                        default:
                            break;
                    }

                    if (selectOwnerProperty != null)
                    {
                        selectContext.Properties.Add(selectOwnerProperty);
                    }
                    else
                    {
                        // 非聚合投影（如 GroupBy Key）才记为 Owner；聚合属性不能再追加一份 Owner，否则跨分片按组合并会把 Max/Min 值当成分组键
                        selectContext.Properties.Add(new SelectOwnerProperty(declaringType, propertyInfo));
                    }
                }
                else
                {
                    selectContext.Properties.Add(new SelectOwnerProperty(declaringType, propertyInfo));
                }
            }
        }

        return base.VisitNew(node);
    }
}

internal sealed class QueryableDiscoveryVisitor(IMergeQueryCompilerContext context) : ShardingExpressionVisitor
{
    private readonly IMergeQueryCompilerContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly PagedContext _pagedContext = new();

    public SelectContext SelectContext { get; } = new();

    public OrderByContext OrderByContext { get; } = new();

    public GroupByContext GroupByContext { get; } = new();

    public PagedContext PagedContext
    {
        get
        {
            var fixedTake = _context.FixedTake;
            if (fixedTake.HasValue)
            {
                _pagedContext.ReplaceTake(fixedTake.Value);
            }

            return _pagedContext;
        }
    }

    private static void GetPropertyInfo(List<string> properties, MemberExpression memberExpression)
    {
        properties.Add(memberExpression.Member.Name);
        if (memberExpression.Expression is MemberExpression member)
        {
            GetPropertyInfo(properties, member);
        }
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var method = node.Method;
        if (node.Method.Name == nameof(Queryable.Skip))
        {
            if (_pagedContext.Skip.HasValue)
            {
                throw new ShardingInvalidOperationException("more than one skip found");
            }

            var skip = (int)(GetExpressionValue(node.Arguments[1]) ?? throw new ShardingInvalidOperationException("skip value is null"));
            _pagedContext.SetSkip(skip);
        }
        else if (node.Method.Name == nameof(Queryable.Take))
        {
            if (_pagedContext.Take.HasValue)
            {
                throw new ShardingInvalidOperationException("more than one take found");
            }

            var take = (int)(GetExpressionValue(node.Arguments[1]) ?? throw new ShardingInvalidOperationException("take value is null"));
            _pagedContext.SetTake(take);
        }
        else if (method.Name.In(nameof(Queryable.OrderBy), nameof(Queryable.OrderByDescending), nameof(Queryable.ThenBy), nameof(Queryable.ThenByDescending)))
        {
            if (typeof(IOrderedQueryable).IsAssignableFrom(node.Type))
            {
                MemberExpression? expression = null;
                var orderbody = ((node.Arguments[1] as UnaryExpression)?.Operand as LambdaExpression)?.Body
                                ?? throw new NotSupportedException("sharding order not support ");

                if (orderbody is MemberExpression orderMemberExpression)
                {
                    expression = orderMemberExpression;
                }
                else if (orderbody.NodeType == ExpressionType.Convert && orderbody is UnaryExpression orderUnaryExpression)
                {
                    if (orderUnaryExpression.Operand is MemberExpression orderMemberConvertExpression)
                    {
                        expression = orderMemberConvertExpression;
                    }
                }

                if (expression == null)
                {
                    throw new NotSupportedException("sharding order not support ");
                }

                var properties = new List<string>();
                GetPropertyInfo(properties, expression);
                if (properties.IsNull())
                {
                    throw new NotSupportedException("sharding order only support property expression");
                }

                properties.Reverse();
                var propertyExpression = properties.JoinStr(".");
                OrderByContext.Sorts.AddFirst(new PropertySorting(propertyExpression, method.Name.In(nameof(Queryable.OrderBy), nameof(Queryable.ThenBy)) ? SortDirection.Ascending : SortDirection.Descending, expression.Member.DeclaringType));
            }
        }
        else if (node.Method.Name == nameof(Queryable.GroupBy))
        {
            if (GroupByContext.Expression == null)
            {
                if ((node.Arguments[1] as UnaryExpression)?.Operand is not LambdaExpression expression)
                {
                    throw new NotSupportedException("sharding group not support ");
                }

                GroupByContext.Expression = expression;
            }
        }
        else if (node.Method.Name == nameof(Queryable.Select))
        {
            if (SelectContext.Properties.IsNull())
            {
                var expression = ((node.Arguments[1] as UnaryExpression)?.Operand as LambdaExpression)?.Body
                                 ?? throw new NotSupportedException("sharding select not support ");
                if (expression is NewExpression newExpression)
                {
                    var aggregateDiscoverVisitor = new SelectDiscoveryVisitor(SelectContext);
                    aggregateDiscoverVisitor.Visit(newExpression);
                }
                else if (expression is MemberExpression memberExpression)
                {
                    var declaringType = memberExpression.Member.DeclaringType
                                        ?? throw new ShardingInvalidOperationException("select member DeclaringType is null");
                    var memberName = memberExpression.Member.Name;
                    var propertyInfo = declaringType.GetShadowingProperty(memberName)
                                       ?? throw new ShardingNotFoundException($"type:{declaringType} not found [{memberName}] property");
                    SelectContext.Properties.Add(new SelectOwnerProperty(declaringType, propertyInfo));
                    //memberExpression.Acc
                }
                else if (expression is MemberInitExpression memberInitExpression)
                {
                    foreach (var memberBinding in memberInitExpression.Bindings)
                    {
                        if (memberBinding is MemberAssignment memberAssignment)
                        {
                            if (memberAssignment.Expression is MemberExpression bindMemberExpression)
                            {
                                var declaringType = memberBinding.Member.DeclaringType
                                                    ?? throw new ShardingInvalidOperationException("select member DeclaringType is null");
                                var memberName = memberBinding.Member.Name;
                                var propertyInfo = declaringType.GetShadowingProperty(memberName)
                                                   ?? throw new ShardingNotFoundException($"type:{declaringType} not found [{memberName}] property");
                                SelectContext.Properties.Add(new SelectOwnerProperty(declaringType, propertyInfo));
                            }
                        }
                    }
                }
            }
        }

        return base.VisitMethodCall(node);
    }
}

internal sealed class TrackingDiscoveryVisitor : ExpressionVisitor
{
    public bool? IsNoTracking { get; private set; }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == nameof(EntityFrameworkQueryableExtensions.AsNoTracking))
        {
            IsNoTracking = true;
        }
        else if (node.Method.Name == nameof(EntityFrameworkQueryableExtensions.AsTracking))
        {
            IsNoTracking = false;
        }

        return base.VisitMethodCall(node);
    }
}

internal sealed class UnionDiscoveryVisitor : ExpressionVisitor
{
    public bool IsUnion { get; private set; }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == nameof(Queryable.Union))
        {
            IsUnion = true;
        }

        return base.VisitMethodCall(node);
    }
}

/// <summary>
/// 是否是分表路由
/// </summary>
/// <param name="entityMetadata"></param>
/// <param name="filter"></param>
/// <param name="compareValue"></param>
/// <param name="tableRoute"></param>
internal sealed class TableRouteDiscoveryVisitor(EntityMetadata entityMetadata, Func<object, ShardingOperator, string?, Func<string, bool>> filter, Func<object, string?, object> compareValue, bool tableRoute) : ShardingExpressionVisitor
{
    private static readonly Func<bool, ExpressionType, ShardingOperator> _operatorFactory = (cond, nodeType) =>
        {
            var op = nodeType switch
            {
                ExpressionType.GreaterThan => cond ? ShardingOperator.GreaterThan : ShardingOperator.LessThan,
                ExpressionType.GreaterThanOrEqual => cond ? ShardingOperator.GreaterThanOrEqual : ShardingOperator.LessThanOrEqual,
                ExpressionType.LessThan => cond ? ShardingOperator.LessThan : ShardingOperator.GreaterThan,
                ExpressionType.LessThanOrEqual => cond ? ShardingOperator.LessThanOrEqual : ShardingOperator.GreaterThanOrEqual,
                ExpressionType.Equal => ShardingOperator.Equal,
                ExpressionType.NotEqual => ShardingOperator.NotEqual,
                _ => ShardingOperator.UnKnown
            };
            return op;
        };

    private readonly ShardingResult _noResult = new(false, null);
    private LambdaExpression? _expression;
    private bool _ignoreQueryFilter;
    private ShardingRouteExpression _where = ShardingRouteExpression.True;

    /// <summary>
    /// 获取路由表达式
    /// </summary>
    /// <returns></returns>
    public ShardingRouteExpression GetRouteExpression()
    {
        if (entityMetadata.QueryFilterExpression != null && !_ignoreQueryFilter)
        {
            if (_expression == null)
            {
                _expression = entityMetadata.QueryFilterExpression;
            }
            else
            {
                var body = Expression.AndAlso(_expression.Body, entityMetadata.QueryFilterExpression.Body);
                _expression = Expression.Lambda(body, _expression.Parameters[0]);
            }
        }

        if (_expression != null)
        {
            var newWhere = Resolve(_expression);
            _where = _where.And(newWhere);
        }

        return _where;
    }

    private bool IsShardingKey(Expression? expression, out ShardingResult result)
    {
        MemberExpression? realMember = null;
        if (expression is MemberExpression member)
        {
            if (member.Expression?.Type == entityMetadata.EntityType)
            {
                realMember = member;
            }
        }
        else if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
        {

            if (unary.Operand is MemberExpression m && m.Expression?.Type == entityMetadata.EntityType)
            {
                realMember = m;
            }
        }

        if (realMember != null)
        {
            bool isShardingKey;
            if (tableRoute)
            {
                isShardingKey = entityMetadata.TableProperties.ContainsKey(realMember.Member.Name);
            }
            else
            {
                isShardingKey = entityMetadata.DataSourceProperties.ContainsKey(realMember.Member.Name);
            }

            if (isShardingKey)
            {
                result = new ShardingResult(true, realMember.Member.Name);
                return true;
            }
        }

        result = _noResult;
        return false;
    }

    /// <summary>
    /// 方法是否包含shardingKey xxx.invoke(shardingkey) eg. <code>o=>new[]{}.Contains(o.Id)</code>
    /// </summary>
    /// <param name="methodCallExpression"></param>
    /// <returns></returns>
    private ShardingResult IsShardingKey(MethodCallExpression methodCallExpression)
    {
        if (methodCallExpression.Arguments.IsNotNull())
        {
            for (var i = 0; i < methodCallExpression.Arguments.Count; i++)
            {
                if (IsShardingKey(methodCallExpression.Arguments[i], out var result))
                {
                    return result;
                }
            }
        }

        return _noResult;
    }

    private ShardingResult IsShardingConstant(MethodCallExpression methodCallExpression)
    {
        if (methodCallExpression.Object != null)
        {
            if (IsShardingKey(methodCallExpression.Object, out var result))
            {
                return result;
            }
        }

        return _noResult;
    }

    /// <summary>
    /// 表达式是否可以获取值
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    private static bool CanGetValue(Expression expression)
    {
        return expression is ConstantExpression || expression is NewExpression || expression is ListInitExpression || expression is NewArrayExpression || expression is MemberExpression member && (member.Expression is ConstantExpression || member.Expression is MemberExpression || member.Expression is MemberExpression) || expression is MethodCallExpression || expression is UnaryExpression unaryExpression && unaryExpression.NodeType is ExpressionType.Convert || expression.NodeType == ExpressionType.ArrayIndex;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        switch (node.Method.Name)
        {
            case nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters):
                _ignoreQueryFilter = true;
                break;
            case nameof(Queryable.Where):
                Combine(node);
                break;
        }

        return base.VisitMethodCall(node);
    }

    private void Combine(MethodCallExpression node)
    {
        if (node.Arguments[1] is UnaryExpression unary)
        {
            if (unary.Operand is LambdaExpression expression)
            {
                if (expression.Parameters[0].Type == entityMetadata.EntityType)
                {
                    if (_expression == null)
                    {
                        _expression = expression;
                    }
                    else
                    {
                        var body = Expression.AndAlso(_expression.Body, expression.Body);
                        var lambda = Expression.Lambda(body, _expression.Parameters[0]);
                        _expression = lambda;
                    }
                }
            }
        }
    }

    private ShardingRouteExpression Resolve(Expression expression)
    {
        if (expression is LambdaExpression lambda)
        {
            expression = lambda.Body;
            return Resolve(expression);
        }

        //解析左右结构属性判断
        if (expression is BinaryExpression binary) //解析二元运算符
        {
            return ParseProperty(binary);
        }

        if (expression is UnaryExpression unary) //解析一元运算符
        {
            if (unary.Operand is MethodCallExpression unaryCallExpression)
            {
                // return ResolveLinqToObject(unary.Operand, false);
                return Resolve(unaryCallExpression, unary.NodeType != ExpressionType.Not);
            }
        }

        if (expression is MethodCallExpression methodCallExpression) //解析方法
        {
            return Resolve(methodCallExpression, true);
        }

        return ShardingRouteExpression.True;
    }

    private ShardingRouteExpression Resolve(MethodCallExpression callExpression, bool bit)
    {
        if (callExpression.IsEnumerableMethod(nameof(IList.Contains)))
        {
            var result = IsShardingKey(callExpression);
            if (result.IsShardingKey)
            {
                object? arrayObject = null;
                if (callExpression.Object != null)
                {
                    if (callExpression.Object is MemberExpression member1Expression)
                    {
                        arrayObject = GetExpressionValue(member1Expression);
                    }
                    else if (callExpression.Object is ListInitExpression member2Expression)
                    {
                        arrayObject = GetExpressionValue(member2Expression);
                    }
                }
                else if (callExpression.Arguments[0] is MemberExpression member2Expression)
                {
                    arrayObject = GetExpressionValue(member2Expression);
                }
                else if (callExpression.Arguments[0] is NewArrayExpression member3Expression)
                {
                    arrayObject = GetExpressionValue(member3Expression);
                }

                if (arrayObject != null)
                {
                    var contains = bit ? ShardingRouteExpression.False : ShardingRouteExpression.True;

                    if (arrayObject is IEnumerable enumerable)
                    {
                        var compareSet = new HashSet<object>();

                        foreach (var shardingValue in enumerable)
                        {
                            var compare = compareValue(shardingValue, result.PropertyName);
                            if (!compareSet.Add(compare))
                            {
                                continue;
                            }

                            var eq = filter(shardingValue, bit ? ShardingOperator.Equal : ShardingOperator.NotEqual, result.PropertyName);
                            if (bit)
                            {
                                contains = contains.Or(new ShardingRouteExpression(eq));
                            }
                            else
                            {
                                contains = contains.And(new ShardingRouteExpression(eq));
                            }
                        }
                    }

                    return contains;
                }
            }
        }
        else if (callExpression.IsStringMethod(nameof(string.Contains)))
        {
            if (IsShardingKey(callExpression.Object, out var result))
            {
                if (callExpression.Arguments.Count == 1)
                {
                    var shardingValue = GetExpressionValue(callExpression.Arguments[0]);
                    if (shardingValue is null)
                    {
                        return ShardingRouteExpression.True;
                    }

                    var keyToTailWithFilter = filter(shardingValue, ShardingOperator.AllLike, result.PropertyName);
                    return new ShardingRouteExpression(keyToTailWithFilter);
                }
            }
        }
        else if (callExpression.IsStringMethod(nameof(string.StartsWith)))
        {
            if (IsShardingKey(callExpression.Object, out var result))
            {
                if (callExpression.Arguments.Count == 1)
                {
                    var shardingValue = GetExpressionValue(callExpression.Arguments[0]);
                    if (shardingValue is null)
                    {
                        return ShardingRouteExpression.True;
                    }

                    var keyToTailWithFilter = filter(shardingValue, ShardingOperator.StartLike, result.PropertyName);
                    return new ShardingRouteExpression(keyToTailWithFilter);
                }
            }
        }
        else if (callExpression.IsStringMethod(nameof(string.EndsWith)))
        {
            if (IsShardingKey(callExpression.Object, out var result))
            {
                if (callExpression.Arguments.Count == 1)
                {
                    var shardingValue = GetExpressionValue(callExpression.Arguments[0]);
                    if (shardingValue is null)
                    {
                        return ShardingRouteExpression.True;
                    }

                    var keyToTailWithFilter = filter(shardingValue, ShardingOperator.EndLike, result.PropertyName);
                    return new ShardingRouteExpression(keyToTailWithFilter);
                }
            }
        }
        else if (callExpression.Method.Name.EqualsTo(nameof(object.Equals), StringComparison.Ordinal))
        {
            //"".equals(o.id)
            var result = IsShardingKey(callExpression);
            if (result.IsShardingKey)
            {
                var shardingValue = GetExpressionValue(callExpression.Object);
                if (shardingValue != null)
                {
                    var keyToTailWithFilter = filter(shardingValue, ShardingOperator.Equal, result.PropertyName);
                    return new ShardingRouteExpression(keyToTailWithFilter);
                }
            }
            else
            {
                //o.id.equals("")
                result = IsShardingConstant(callExpression);
                if (result.IsShardingKey)
                {
                    object? shardingValue = null;
                    if (callExpression.Arguments[0] is MemberExpression member2Expression)
                    {
                        shardingValue = GetExpressionValue(member2Expression);
                    }
                    else if (callExpression.Arguments[0] is ConstantExpression constantExpression)
                    {
                        shardingValue = GetExpressionValue(constantExpression);
                    }

                    if (shardingValue != null)
                    {
                        var keyToTailWithFilter = filter(shardingValue, ShardingOperator.Equal, result.PropertyName);
                        return new ShardingRouteExpression(keyToTailWithFilter);
                    }
                }
            }
        }

        return ShardingRouteExpression.True;
    }

    private static ShardingOperator Parse(bool cond,
        ExpressionType expressionType, int compare)
    {
        if (compare == 1)
        {
            return expressionType switch
            {
                ExpressionType.GreaterThanOrEqual => cond ? ShardingOperator.GreaterThan : ShardingOperator.LessThan, //1
                ExpressionType.GreaterThan => ShardingOperator.UnKnown, //无
                ExpressionType.LessThanOrEqual => ShardingOperator.UnKnown, //1,0,-1 = 无
                ExpressionType.LessThan => cond ? ShardingOperator.LessThanOrEqual : ShardingOperator.GreaterThanOrEqual, //0,-1
                ExpressionType.Equal => cond ? ShardingOperator.GreaterThan : ShardingOperator.LessThan, //1
                ExpressionType.NotEqual => ShardingOperator.NotEqual,
                _ => ShardingOperator.UnKnown
            };
        }

        if (compare == 0)
        {
            return expressionType switch
            {
                ExpressionType.GreaterThanOrEqual => cond ? ShardingOperator.GreaterThanOrEqual : ShardingOperator.LessThanOrEqual, //0,1
                ExpressionType.GreaterThan => cond ? ShardingOperator.GreaterThan : ShardingOperator.LessThan, //1
                ExpressionType.LessThanOrEqual => cond ? ShardingOperator.LessThanOrEqual : ShardingOperator.GreaterThanOrEqual, //0,-1
                ExpressionType.LessThan => cond ? ShardingOperator.LessThan : ShardingOperator.GreaterThan, //-1
                ExpressionType.Equal => ShardingOperator.Equal,
                ExpressionType.NotEqual => ShardingOperator.NotEqual,
                _ => ShardingOperator.UnKnown
            };
        }

        if (compare == -1)
        {
            return expressionType switch
            {
                ExpressionType.GreaterThanOrEqual => ShardingOperator.UnKnown, //-1,0,1
                ExpressionType.GreaterThan => cond ? ShardingOperator.GreaterThanOrEqual : ShardingOperator.LessThanOrEqual, //0,1
                ExpressionType.LessThanOrEqual => cond ? ShardingOperator.LessThan : ShardingOperator.GreaterThan, //-1
                ExpressionType.LessThan => ShardingOperator.UnKnown, //无
                ExpressionType.Equal => cond ? ShardingOperator.LessThan : ShardingOperator.GreaterThan, //1
                ExpressionType.NotEqual => ShardingOperator.NotEqual,
                _ => ShardingOperator.UnKnown
            };
        }

        return ShardingOperator.UnKnown;
    }

    private ShardingRouteExpression ParseCompare(MethodCallExpression callExpression, Expression? left, Expression? right, ExpressionType expressionType)
    {
        if (left is null || right is null)
        {
            return ShardingRouteExpression.True;
        }

        if (left.Type == right.Type)
        {
            if (callExpression.Method.ReturnType == typeof(int))
            {
                return ParseCondition(left, right, expressionType);
            }
        }

        return ShardingRouteExpression.True;
    }

    private ShardingRouteExpression ParseCondition(bool cond, ShardingResult result, Expression conditionExpression, ExpressionType expressionType)
    {
        if (CanGetValue(conditionExpression))
        {
            var propertyName = result.PropertyName;
            var value = GetExpressionValue(conditionExpression);

            if (propertyName == null || value == default)
            {
                return ShardingRouteExpression.True;
            }

            var op = _operatorFactory(cond, expressionType);
            return new ShardingRouteExpression(filter(value, op, propertyName));
        }

        return ShardingRouteExpression.True;
    }

    private ShardingRouteExpression ParseCondition(Expression left, Expression right, ExpressionType expressionType)
    {
        if (IsShardingKey(left, out var predicateLeftResult))
        {
            return ParseCondition(true, predicateLeftResult, right, expressionType);
        }
        else if (IsShardingKey(right, out var predicateRightResult))
        {
            return ParseCondition(false, predicateRightResult, left, expressionType);
        }

        return ShardingRouteExpression.True;
    }

    private ShardingRouteExpression ParseNamedComparison(BinaryExpression binaryExpression,
        MethodCallExpression methodCallExpression)
    {
        if (methodCallExpression.GetComparison(out var result))
        {
            return ParseCompare(methodCallExpression, result.Left, result.Right, binaryExpression.NodeType);
        }

        return ShardingRouteExpression.True;
    }

    private ShardingRouteExpression ParseProperty(BinaryExpression binaryExpression)
    {
        // ShardingRouteExpression left = ShardingRouteExpression.Default;
        // ShardingRouteExpression right = ShardingRouteExpression.Default;
        //左边是属性判断是否是分片的
        if (IsShardingKey(binaryExpression.Left, out var predicateLeftResult))
        {
            return ParseCondition(true, predicateLeftResult, binaryExpression.Right,
                binaryExpression.NodeType);
        }
        else if (IsShardingKey(binaryExpression.Right, out var predicateRightResult))
        {
            return ParseCondition(false, predicateRightResult, binaryExpression.Left,
                binaryExpression.NodeType);
        }
        else if (binaryExpression.IsNamedComparison(out var methodCallExpression))
        {
            return ParseNamedComparison(binaryExpression, methodCallExpression);
        }
        else
        {
            var left = ShardingRouteExpression.True;
            var right = ShardingRouteExpression.True;

            //递归获取
            if (binaryExpression.Left is BinaryExpression binaryExpression1)
            {
                left = ParseProperty(binaryExpression1);
            }

            if (binaryExpression.Right is BinaryExpression binaryExpression2)
            {
                right = ParseProperty(binaryExpression2);
            }

            if (binaryExpression.Left is MethodCallExpression methodCallLeftExpression)
            {
                if (!methodCallLeftExpression.IsNamedComparison())
                {
                    left = Resolve(methodCallLeftExpression);
                }
            }

            if (binaryExpression.Right is MethodCallExpression methodCallRightExpression)
            {
                if (!methodCallRightExpression.IsNamedComparison())
                {
                    right = Resolve(methodCallRightExpression);
                }
            }

            if (binaryExpression.Left is UnaryExpression unary1 &&
                binaryExpression.Right is MemberExpression)
            {
                left = Resolve(unary1);
            }

            if (binaryExpression.Right is UnaryExpression unary2 &&
                binaryExpression.Left is MemberExpression)
            {
                right = Resolve(unary2);
            }

            //组合
            if (binaryExpression.NodeType == ExpressionType.AndAlso)
            {
                return left.And(right);
            }
            else if (binaryExpression.NodeType == ExpressionType.OrElse)
            {
                return left.Or(right);
            }
            else
            {
                return ShardingRouteExpression.True;
            }
        }
    }
}

internal sealed class ShardingPrepareVisitor(IShardingDbContext shardingDbContext) : ExpressionVisitor
{
    private readonly ITrackerManager _trackerManager = ((DbContext)shardingDbContext).GetRuntimeContext().TrackerManager;
    private bool _notSupport;
    private ShardingAsConnectionOptions? _connectionOptions;
    private ShardingAsRouteOptions? _routeOptions;
    private ShardingAsSequenceOptions? _sequenceOptions;

    private bool _noTracking;
    private bool _ignoreFilter;
    private readonly Dictionary<Type, IQueryable?> _entities = [];

    public ShardingPrepareResult GetShardingPrepareResult()
    {
        return new ShardingPrepareResult(_connectionOptions, _routeOptions, null, _sequenceOptions, _notSupport, _entities, _noTracking, _ignoreFilter);
    }

    protected override Expression VisitExtension(Expression node)
    {
        if (node is QueryRootExpression queryRootExpression)
        {
            TryAddShardingEntities(queryRootExpression.ElementType, null);
        }

        return base.VisitExtension(node);
    }

    private void TryAddShardingEntities(Type entityType, IQueryable? queryable)
    {
        _entities.TryAdd(entityType, queryable);
    }

    protected override Expression VisitMember(MemberExpression memberExpression)
    {
        // Recurse down to see if we can simplify...
        var expression = Visit(memberExpression.Expression);

        // If we've ended up with a constant, and it's a property or a field,
        // we can simplify ourselves to a constant
        if (expression is ConstantExpression expr)
        {
            var container = expr.Value;
            var member = memberExpression.Member;
            if (member is FieldInfo fieldInfo)
            {
                var value = fieldInfo.GetValue(container);
                if (value is IQueryable queryable)
                {
                    TryAddShardingEntities(queryable.ElementType, queryable);
                }
                //return Expression.Constant(value);
            }

            if (member is PropertyInfo propertyInfo)
            {
                var value = propertyInfo.GetValue(container, null);
                if (value is IQueryable queryable)
                {
                    TryAddShardingEntities(queryable.ElementType, queryable);
                }
            }
        }

        return base.VisitMember(memberExpression);
    }
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {

        switch (node.Method.Name)
        {
            case nameof(EntityFrameworkQueryableExtensions.AsNoTracking):
                _noTracking = true;
                break;
            case nameof(EntityFrameworkQueryableExtensions.AsTracking):
                _noTracking = false;
                break;
            case nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters):
                _ignoreFilter = true;
                break;
            default:
                {
                    if (node.Method.ReturnType.IsQueryable() && node.Method.ReturnType.IsGenericType)
                    {
                        DiscoverQueryEntities(node);
                    }

                    var customerExpression = DiscoverEntities(node);
                    if (customerExpression != null)
                    {
                        return Visit(customerExpression);
                    }
                }

                break;
        }

        return base.VisitMethodCall(node);
    }

    private Expression? DiscoverEntities(MethodCallExpression node)
    {

        if (node.Method.IsGenericMethod)
        {
            var method = node.Method.GetGenericMethodDefinition();

            // find  notsupport extention calls
            if (method == ShardingEntityFrameworkExtensions.UseMergeMethod)
            {
                _notSupport = true;
                // cut out extension expression
                return node.Arguments[0];
            }
            else if (method == ShardingEntityFrameworkExtensions.AsConnectionMethod)
            {
                _connectionOptions = (ShardingAsConnectionOptions)node.Arguments.OfType<ConstantExpression>().Last(o => o.Value is ShardingAsConnectionOptions).Value!;
                return node.Arguments[0];
            }
            else if (method == ShardingEntityFrameworkExtensions.AsRouteMethod)
            {
                _routeOptions = (ShardingAsRouteOptions)node.Arguments.OfType<ConstantExpression>().Last(o => o.Value is ShardingAsRouteOptions).Value!;
                return node.Arguments[0];
            }
            else if (method == ShardingEntityFrameworkExtensions.AsSequenceMethod)
            {
                _sequenceOptions = (ShardingAsSequenceOptions)node.Arguments.OfType<ConstantExpression>().Last(o => o.Value is ShardingAsSequenceOptions).Value!;
                return node.Arguments[0];
            }
        }

        return null;
    }

    private void DiscoverQueryEntities(MethodCallExpression node)
    {
        var args = node.Type.GetGenericArguments();
        foreach (var i in args.Length)
        {
            var genericArgument = args[i];

            if (typeof(IEnumerable).IsAssignableFrom(genericArgument))
            {
                var arguments = genericArgument.GetGenericArguments();
                foreach (var argument in arguments)
                {
                    //if is db context model
                    if (_trackerManager.Contains(argument))
                    {
                        TryAddShardingEntities(argument, null);
                    }
                }
            }

            if (!genericArgument.IsSimpleType())
            {
                //if is db context model
                if (_trackerManager.Contains(genericArgument))
                {
                    TryAddShardingEntities(genericArgument, null);
                }
            }
        }
    }
}
