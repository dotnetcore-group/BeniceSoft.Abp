namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IEntityQuery<T>
     where T : class
{
    void Configure(EntityQueryBuilder<T> builder);
}
