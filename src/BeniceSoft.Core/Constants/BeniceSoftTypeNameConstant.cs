namespace BeniceSoft.Core.Constants;

public static class BeniceSoftTypeNameConstant
{
    public const string Integer = "integer";

    public const string Long = "long";

    public const string Double = "double";

    public const string Decimal = "decimal";

    public const string String = "string";

    public const string Date = "date";

    public const string DateTime = "datetime";

    public const string Boolean = "boolean";

    public const string Guid = "guid";

    /// <summary>
    /// 全部受支持的字段数据类型常量
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Integer,
        Long,
        Double,
        Decimal,
        String,
        Date,
        DateTime,
        Boolean,
        Guid,
    ];

    private static readonly HashSet<string> KnownTypes = new(All, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> DescriptionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [Integer] = "整数",
        [Long] = "长整数",
        [Double] = "浮点数",
        [Decimal] = "精确小数",
        [String] = "字符串",
        [Date] = "日期",
        [DateTime] = "日期时间",
        [Boolean] = "布尔",
        [Guid] = "GUID",
    };

    /// <summary>
    /// 是否为受支持的字段数据类型
    /// </summary>
    public static bool IsKnown(string? typeName)
    {
        return !string.IsNullOrWhiteSpace(typeName) && KnownTypes.Contains(typeName.Trim());
    }

    /// <summary>
    /// 规范化为小写常量值；不受支持时返回 null
    /// </summary>
    public static string? Normalize(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        var normalized = typeName.Trim().ToLowerInvariant();
        return KnownTypes.Contains(normalized) ? normalized : null;
    }

    /// <summary>
    /// 获取类型中文描述；未知类型时回退为原值
    /// </summary>
    public static string GetDescription(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return string.Empty;
        }

        return DescriptionMap.TryGetValue(typeName.Trim(), out var desc) ? desc : typeName.Trim();
    }
}
