using System.Linq.Expressions;
using BeniceSoft.Core;
using BeniceSoft.Core.Strategy;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface IQueryableOptimize
{
    IOptimizeResult Optimize(IMergeQueryCompilerContext context, IParseResult parseResult, IRewriteResult rewriteResult);
}

internal sealed class QueryableOptimize(ITableRouteManager manager) : IQueryableOptimize
{
    public IOptimizeResult Optimize(IMergeQueryCompilerContext context, IParseResult parseResult, IRewriteResult rewriteResult)
    {
        var db = context.DbContext;
        var virtualDataSource = db.GetExecutor().GetVirtualDataSource();
        var max = virtualDataSource.Options.MaxQueryConnections;
        IComparer<string> tailComparer = Comparer<string>.Default;
        var sameComparer = true;
        var sequence = false;
        if (context.Single && context.IsCrossTable && !context.UseMerge)
        {
            var singleType = context.SingleType
                ?? throw new ShardingInvalidOperationException("SingleType is required for cross-table optimize.");
            var tableRoute = manager.GetRoute(singleType);
            if (tableRoute.EnableQuery)
            {
                var queryMetadata = tableRoute.EntityQueryMetadata!;
                if (queryMetadata.DefaultTailComparer != null)
                {
                    tailComparer = queryMetadata.DefaultTailComparer;
                }

                sameComparer = queryMetadata.Reverse;
                var methodName = context.IsEnumerable ? EntityQueryMetadata.Enumerator : ((MethodCallExpression)context.Expression).Method.Name;

                if (queryMetadata.TryGetLimit(methodName, out var limit))
                {
                    max = Math.Min(limit, max);
                }

                if (TryGetSequence(parseResult, singleType, tableRoute, methodName, out var direction))
                {
                    sequence = true;
                    if (direction == SortDirection.Descending)
                    {
                        sameComparer = !sameComparer;
                    }
                }
            }
        }

        max = context.MaxQueryConnections == 0 ? max : context.MaxQueryConnections;

        var connectionMode = context.ConnectionMode;
        return new OptimizeResult(max, connectionMode, sequence, sameComparer,
            tailComparer);
    }

    /// <summary>
    /// 是否需要判断order
    /// </summary>
    /// <param name="methodName"></param>
    /// <param name="sorts"></param>
    /// <returns></returns>
    private bool EffectOrder(string methodName, PropertySorting[] sorts)
    {
        if ((methodName == null || nameof(Queryable.First) == methodName || nameof(Queryable.FirstOrDefault) == methodName || nameof(Queryable.Last) == methodName || nameof(Queryable.LastOrDefault) == methodName || nameof(Queryable.Single) == methodName || nameof(Queryable.SingleOrDefault) == methodName || EntityQueryMetadata.Enumerator == methodName) && sorts.Length > 0)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 尝试获取当前方法是否采用顺序查询,如果有先判断排序没有的情况下判断默认
    /// </summary>
    /// <param name="parseResult"></param>
    /// <param name="singleType"></param>
    /// <param name="tableRoute"></param>
    /// <param name="methodName"></param>
    /// <param name="direction"></param>
    /// <returns></returns>
    private bool TryGetSequence(IParseResult parseResult, Type singleType, ITableRoute tableRoute, string methodName, out SortDirection direction)
    {
        var propertysortsrders = parseResult.OrderByContext.Sorts.ToArray();
        var effectOrder = EffectOrder(methodName, propertysortsrders);

        if (effectOrder)
        {
            var primaryOrder = propertysortsrders[0];
            //不是多级order 
            var primaryOrderPropertyName = primaryOrder.Expression;
            if (!primaryOrderPropertyName.Contains('.'))
            {
                if (tableRoute.EnablePaged && tableRoute.EntityQueryMetadata!.TryGetSequence(primaryOrderPropertyName, out var match) && match != null && (primaryOrder.OwnerType == singleType || match.Mode.HasFlag(SequenceMatchMode.Named))) //要么必须是当前对象查询要么就是名称一样
                {
                    direction = match.SameTailComparer ? primaryOrder.Direction : primaryOrder.Direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;

                    //如果是获取最后一个还需要再次翻转
                    if (methodName is (nameof(Queryable.Last)) or (nameof(Queryable.LastOrDefault)))
                    {
                        direction = direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
                    }

                    return true;
                }
            }

            direction = SortDirection.Ascending;
            return false;
        }

        if (tableRoute.EnableQuery && methodName != null &&
            tableRoute.EntityQueryMetadata!.TryGetDefault(methodName, out var asc))
        {
            direction = asc ? SortDirection.Ascending : SortDirection.Descending;
            return true;
        }

        //Max和Min
        if (methodName is (nameof(Queryable.Max)) or (nameof(Queryable.Min)))
        {
            //如果是max或者min
            if (tableRoute.EnableQuery && parseResult.SelectContext.Properties.Count == 1 && tableRoute.EntityQueryMetadata!.TryGetSequence(parseResult.SelectContext.Properties[0].Name, out var match) && match != null && (parseResult.SelectContext.Properties[0].OwnerType == singleType ||
                    match.Mode.HasFlag(SequenceMatchMode.Named)))
            {
                var tag = match.SameTailComparer ? nameof(Queryable.Min) == methodName : nameof(Queryable.Max) == methodName;
                direction = tag ? SortDirection.Ascending : SortDirection.Descending;
                return true;
            }
        }

        direction = SortDirection.Ascending;
        return false;
    }
}

internal interface IQueryableRewrite
{
    IRewriteResult Rewrite(IMergeQueryCompilerContext context, IParseResult parseResult);
}

internal sealed class QueryableRewrite : IQueryableRewrite
{
    public IRewriteResult Rewrite(IMergeQueryCompilerContext context, IParseResult parseResult)
    {
        var combineQueryable = context.Result.Queryable;
        var pagedContext = parseResult.PagedContext;
        var skip = pagedContext.Skip;
        var take = pagedContext.Take;

        var reWriteQueryable = combineQueryable;

        //去除分页,获取分页前的数量
        if (skip.HasValue || take.HasValue)
        {
            reWriteQueryable = reWriteQueryable.RemoveVisitor(nameof(Queryable.Skip), nameof(Queryable.Take));
        }

        if (take.HasValue)
        {
            if (skip.HasValue)
            {
                reWriteQueryable = reWriteQueryable.ReSkip(0).ReTake(take.Value + skip.GetValueOrDefault());
            }
            else
            {
                reWriteQueryable = reWriteQueryable.ReTake(take.Value + skip.GetValueOrDefault());
            }
        }

        var selectContext = parseResult.SelectContext;
        var groupByContext = parseResult.GroupByContext;
        var sorts = parseResult.OrderByContext.Sorts;
        if (groupByContext.Expression != null)
        {
            var groupProperties = selectContext.Properties.FindAll(o => o is not SelectAggregateProperty);

            if (groupProperties.IsNull())
            {
                throw new ShardingInvalidOperationException("group by select object must contains group by key value");
            }

            if (sorts.IsNull())
            {
                groupByContext.MemoryMerge = false;
                var sort = groupProperties.Select(o => $"{o.Name} asc").JoinStr();
                reWriteQueryable = reWriteQueryable.RemoveAnyOrderBy().WithSort(sort);

                foreach (var orderProperty in groupProperties)
                {
                    sorts.AddLast(new PropertySorting(orderProperty.Name, SortDirection.Ascending, orderProperty.OwnerType));
                }
            }
            else
            {
                var groupKeys = groupProperties.Select(o => o.Name).ToHashSet();
                var groupMemoryMerge = false;
                foreach (var sort in sorts)
                {
                    groupByContext.Sorts.Add(sort);
                    if (!groupMemoryMerge && groupKeys.IsNotNull())
                    {
                        if (!groupKeys.Contains(sort.Expression))
                        {
                            groupMemoryMerge = true;
                        }

                        groupKeys.Remove(sort.Expression);
                    }
                }

                //判断是否优先group key排序如果不是就是要内存聚合
                groupByContext.MemoryMerge = groupMemoryMerge;
                if (groupByContext.MemoryMerge)
                {
                    var sort = groupProperties.Select(o => $"{o.Name} asc").JoinStr();
                    reWriteQueryable = reWriteQueryable.RemoveAnyOrderBy().WithSort(sort);

                    sorts.Clear();
                    foreach (var property in groupProperties)
                    {
                        sorts.AddLast(new PropertySorting(property.Name, SortDirection.Ascending, property.OwnerType));
                    }
                }
            }

            if (selectContext.HasAverage)
            {
                var properties = selectContext.Properties.OfType<SelectAverageProperty>().ToList();
                var subProperties = selectContext.Properties.OfType<SelectAggregateProperty>()
                    .Where(o => o is not SelectAverageProperty).ToList();

                foreach (var property in properties)
                {
                    var countProperty = subProperties.Find(o => o is SelectCountProperty selectCountProperty);
                    if (countProperty != null)
                    {
                        property.SetCountProperty(countProperty.Property);
                    }

                    var sumProperty = subProperties.Find(o => o is SelectSumProperty selectSumProperty && selectSumProperty.FromProperty == property.FromProperty);
                    if (sumProperty != null)
                    {
                        property.SetSumProperty(sumProperty.Property);
                    }

                    if (property.CountProperty == null && property.SumProperty == null)
                    {
                        throw new ShardingInvalidOperationException($"use aggregate function average error,not found count aggregate function and not found sum aggregate function that property name same as average aggregate function property name:[{property.FromProperty?.Name}]");
                    }
                }
            }
        }

        if (context.UseMerge)
        {
            if (!context.DbContext.SupportMerge())
            {
                throw new ShardingException($"if use UseMerge plz rewrite {nameof(IQuerySqlGeneratorFactory)} with {nameof(IMergeQuerySqlGeneratorFactory)} and {nameof(IQueryCompiler)} with {nameof(IMergeQueryCompiler)}");
            }
        }

        return new RewriteResult(combineQueryable, reWriteQueryable);
    }
}

internal interface IQueryableParse
{
    IParseResult Parse(IMergeQueryCompilerContext context);
}

internal sealed class QueryableParse : IQueryableParse
{
    public IParseResult Parse(IMergeQueryCompilerContext context)
    {
        var combineQueryable = context.Result.Queryable;
        var visitor = new QueryableDiscoveryVisitor(context);
        visitor.Visit(combineQueryable.Expression);
        return new ParseResult(visitor.PagedContext, visitor.SelectContext, visitor.OrderByContext, visitor.GroupByContext);
    }
}
