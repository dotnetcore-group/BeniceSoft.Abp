namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IRouteQueryResult
{
    bool HasResult { get; }
}

public class RouteQueryResult<T> : IRouteQueryResult
{
    public RouteQueryResult(string? dataSource, TableRouteResult? tableRouteResult, T? result)
    {
        DataSource = dataSource;
        TableRouteResult = tableRouteResult;
        Result = result!;
        HasResult = result != null;
    }

    public RouteQueryResult(string? dataSource, TableRouteResult? tableRouteResult, T? result, bool hasValue)
    {
        HasResult = hasValue;
        DataSource = dataSource;
        TableRouteResult = tableRouteResult;
        Result = result!;
    }

    public string? DataSource { get; }

    public TableRouteResult? TableRouteResult { get; }

    public T Result { get; }

    public bool HasResult { get; }
}
