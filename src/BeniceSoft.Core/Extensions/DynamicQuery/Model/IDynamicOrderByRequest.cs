namespace BeniceSoft.Extensions.DynamicQuery;

public interface IDynamicOrderByRequest
{
    List<DynamicOrderBy>? OrderBys { get; set; }
}
