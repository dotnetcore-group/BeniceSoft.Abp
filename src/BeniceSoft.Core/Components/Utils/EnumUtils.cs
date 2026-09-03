using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BeniceSoft.Core;

public static class EnumUtils
{
    public static string GetFullName(this Enum value)
    {
        var enumType = value.GetType();
        var enumStringValue = value.ToString("F");
        return $"{enumType.FullName}.{enumStringValue}";
    }

    public static string Description(this Enum aim)
    {
        var attr = aim.GetType().GetField(aim.ToString())?.GetCustomAttribute<DescriptionAttribute>();
        return attr?.Description ?? "";
    }

    public static string Display(this Enum aim)
    {
        var attr = aim.GetType().GetField(aim.ToString())?.GetCustomAttribute<DisplayAttribute>();
        return attr?.Name ?? "";
    }

    public static byte ToByte(this Enum aim)
    {
        return Convert.ToByte(aim);
    }

    public static short ToInt16(this Enum aim)
    {
        return Convert.ToInt16(aim);
    }

    public static int ToInt32(this Enum aim)
    {
        return Convert.ToInt32(aim);
    }

    public static long ToInt64(this Enum aim)
    {
        return Convert.ToInt64(aim);
    }

    public static T ToEnum<T>(this byte aim)
        where T : struct, Enum
    {
        var result = (T)Enum.ToObject(typeof(T), aim);
        return result;
    }

    public static T ToEnum<T>(this short aim)
        where T : struct, Enum
    {
        var result = (T)Enum.ToObject(typeof(T), aim);
        return result;
    }

    public static T ToEnum<T>(this int aim)
        where T : struct, Enum
    {
        var result = (T)Enum.ToObject(typeof(T), aim);
        return result;
    }

    public static T ToEnum<T>(this long aim)
        where T : struct, Enum
    {
        var result = (T)Enum.ToObject(typeof(T), aim);
        return result;
    }

    public static T ToEnum<T>(this string aim, bool ignoreCase = true, T defaultValue = default)
        where T : struct, Enum
    {
        if (Enum.TryParse<T>(aim, ignoreCase, out var result))
        {
            return result;
        }
        else
        {
            return defaultValue;
        }
    }

    public static T ToEnum<T>(this Enum aim)
        where T : struct, Enum
    {
        var result = aim.ToInt32().ToEnum<T>();
        return result;
    }
}
