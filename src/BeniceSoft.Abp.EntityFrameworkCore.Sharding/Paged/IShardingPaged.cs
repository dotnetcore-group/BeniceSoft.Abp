namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IShardingPaged<T>
    where T : class
{
    void Configure(PagedBuilder<T> builder);
}
