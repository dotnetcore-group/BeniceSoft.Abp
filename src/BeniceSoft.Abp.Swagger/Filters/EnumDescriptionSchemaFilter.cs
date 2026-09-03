using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using BeniceSoft.Core.Reflector;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BeniceSoft.Abp.Swagger.Filters;

/// <summary>
/// 枚举描述 Schema 过滤器
/// 将枚举的 Description/Display 特性值添加到 Swagger 文档中
/// </summary>
public class EnumDescriptionSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concreteSchema)
            return;

        var type = context.Type;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = Nullable.GetUnderlyingType(type)!;
        }

        if (!type.IsEnum)
            return;

        var enumDescriptions = new List<string>();
        var enumNamesArray = new JsonArray();

        foreach (var enumValue in Enum.GetValues(type))
        {
            var enumName = enumValue.ToString()!;
            var enumIntValue = Convert.ToInt32(enumValue);

            var fieldInfo = type.GetField(enumName);
            var description = GetEnumDescription(fieldInfo, enumName);

            enumDescriptions.Add($"{enumIntValue} = {enumName} ({description})");
            enumNamesArray.Add(JsonValue.Create(enumName));
        }

        var enumDesc = string.Join("<br/>", enumDescriptions);
        concreteSchema.Description = string.IsNullOrEmpty(concreteSchema.Description)
            ? enumDesc
            : $"{concreteSchema.Description}<br/><br/>{enumDesc}";

        concreteSchema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        if (!concreteSchema.Extensions.ContainsKey("x-enumNames"))
        {
            concreteSchema.Extensions.Add("x-enumNames", new JsonNodeExtension(enumNamesArray));
        }
    }

    /// <summary>
    /// 获取枚举字段的描述
    /// </summary>
    private static string GetEnumDescription(FieldInfo? fieldInfo, string defaultValue)
    {
        if (fieldInfo is null) return defaultValue;

        var descAttr = fieldInfo.GetReflector().GetCustomAttribute<DescriptionAttribute>();
        if (descAttr is not null && !string.IsNullOrEmpty(descAttr.Description))
        {
            return descAttr.Description;
        }

        var displayAttr = fieldInfo.GetReflector().GetCustomAttribute<System.ComponentModel.DataAnnotations.DisplayAttribute>();
        if (displayAttr is not null && !string.IsNullOrEmpty(displayAttr.Name))
        {
            return displayAttr.Name;
        }

        return defaultValue;
    }
}

