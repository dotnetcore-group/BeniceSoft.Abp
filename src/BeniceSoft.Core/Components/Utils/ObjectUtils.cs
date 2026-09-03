using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using BeniceSoft.Core.Reflector;

namespace BeniceSoft.Core;

public static class ObjectUtils
{
    #region Common
    public static object? ConvertObject(object aim, Type type)
    {
        if (type == null)
        {
            return aim;
        }

        if (aim.GetType() == type)
        {
            return aim;
        }

        if (aim == null)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        var underlyingType = type.GetUnderlyingType();
        var aimType = aim.GetType();
        if (type.IsAssignableFrom(aimType))
        {
            return aim;
        }

        if (underlyingType.IsEnum)
        {
            if (Enum.TryParse(underlyingType, aim.ToString(), true, out var result))
            {
                return result;
            }

            return null;
        }

        if (typeof(IConvertible).IsAssignableFrom(underlyingType))
        {
            return Convert.ChangeType(aim, underlyingType, null);
        }

        var converter = TypeDescriptor.GetConverter(type);
        if (converter.CanConvertFrom(aimType))
        {
            return converter.ConvertFrom(aim);
        }

        var constructor = type.GetConstructor(Type.EmptyTypes);
        if (constructor != null)
        {
            var o = constructor.GetReflector().Invoke(null!);
            var propertys = type.GetProperties();
            foreach (var property in propertys)
            {
                var p = aimType.GetProperty(property.Name);
                if (property.CanWrite && p != null && p.CanRead)
                {
                    property.GetReflector().SetValue(o!, ConvertObject(p.GetReflector().GetValue(aim)!, property.PropertyType));
                }
            }

            return o;
        }

        return aim;
    }

    public static T? ConvertObject<T>(object aim)
    {
        var result = ConvertObject(aim, typeof(T));
        if (result == null)
        {
            return default;
        }

        return (T)result;
    }

    public static T ParseOptions<T>(string connectionString, bool ignoreCase = true, bool ignoreUnknown = true, string splitChars = ",;")
        where T : class, new()
    {
        var options = new T();
        var props = options.GetType().GetProperties().FindAll(t => t.CanWrite);
        var list = new List<(OptionsMode Mode, HashSet<string> Keys, PropertyInfo Property)>();
        var compose = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        foreach (var prop in props)
        {
            if (prop.IsDefined<OptionsIgnoreAttribute>())
            {
                continue;
            }

            var attr = prop.GetCustomAttribute<OptionsAttribute>();
            var keys = new HashSet<string>(compose);
            var mode = OptionsMode.None;
            if (attr != null)
            {
                attr.Keys.ForEach(k => keys.Add(k));
                mode = attr.Mode;

                if (attr.OnlySupport)
                {
                    list.Add((mode, keys, prop));
                    continue;
                }
            }

            keys.Add(prop.Name);
            list.Add((mode, keys, prop));
        }

        var arr = connectionString.Split(splitChars.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

        foreach (var paddedOption in arr)
        {
            var option = paddedOption.Trim();
            if (option.IsNull())
            {
                continue;
            }

            var idx = option.IndexOf('=');
            if (idx < 0)
            {
                var host = option;
                var itemPort = list.Find(t => t.Mode == OptionsMode.Port);
                var port = 0;
                if (itemPort != default)
                {
                    var ptx = option.LastIndexOf(':');
                    if (ptx > 0)
                    {
                        port = option[(ptx + 1)..].ToInt32();
                        if (port > 0)
                        {
                            host = option[..ptx];
                        }
                    }
                }

                if (port > 0)
                {
                    if (itemPort == default)
                    {
                        if (!ignoreUnknown)
                        {
                            throw new ArgumentException($"Port not found.");
                        }

                        continue;
                    }

                    itemPort.Property.SetValue(options, ObjectUtils.ConvertObject(port, itemPort.Property.PropertyType));
                }

                var itemHost = list.Find(t => t.Mode == OptionsMode.Host);
                if (itemHost == default)
                {
                    if (!ignoreUnknown)
                    {
                        throw new ArgumentException($"Host not found.");
                    }

                    continue;
                }

                itemHost.Property.SetValue(options, ObjectUtils.ConvertObject(host, itemHost.Property.PropertyType));

                continue;
            }

            var key = option[..idx].Trim();
            var value = option[(idx + 1)..].Trim();
            if (key.IsNull())
            {
                continue;
            }

            var item = list.Find(t => t.Keys.IsNotNull() && t.Keys.Contains(key, compose));
            if (item == default)
            {
                if (!ignoreUnknown)
                {
                    throw new ArgumentException($"Keyword '{key}' is not supported.");
                }

                continue;
            }

            item.Property.SetValue(options, ObjectUtils.ConvertObject(value, item.Property.PropertyType));
        }

        return options;
    }
    #endregion

    #region Extensions
    #region Convertsion
    /// <summary>
    /// convert to string
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static string ToStringSafe(this object? aim, string defaultValue = "")
    {
        if (aim == null || aim == DBNull.Value)
        {
            return defaultValue;
        }
        else
        {
            return aim + string.Empty;
        }
    }

    /// <summary>
    /// convert to int8
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static byte ToByte(this short aim, byte defaultValue = 0)
    {
        if (aim is <= byte.MaxValue and >= byte.MinValue)
        {
            return Convert.ToByte(aim);
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to int8
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static byte ToByte(this int aim, byte defaultValue = 0)
    {
        if (aim is <= byte.MaxValue and >= byte.MinValue)
        {
            return Convert.ToByte(aim);
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to int16
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static short ToInt16(this byte aim)
    {
        return Convert.ToInt16(aim);
    }

    /// <summary>
    /// convert to int16
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static short ToInt16(this int aim, short defaultValue = 0)
    {
        if (aim is <= short.MaxValue and >= short.MinValue)
        {
            return Convert.ToInt16(aim);
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to int16
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static short ToInt16(this long aim, short defaultValue = 0)
    {
        if (aim is <= short.MaxValue and >= short.MinValue)
        {
            return Convert.ToInt16(aim);
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to int32
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static int ToInt32(this byte aim)
    {
        return Convert.ToInt32(aim);
    }

    /// <summary>
    /// convert to int32
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static int ToInt32(this short aim)
    {
        return Convert.ToInt32(aim);
    }

    /// <summary>
    /// convert to int32
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static int ToInt32(this long aim, int defaultValue = 0)
    {
        if (aim is <= int.MaxValue and >= int.MinValue)
        {
            return Convert.ToInt32(aim);
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to int32
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static int ToInt32(this decimal aim, int defaultValue = 0)
    {
        if (aim is <= int.MaxValue and >= int.MinValue)
        {
            return decimal.ToInt32(aim);
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to int32
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static int ToInt32(this float aim, int defaultValue = 0)
    {
        if (aim is <= int.MaxValue and >= int.MinValue)
        {
            return Convert.ToInt32(aim);
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to int32
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static int ToInt32(this double aim, int defaultValue = 0)
    {
        if (aim is <= int.MaxValue and >= int.MinValue)
        {
            return Convert.ToInt32(aim);
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to int64
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static long ToInt64(this short aim)
    {
        return Convert.ToInt64(aim);
    }

    /// <summary>
    /// convert to int64
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static long ToInt64(this int aim)
    {
        return Convert.ToInt64(aim);
    }

    /// <summary>
    /// convert to int64
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static long ToInt64(this decimal aim, int defaultValue = 0)
    {
        if (aim is <= long.MaxValue and >= long.MinValue)
        {
            return Convert.ToInt64(aim);
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to int64
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static long ToInt64(this float aim, int defaultValue = 0)
    {
        if (aim is <= long.MaxValue and >= long.MinValue)
        {
            return Convert.ToInt64(aim);
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to int64
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static long ToInt64(this double aim, int defaultValue = 0)
    {
        if (aim is <= long.MaxValue and >= long.MinValue)
        {
            return Convert.ToInt64(aim);
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to float(注意Float精确度问题，谨顺使用此类型)
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static float ToSingle(this int aim)
    {
        return Convert.ToSingle(aim);
    }

    /// <summary>
    /// convert to float(注意Float精确度问题，谨顺使用此类型)
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static float ToSingle(this long aim)
    {
        return Convert.ToSingle(aim);
    }

    /// <summary>
    /// convert to float(注意Float精确度问题，谨顺使用此类型)
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static float ToSingle(this double aim)
    {
        return Convert.ToSingle(aim);
    }

    /// <summary>
    /// convert to float(注意Float精确度问题，谨顺使用此类型)
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static float ToSingle(this decimal aim)
    {
        return Convert.ToSingle(aim);
    }

    /// <summary>
    /// convert to double
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static double ToDouble(this int aim)
    {
        return Convert.ToDouble(aim);
    }

    /// <summary>
    /// convert to double
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static double ToDouble(this long aim)
    {
        return Convert.ToDouble(aim);
    }

    /// <summary>
    /// convert to double
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static double ToDouble(this decimal aim)
    {
        return Convert.ToDouble(aim);
    }

    /// <summary>
    /// convert to specified number of double
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="digits"></param>
    /// <returns></returns>
    public static double ToDouble(this decimal aim, int digits)
    {
        return Math.Round(aim.ToDouble(), digits);
    }

    /// <summary>
    /// convert to double
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static double ToDouble(this float aim)
    {
        return Convert.ToDouble(aim);
    }

    /// <summary>
    /// convert to specified number of double
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="digits"></param>
    /// <returns></returns>
    public static double ToDouble(this float aim, int digits)
    {
        return Math.Round(aim, digits);
    }

    /// <summary>
    /// convert to specified number of double
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="digits"></param>
    /// <returns></returns>
    public static double ToDouble(this double aim, int digits)
    {
        return Math.Round(aim, digits);
    }

    /// <summary>
    /// convert to decimal
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static decimal ToDecimal(this int aim)
    {
        return Convert.ToDecimal(aim);
    }

    /// <summary>
    /// convert to decimal
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static decimal ToDecimal(this long aim)
    {
        return Convert.ToDecimal(aim);
    }

    /// <summary>
    /// convert to specified number of decimal
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="digits"></param>
    /// <returns></returns>
    public static decimal ToDecimal(this decimal aim, int digits)
    {
        return Math.Round(aim, digits);
    }

    /// <summary>
    /// convert to decimal
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static decimal ToDecimal(this float aim, decimal defaultValue = 0)
    {
        if (aim <= decimal.MaxValue.ToSingle() && aim >= decimal.MinValue.ToSingle())
        {
            return Convert.ToDecimal(aim);
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to specified number of decimal
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="digits"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static decimal ToDecimal(this float aim, int digits, decimal defaultValue = 0)
    {
        return Math.Round(aim.ToDecimal(defaultValue), digits);
    }

    /// <summary>
    /// convert to decimal
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static decimal ToDecimal(this double aim)
    {
        return Convert.ToDecimal(aim);
    }

    /// <summary>
    /// convert to specified number of decimal
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="digits"></param>
    /// <returns></returns>
    public static decimal ToDecimal(this double aim, int digits)
    {
        return Math.Round(aim.ToDecimal(), digits);
    }

    public static void ReThrow(this Exception exception)
    {
        if (exception == null)
        {
            return;
        }

        ExceptionDispatchInfo.Capture(exception).Throw();
    }
    #endregion

    #region Compare
    /// <summary>
    /// determine whether it is in the array
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <param name="list"></param>
    /// <returns></returns>
    [InjectLambda(typeof(InjectLambdaExtensions))]
    public static bool In<T>(this T aim, params T[] list)
    {
        if (list.IsNull())
        {
            return false;
        }

        return list.Contains(aim);
    }

    [InjectLambda(typeof(InjectLambdaExtensions))]
    public static bool NotIn<T>(this T aim, params T[] list)
    {
        return !aim.In(list);
    }

    /// <summary>
    /// determine whether it is in the array
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="aim"></param>
    /// <param name="comparer"></param>
    /// <param name="list"></param>
    /// <returns></returns>
    public static bool In<T>(this T aim, IEqualityComparer<T> comparer, params T[] list)
    {
        if (list.IsNull())
        {
            return false;
        }

        return list.Contains(aim, comparer);
    }

    public static bool NotIn<T>(this T aim, IEqualityComparer<T> comparer, params T[] list)
    {
        return !aim.In(comparer, list);
    }

    /// <summary>
    /// judging that it does not have any data
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    public static bool IsNull(this Guid aim)
    {
        return aim == Guid.Empty;
    }
    #endregion

    #region Other
    public static T GetResult<T>(this Task<T> task)
    {
        return task.ConfigureAwait(false).GetAwaiter().GetResult();
    }

    /// <summary>
    /// compute hash
    /// </summary>
    /// <param name="algorithm"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    public static byte[] Hash(this HashAlgorithm algorithm, string input)
    {
        using (algorithm)
        {
            var btsHash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(input));
            return btsHash;
        }
    }

    /// <summary>
    /// hash hex string
    /// </summary>
    /// <param name="algorithm"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string HashHex(this HashAlgorithm algorithm, string input)
    {
        return Convert.ToHexString(algorithm.Hash(input));
    }

    public static async Task<string?> ReadTextAsync(this WebSocketReceiveResult result, ArraySegment<byte> buffer)
    {
        using var ms = new MemoryStream();
        ms.Write(buffer.Array!, buffer.Offset, result.Count);
        if (result.MessageType == WebSocketMessageType.Text)
        {
            ms.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(ms, Encoding.UTF8);
            var message = await reader.ReadToEndAsync();
            return message;
        }

        return default;
    }

    public static async Task SendTextAsync(this WebSocket webSocket, string message, CancellationToken cancellationToken = default)
    {
        var data = Encoding.UTF8.GetBytes(message);
        await webSocket.SendAsync(data, WebSocketMessageType.Text, true, cancellationToken);
    }

    public static T? GetValue<T>(this SerializationInfo info, string name)
    {
        return (T?)info.GetValue(name, typeof(T));
    }

    public static string GetTraceId(this Activity activity)
    {
        return activity.IdFormat switch
        {
            ActivityIdFormat.Hierarchical => activity.RootId,
            ActivityIdFormat.W3C => activity.TraceId.ToHexString(),
            _ => null,
        } ?? string.Empty;
    }

    public static string GetSpanId(this Activity activity)
    {
        return activity.IdFormat switch
        {
            ActivityIdFormat.Hierarchical => activity.Id,
            ActivityIdFormat.W3C => activity.SpanId.ToHexString(),
            _ => null,
        } ?? string.Empty;
    }

    public static string GetParentId(this Activity activity)
    {
        return activity.IdFormat switch
        {
            ActivityIdFormat.Hierarchical => activity.ParentId,
            ActivityIdFormat.W3C => activity.ParentSpanId.ToHexString(),
            _ => null,
        } ?? string.Empty;
    }
    #endregion

    #region XML
    private static readonly ConcurrentDictionary<Type, XmlSerializer> XmlSerializers = new();

    public static XmlSerializer GetXmlSerializer(Type type)
    {
        return XmlSerializers.GetOrAdd(type, t => new XmlSerializer(t));
    }

    public static string XmlSerialize<T>(this T value, XmlWriterSettings? settings = null)
    {
        settings ??= new XmlWriterSettings { Encoding = new UTF8Encoding() };
        using var ms = new MemoryStream();
        using var xmlWriter = XmlWriter.Create(ms, settings);
        var xsnp = new XmlSerializerNamespaces();
        xsnp.Add(string.Empty, string.Empty);
        GetXmlSerializer(typeof(T)).Serialize(xmlWriter, value, xsnp);
        var rst = settings.Encoding.GetString(ms.ToArray());
        return rst;
    }

    public static T? XmlDeserialize<T>(this string value, XmlReaderSettings? settings = null)
    {
        settings ??= new XmlReaderSettings();
        using var sr = new StringReader(value);
        using var reader = XmlReader.Create(sr, settings);
        return (T?)GetXmlSerializer(typeof(T)).Deserialize(reader);
    }

    public static T? XmlDeserialize<T>(this Uri uri, XmlReaderSettings? settings = null)
    {
        settings ??= new XmlReaderSettings();
        using var reader = XmlReader.Create(uri.ToString(), settings);
        return (T?)GetXmlSerializer(typeof(T)).Deserialize(reader);
    }
    #endregion

    #endregion
}

[AttributeUsage(AttributeTargets.Property)]
public class OptionsAttribute(params string[] keys) : Attribute
{
    /// <summary>
    /// 支持的关键词
    /// </summary>
    public string[] Keys { get; set; } = keys;

    /// <summary>
    /// 只和当前Keys比较,忽略属性名称
    /// </summary>
    public bool OnlySupport { get; set; }

    /// <summary>
    /// 0：表示常规属性，1
    /// </summary>
    public OptionsMode Mode { get; set; } = OptionsMode.None;
}

public enum OptionsMode
{
    /// <summary>
    /// 常规属性
    /// </summary>
    None = 0,

    /// <summary>
    /// host
    /// </summary>
    Host = 1,

    /// <summary>
    /// port
    /// </summary>
    Port = 2
}

[AttributeUsage(AttributeTargets.Property)]
public class OptionsIgnoreAttribute : Attribute
{
}