using BeniceSoft.Core.Strategy;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal sealed class PropertySorting(string expression, SortDirection direction, Type? ownerType)
{
    public string Expression { get; set; } = expression;

    public SortDirection Direction { get; set; } = direction;

    public Type? OwnerType { get; } = ownerType;

    public override string ToString()
    {
        return $"{Expression} {(Direction == SortDirection.Ascending ? "asc" : "desc")}";
    }
}
