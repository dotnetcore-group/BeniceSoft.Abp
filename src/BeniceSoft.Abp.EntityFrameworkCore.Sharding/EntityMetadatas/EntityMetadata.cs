using BeniceSoft.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

public class EntityMetadata(Type entityType)
{
    #region Members
    private const string QueryFilter = "QueryFilter";

    /// <summary>
    /// 分表类型
    /// </summary>
    public Type EntityType { get; } = entityType ?? throw new ArgumentNullException(nameof(entityType));

    /// <summary>
    /// 分库字段
    /// </summary>
    public PropertyInfo? DataSourceProperty { get; private set; }

    /// <summary>
    /// 分库所有字段包括DataSourceProperty
    /// </summary>
    public IDictionary<string, PropertyInfo> DataSourceProperties { get; } = new Dictionary<string, PropertyInfo>();

    /// <summary>
    /// 是否分库
    /// </summary>
    public bool ShardingDataSource => DataSourceProperty != null;

    /// <summary>
    /// 启动创建数据库
    /// </summary>
    public bool? AutoCreateDataSource { get; set; }

    /// <summary>
    /// 分表字段
    /// </summary>
    public PropertyInfo? TableProperty { get; private set; }

    /// <summary>
    /// 分表所有字段包括TableProperty
    /// </summary>
    public IDictionary<string, PropertyInfo> TableProperties { get; } = new Dictionary<string, PropertyInfo>();

    /// <summary>
    /// 是否分表
    /// </summary>
    public bool ShardingTable => TableProperty != null;

    /// <summary>
    /// 自动创建表
    /// </summary>
    public bool? AutoCreateTable { get; set; }

    /// <summary>
    /// 分表隔离器 table sharding tail prefix
    /// </summary>
    public string TableSeparator { get; set; } = "_";

    /// <summary>
    /// 逻辑表名
    /// </summary>
    public string? LogicTableName { get; private set; }

    public bool IsView { get; private set; }

    /// <summary>
    /// 对象表所属schema
    /// </summary>
    public string? Schema { get; private set; }

    /// <summary>
    /// 查询过滤
    /// </summary>
    public LambdaExpression? QueryFilterExpression { get; private set; }

    /// <summary>
    /// 主键
    /// </summary>
    public IReadOnlyList<PropertyInfo> PrimaryKeys { get; private set; } = [];

    /// <summary>
    /// 是否单主键
    /// </summary>
    public bool SinglePrimaryKey { get; private set; }
    #endregion

    #region Methods
    public void SetEntityType(IEntityType entityType)
    {
        Schema = entityType.GetSchema();
        LogicTableName = entityType.GetTableName();
        if (LogicTableName.IsNull())
        {
            var viewName = entityType.GetViewName();
            IsView = viewName.IsNotNull();
            if (IsView)
            {
                LogicTableName = viewName;
                Schema = entityType.GetViewSchema();
            }
        }

        QueryFilterExpression = entityType.GetAnnotations().FirstOrDefault(o => o.Name == QueryFilter)?.Value as LambdaExpression;
        PrimaryKeys = entityType.FindPrimaryKey()?.Properties
            .Select(o => o.PropertyInfo)
            .Where(o => o is not null)
            .Cast<PropertyInfo>()
            .ToList() ?? [];
        SinglePrimaryKey = PrimaryKeys.Count == 1;
    }

    /// <summary>
    /// 设置分库字段
    /// </summary>
    /// <param name="propertyInfo"></param>
    public void SetDataSourceProperty(PropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        if (DataSourceProperties.ContainsKey(propertyInfo.Name))
        {
            throw new ShardingAccessException($"same sharding data source property name:[{propertyInfo.Name}] don't repeat add");
        }

        DataSourceProperty = propertyInfo;
        DataSourceProperties.Add(propertyInfo.Name, propertyInfo);
    }

    /// <summary>
    /// 添加额外的分库字段
    /// </summary>
    /// <param name="propertyInfo"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ShardingAccessException"></exception>
    public void AddDataSourceProperty(PropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        if (DataSourceProperties.ContainsKey(propertyInfo.Name))
        {
            throw new ShardingAccessException($"same sharding data source property name:[{propertyInfo.Name}] don't repeat add");
        }

        DataSourceProperties.Add(propertyInfo.Name, propertyInfo);
    }

    /// <summary>
    /// 设置分表字段
    /// </summary>
    /// <param name="propertyInfo"></param>
    public void SetTableProperty(PropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        if (DataSourceProperties.ContainsKey(propertyInfo.Name))
        {
            throw new ShardingAccessException($"same sharding table property name:[{propertyInfo.Name}] don't repeat add");
        }

        TableProperty = propertyInfo;
        TableProperties.Add(propertyInfo.Name, propertyInfo);
    }

    /// <summary>
    /// 添加额外的分表字段
    /// </summary>
    /// <param name="propertyInfo"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ShardingAccessException"></exception>
    public void AddTableProperty(PropertyInfo propertyInfo)
    {
        ArgumentNullException.ThrowIfNull(propertyInfo);

        if (DataSourceProperties.ContainsKey(propertyInfo.Name))
        {
            throw new ShardingAccessException($"same sharding table property name:[{propertyInfo.Name}] don't repeat add");
        }

        TableProperties.Add(propertyInfo.Name, propertyInfo);
    }

    /// <summary>
    /// 检查是否分库
    /// </summary>
    /// <exception cref="ShardingException"></exception>
    public void CheckShardingDataSource()
    {
        if (!ShardingDataSource)
        {
            throw new ShardingException($"not found entity:{EntityType} configure");
        }
    }

    /// <summary>
    /// 检查是否分表
    /// </summary>
    /// <exception cref="ShardingException"></exception>
    public void CheckShardingTable()
    {
        if (!ShardingTable)
        {
            throw new ShardingException($"not found entity:{EntityType} configure");
        }
    }

    /// <summary>
    /// 检查对象是否完整
    /// </summary>
    /// <exception cref="ShardingException"></exception>
    public void CheckMetadata()
    {
        if (!ShardingDataSource && !ShardingTable)
        {
            throw new ShardingException($"not found  entity:{EntityType} configure");
        }
    }

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

        return Equals(EntityType, ((EntityMetadata)obj).EntityType);
    }

    public override int GetHashCode()
    {
        return EntityType != null ? EntityType.GetHashCode() : 0;
    }
    #endregion
}
