using BeniceSoft.Core;
using BeniceSoft.Core.Reflector;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IDbContextCreator
{
    /// <summary>
    /// 创建Dbcontext
    /// </summary>
    /// <param name="shellDbContext">最外部的DbContext也就是壳不具备真正的执行</param>
    /// <param name="shardingDbContextOptions">返回DbContext的配置路由等信息</param>
    /// <returns></returns>
    DbContext Create(DbContext shellDbContext, ShardingDbContextOptions shardingDbContextOptions);

    /// <summary>
    /// 返回ShellDbContext 框架如何获取DbContext
    /// </summary>
    /// <param name="shardingProvider"></param>
    /// <returns></returns>
    DbContext GetShell(IShardingProvider shardingProvider);
}

internal sealed class DbContextCreator<T> : IDbContextCreator
    where T : DbContext, IShardingDbContext
{
    public DbContext Create(DbContext shellDbContext, ShardingDbContextOptions shardingDbContextOptions)
    {
        var ctx = Create().Invoke(shardingDbContextOptions);
        if (ctx is IShardingTableDbContext shardingTableDbContext)
        {
            shardingTableDbContext.RouteTail ??= shardingDbContextOptions.RouteTail;
        }

        // 物理上下文由 new 创建，不经 ABP DI；共享壳的 LazyServiceProvider，以便 SaveChanges 审计/过滤器可用
        ShareAbpLazyServiceProvider(shellDbContext, ctx);

        _ = ctx.Model;
        return ctx;
    }

    private static void ShareAbpLazyServiceProvider(DbContext shell, DbContext physical)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        var prop = shell.GetType().GetProperty("LazyServiceProvider", flags);
        if (prop is null || !prop.CanRead)
        {
            return;
        }

        var value = prop.GetValue(shell);
        if (value is null)
        {
            return;
        }

        var target = physical.GetType().GetProperty("LazyServiceProvider", flags);
        if (target is not null && target.CanWrite)
        {
            target.SetValue(physical, value);
        }
    }

    public DbContext GetShell(IShardingProvider shardingProvider)
    {
        try
        {
            return shardingProvider.GetService<T>()!;
        }
        catch (Exception ex)
        {
            throw new ShardingInvalidOperationException($"cant get shell db context,plz override {nameof(IDbContextCreator)}.{nameof(IDbContextCreator.GetShell)}", ex);
        }
    }

    private static Func<ShardingDbContextOptions, DbContext> Create()
    {
        var constructors = typeof(T).DeclaredConstructors(c => !c.IsStatic && c.IsPublic).ToArray();

        var parameters = constructors[0].GetParameters();
        var parameterType = parameters[0].ParameterType;

        if (parameterType == typeof(ShardingDbContextOptions))
        {
            return CreateShardingDbContextOptions(constructors[0], parameterType);
        }
        else if (typeof(DbContextOptions).IsAssignableFrom(parameterType))
        {
            if (parameters[0].ParameterType != typeof(DbContextOptions)
                 && parameters[0].ParameterType != typeof(DbContextOptions<T>))
            {
                throw new ShardingException("cant create activator");
            }

            return CreateDbContextOptionsGeneric(constructors[0], parameterType);
        }

        var po = Expression.Parameter(parameterType, "o");
        var new1 = Expression.New(constructors[0], po);
        var inner = Expression.Lambda(new1, po);

        var args = Expression.Parameter(typeof(ShardingDbContextOptions), "args");
        var body = Expression.Invoke(inner, Expression.Convert(args, po.Type));
        var outer = Expression.Lambda<Func<ShardingDbContextOptions, T>>(body, args);
        var ret = outer.Compile();
        return ret;
    }

    private static Func<ShardingDbContextOptions, DbContext> CreateShardingDbContextOptions(ConstructorInfo constructor, Type paramType)
    {
        var po = Expression.Parameter(paramType, "o");
        var newExpression = Expression.New(constructor, po);
        var inner = Expression.Lambda(newExpression, po);

        var args = Expression.Parameter(typeof(ShardingDbContextOptions), "args");
        var body = Expression.Invoke(inner, Expression.Convert(args, po.Type));
        var outer = Expression.Lambda<Func<ShardingDbContextOptions, T>>(body, args);
        var ret = outer.Compile();
        return ret;
    }

    private static Func<ShardingDbContextOptions, DbContext> CreateDbContextOptionsGeneric(ConstructorInfo constructor, Type paramType)
    {
        var parameterExpression = Expression.Parameter(typeof(ShardingDbContextOptions), "o");
        //o.DbContextOptions
        var paramMemberExpression = Expression.Property(parameterExpression, nameof(ShardingDbContextOptions.DbContextOptions));

        var newExpression = Expression.New(constructor, Expression.Convert(paramMemberExpression, paramType));

        var inner = Expression.Lambda(newExpression, parameterExpression);

        var args = Expression.Parameter(typeof(ShardingDbContextOptions), "args");
        var body = Expression.Invoke(inner, Expression.Convert(args, parameterExpression.Type));
        var outer = Expression.Lambda<Func<ShardingDbContextOptions, T>>(body, args);
        var ret = outer.Compile();
        return ret;
    }
}

public interface IRouteTailDbContextCreator
{
    /// <summary>
    /// 创建Dbcontext
    /// </summary>
    /// <param name="shellDbContext">最外部的DbContext也就是壳不具备真正的执行</param>
    /// <param name="shardingDbContextOptions">返回DbContext的配置路由等信息</param>
    /// <returns></returns>
    DbContext Create(DbContext shellDbContext, ShardingDbContextOptions shardingDbContextOptions);

    /// <summary>
    /// 返回ShellDbContext 框架如何获取DbContext
    /// </summary>
    /// <param name="shardingProvider"></param>
    /// <returns></returns>
    DbContext GetShell(IShardingProvider shardingProvider);
}

internal sealed class RouteTailDbContextCreator(IDbContextCreator creator) : IRouteTailDbContextCreator
{
    private static readonly AsyncLocal<IRouteTail?> _local = new();

    public DbContext Create(DbContext shellDbContext, ShardingDbContextOptions shardingDbContextOptions)
    {
        try
        {
            _local.Value = shardingDbContextOptions.RouteTail;
            return creator.Create(shellDbContext, shardingDbContextOptions);
        }
        finally
        {
            _local.Value = null;
        }
    }

    public DbContext GetShell(IShardingProvider shardingProvider)
    {
        return creator.GetShell(shardingProvider);
    }
}
