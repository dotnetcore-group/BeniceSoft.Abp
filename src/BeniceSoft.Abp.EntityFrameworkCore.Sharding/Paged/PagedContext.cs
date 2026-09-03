namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal sealed class PagedContext
{
    public int? Skip { get; private set; }

    public int? Take { get; private set; }

    public void SetSkip(int skip)
    {
        if (Skip.HasValue)
        {
            throw new ShardingNotSupportException(nameof(Skip));
        }

        Skip = skip;
    }

    public void SetTake(int take)
    {
        if (Take.HasValue)
        {
            throw new ShardingNotSupportException(nameof(Take));
        }

        Take = take;
    }

    public void ReplaceTake(int take)
    {
        Take = take;
    }

    public override string ToString()
    {
        return $"{nameof(Skip)}: {Skip},  {nameof(Take)}: {Take}";
    }
}

public sealed class ShardingPageContext
{
    public ICollection<RouteQueryResult<long>> Results { get; } = new LinkedList<RouteQueryResult<long>>();
}
