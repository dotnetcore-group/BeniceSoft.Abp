using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

/// <summary>
/// 用来实现DbContext的创建,将RouteTail和DbContextOptions封装到一起
/// </summary>
public class ShardingDbContextOptions(DbContextOptions dbContextOptions, IRouteTail routeTail)
{

    /// <summary>
    /// 用来告诉ShardingCore创建的DbContext是什么后缀
    /// </summary>
    public IRouteTail RouteTail { get; } = routeTail;

    /// <summary>
    /// 用来创建DbContext
    /// </summary>
    public DbContextOptions DbContextOptions { get; } = dbContextOptions;
}
