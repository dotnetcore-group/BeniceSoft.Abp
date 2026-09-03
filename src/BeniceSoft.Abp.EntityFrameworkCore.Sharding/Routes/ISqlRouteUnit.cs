using BeniceSoft.Core;
namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal interface ISqlRouteUnit
{
    string DataSource { get; }

    TableRouteResult RouteResult { get; }
}

internal sealed class SqlRouteUnit(string dataSource, TableRouteResult routeResult) : ISqlRouteUnit
{
    public string DataSource { get; } = dataSource;

    public TableRouteResult RouteResult { get; } = routeResult;

    public override string ToString()
    {
        return $"{nameof(DataSource)}:{DataSource},{nameof(RouteResult)}:{RouteResult}";
    }
}

internal sealed class EmptySqlRouteUnit(string dataSource, IReadOnlyList<TableRouteResult> routeResults) : ISqlRouteUnit
{
    public string DataSource { get; } = dataSource;

    public TableRouteResult RouteResult { get; } = new TableRouteResult([]);

    public IReadOnlyList<TableRouteResult> RouteResults { get; } = routeResults;
}

internal sealed class SequenceRouteUnit(SequenceResult sequenceResult) : ISqlRouteUnit
{
    public SequenceResult SequenceResult { get; } = sequenceResult;

    public string DataSource => SequenceResult.DataSource;

    public TableRouteResult RouteResult => SequenceResult.RouteResult;
}

internal sealed class ShardingRouteResult(List<ISqlRouteUnit> routeUnits, bool isEmpty, bool isCrossDataSource, bool isCrossTable, bool existCrossTableTails)
{
    public IReadOnlyList<ISqlRouteUnit> RouteUnits { get; } = routeUnits;

    public bool IsCrossDataSource { get; } = isCrossDataSource;

    public bool IsCrossTable { get; } = isCrossTable;

    public bool ExistCrossTableTails { get; } = existCrossTableTails;

    public bool IsEmpty { get; } = isEmpty;

    public override string ToString()
    {
        return RouteUnits.JoinStr();
    }
}
