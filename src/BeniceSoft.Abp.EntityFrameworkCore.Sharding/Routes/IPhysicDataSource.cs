using System.Collections.Concurrent;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public interface IPhysicDataSource
{
    /// <summary>
    /// data source name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 数据源链接
    /// </summary>
    string ConnectionString { get; }

    /// <summary>
    /// 是否是默认的数据源
    /// </summary>
    bool IsDefault { get; }
}

internal sealed class PhysicDataSource(string name, string connectionString, bool isDefault) : IPhysicDataSource
{
    public string Name { get; } = name;

    public string ConnectionString { get; } = connectionString;

    public bool IsDefault { get; } = isDefault;

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        if (obj is PhysicDataSource other)
        {
            return other.Name == Name;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return Name != null ? Name.GetHashCode() : 0;
    }
}

public interface IPhysicDataSourcePool
{
    /// <summary>
    /// 添加一个物理数据源
    /// </summary>
    /// <param name="dataSource"></param>
    /// <returns></returns>
    bool TryAdd(IPhysicDataSource dataSource);

    /// <summary>
    /// 尝试获取一个物理数据源没有返回null
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    IPhysicDataSource? TryGet(string name);

    /// <summary>
    /// 获取所有的数据源名称
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<string> GetAllDataSource();

    IReadOnlyDictionary<string, string> GetDataSource();
}

internal sealed class PhysicDataSourcePool : IPhysicDataSourcePool
{
    private readonly ConcurrentDictionary<string, IPhysicDataSource> _data = new();

    public IReadOnlyList<string> GetAllDataSource()
    {
        return [.. _data.Keys];
    }

    public IReadOnlyDictionary<string, string> GetDataSource()
    {
        return _data.ToDictionary(d => d.Key, d => d.Value.ConnectionString);
    }

    public bool TryAdd(IPhysicDataSource dataSource)
    {
        return _data.TryAdd(dataSource.Name, dataSource);
    }

    public IPhysicDataSource? TryGet(string name)
    {
        if (_data.TryGetValue(name, out var physicDataSource))
        {
            return physicDataSource;
        }

        return null;
    }
}
