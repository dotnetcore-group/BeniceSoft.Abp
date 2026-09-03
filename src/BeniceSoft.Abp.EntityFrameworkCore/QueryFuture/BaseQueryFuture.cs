using BeniceSoft.Core.Reflector;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>单个延迟查询的基类：负责编译命令、在批结果 DataReader 上物化。</summary>
[SuppressMessage("Usage", "EF1001:Internal EF Core API usage.", Justification = "Required to inject DataReader into EF compiled query enumerator.")]
public abstract class BaseQueryFuture
{
    public bool HasValue { get; protected set; }

    public QueryFutureBatch? OwnerBatch { get; set; }

    public IQueryable? Query { get; set; }

    public object? QueryExecutor { get; set; }

    public QueryContext? QueryContext { get; set; }

    internal IRelationalConnection? QueryConnection { get; set; }

    internal object? CompiledQuery { get; set; }

    internal Action? RestoreConnection { get; set; }

    public virtual void ExecuteInMemory()
    {
    }

    /// <summary>
    /// 编译本查询命令，并把 RelationalConnection 的底层连接临时换成 CreateEntityConnection，
    /// 以便随后 SetResult 时用批 DataReader 喂给 EF 枚举器而不再真正 Open/Execute。
    /// </summary>
    public virtual IRelationalCommand CreateExecutorAndGetCommand(out RelationalQueryContext queryContext)
    {
        var query = Query ?? throw new InvalidOperationException("QueryFuture.Query is not set.");
        var ctx = query.GetDbContext();
        QueryConnection = ctx.Database.GetService<IRelationalConnection>();
        var innerConnection = new CreateEntityConnection(QueryConnection.DbConnection, null);
        var innerConnectionField = typeof(RelationalConnection).GetField("_connection", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var initialConnection = innerConnectionField.GetReflector().GetValue(QueryConnection);
        innerConnectionField.GetReflector().SetValue(QueryConnection, innerConnection);
        RestoreConnection = () => innerConnectionField.GetReflector().SetValue(QueryConnection, initialConnection);

        var relationalCommand = query.CreateCommand(out queryContext, out var compiledQueryOut);
        QueryContext = queryContext;
        CompiledQuery = compiledQueryOut;

        return relationalCommand;
    }

    public virtual void SetResult(DbDataReader reader)
    {
    }

    /// <summary>
    /// 把当前 DataReader 挂到劫持连接上，取出编译查询的 GetEnumerator，清空 _readerColumns 后返回，
    /// 使枚举器从已打开的 reader 物化实体。
    /// </summary>
    public IEnumerator<T> GetQueryEnumerator<T>(DbDataReader reader)
    {
        ((CreateEntityConnection)QueryConnection!.DbConnection).OriginalDataReader = reader;
        var compiledQuery = CompiledQuery!;
        var getEnumeratorMethod = compiledQuery.GetType().GetMethod("GetEnumerator", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        var enumerator = (IEnumerator<T>)getEnumeratorMethod.GetReflector().Invoke(compiledQuery, [])!;

        var fieldReaderColumns = enumerator.GetType().GetField("_readerColumns", BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        fieldReaderColumns?.GetReflector().SetValue(enumerator, null!);

        return enumerator;
    }

    public virtual void GetResultDirectly()
    {
    }

    public virtual Task GetResultDirectlyAsync(CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
