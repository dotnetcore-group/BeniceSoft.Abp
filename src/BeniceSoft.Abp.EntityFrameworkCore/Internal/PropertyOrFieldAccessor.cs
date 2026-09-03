using BeniceSoft.Core;
using BeniceSoft.Core.Reflector;
using System.Collections.ObjectModel;
using System.Reflection;

namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>
/// 表达式成员访问路径（属性/字段链），供 InternalExtensions 与后续 Audit 等能力使用。
/// </summary>
internal sealed class PropertyOrFieldAccessor
{
    public ReadOnlyCollection<MemberInfo> PropertyOrFieldPaths { get; internal set; }

    public MemberInfo? PropertyOrField { get; set; }

    public PropertyOrFieldAccessor(ReadOnlyCollection<MemberInfo> propertyOrFieldPaths)
    {
        PropertyOrFieldPaths = propertyOrFieldPaths;
        PropertyOrField = propertyOrFieldPaths.LastOrDefault();
    }

    public PropertyOrFieldAccessor(MemberInfo? property)
    {
        if (property != null)
        {
            PropertyOrFieldPaths = new ReadOnlyCollection<MemberInfo>([property]);
            PropertyOrField = property;
        }
        else
        {
            PropertyOrFieldPaths = new ReadOnlyCollection<MemberInfo>([]);
        }
    }

    public object? GetValue(object obj)
    {
        object? value = obj;
        foreach (var item in PropertyOrFieldPaths)
        {
            if (item is PropertyInfo property)
            {
                value = property.GetReflector().GetValue(value!);
            }
            else if (item is FieldInfo field)
            {
                value = field.GetReflector().GetValue(value!);
            }
        }

        return value;
    }

    public void SetValue(object obj, object? value)
    {
        for (var i = 0; i < PropertyOrFieldPaths.Count; i++)
        {
            var item = PropertyOrFieldPaths[i];
            if (i == PropertyOrFieldPaths.Count - 1)
            {
                if (item is PropertyInfo property)
                {
                    property.GetReflector().SetValue(obj, value);
                }
                else if (item is FieldInfo field)
                {
                    field.GetReflector().SetValue(obj, value);
                }
            }
            else
            {
                if (item is PropertyInfo property)
                {
                    obj = property.GetReflector().GetValue(obj)!;
                }
                else if (item is FieldInfo field)
                {
                    obj = field.GetReflector().GetValue(obj)!;
                }
            }
        }
    }

    public override string ToString()
        => PropertyOrFieldPaths.Select(x => x.Name).JoinStr(".");
}
