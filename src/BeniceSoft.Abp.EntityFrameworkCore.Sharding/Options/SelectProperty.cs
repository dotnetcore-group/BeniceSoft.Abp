using System.Reflection;

namespace BeniceSoft.Abp.EntityFrameworkCore.Sharding;

internal class SelectProperty(PropertyInfo property)
{
    public PropertyInfo Property { get; } = property;

    public string Name { get; } = property.Name;
}

internal class SelectOwnerProperty(Type ownerType, PropertyInfo property) : SelectProperty(property)
{
    public Type OwnerType { get; } = ownerType;

    public override string ToString()
    {
        return $"{nameof(OwnerType)}: {OwnerType}, {nameof(Property)}: {Property}, {nameof(Name)}: {Name}";
    }
}

internal class SelectAggregateProperty(string methodName, Type ownerType, PropertyInfo property) : SelectOwnerProperty(ownerType, property)
{
    public string MethodName { get; } = methodName;
}

internal sealed class SelectCountProperty(string methodName, Type ownerType, PropertyInfo property) : SelectAggregateProperty(methodName, ownerType, property)
{
}

internal sealed class SelectMaxProperty(string methodName, Type ownerType, PropertyInfo property) : SelectAggregateProperty(methodName, ownerType, property)
{
}

internal sealed class SelectMinProperty(string methodName, Type ownerType, PropertyInfo property) : SelectAggregateProperty(methodName, ownerType, property)
{
}

internal sealed class SelectSumProperty(string methodName, Type ownerType, PropertyInfo property, PropertyInfo? fromProperty) : SelectAggregateProperty(methodName, ownerType, property)
{
    public PropertyInfo? FromProperty { get; } = fromProperty;
}

internal sealed class SelectAverageProperty(string methodName, Type ownerType, PropertyInfo property, PropertyInfo? fromProperty) : SelectAggregateProperty(methodName, ownerType, property)
{

    /// <summary>
    /// 平均值是通过哪个属性获取的
    /// </summary>
    public PropertyInfo? FromProperty { get; } = fromProperty;

    /// <summary>
    /// 求数量的属性
    /// </summary>
    public PropertyInfo? CountProperty { get; private set; }

    /// <summary>
    /// 当前属性的求和属性
    /// </summary>
    public PropertyInfo? SumProperty { get; private set; }

    public void SetCountProperty(PropertyInfo countProperty)
    {
        CountProperty = countProperty;
    }

    public void SetSumProperty(PropertyInfo sumProperty)
    {
        SumProperty = sumProperty;
    }
}
