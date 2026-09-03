using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BeniceSoft.Core.Strategy;

namespace BeniceSoft.Core;

public static partial class StringUtils
{
    #region Characters Convertsion
    public static void IfNull(ref string aim, string value)
    {
        if (aim.IsNull())
        {
            aim = value;
        }
    }

    public static void IfEmpty(ref string aim, string value)
    {
        if (aim.IsEmpty())
        {
            aim = value;
        }
    }

    /// <summary>
    /// cut string to safe
    /// </summary>
    /// <param name="aim">current string</param>
    /// <param name="length">max length</param>
    /// <param name="post">whether to start cutting after completion</param>
    /// <returns></returns>
    public static string CutString(string aim, int length, bool post = false)
    {
        if (aim.IsNull())
        {
            return string.Empty;
        }

        if (aim.Length <= length)
        {
            return aim;
        }

        if (!post)
        {
            return aim[..length];
        }
        else
        {
            var index = aim.Length - length;
            return aim[index..];
        }
    }

    /// <summary>
    /// unicode to string
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string UnicodeToString(string input)
    {
        var res = input;
        var reg = MyRegex().Matches(res);
        foreach (var i in reg.Count)
        {
            res = res.Replace(reg[i].Groups[0].Value, string.Empty + Regex.Unescape(reg[i].Value.ToString()).ToString());
        }

        return res;
    }

    /// <summary>
    /// convert semiangle
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string ConvertSemiangle(string input)
    {
        input ??= string.Empty;

        var c = input.ToCharArray();
        foreach (var i in c.Length)
        {
            if (c[i] == 12288)
            {
                c[i] = (char)32;
                continue;
            }

            if (c[i] is > (char)65280 and < (char)65375)
            {
                c[i] = (char)(c[i] - 65248);
            }
        }

        return new(c);
    }

    /// <summary>
    /// convert holomorph
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string ConvertHolomorph(string input)
    {
        var c = input.ToCharArray();
        foreach (var i in c.Length)
        {
            if (c[i] == 32)
            {
                c[i] = (char)12288;
                continue;
            }

            if (c[i] < 127)
            {
                c[i] = (char)(c[i] + 65248);
            }
        }

        return new(c);
    }

    /// <summary>
    /// C#默认的字符串GetHashCode只是进程内一致如果程序关闭开启后那么就会乱掉
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static int GetHashCode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var h = 0; // 默认值是0
        if (value.Length > 0)
        {
            foreach (var i in value.Length)
            {
                h = 31 * h + value[i]; // val[0]*31^(n-1) + val[1]*31^(n-2) + ... + val[n-1]
            }
        }

        return h;
    }
    #endregion

    #region Encrypt And Decrypt
    private const string Hex36Char = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Base62Char = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private static string ObfuscateBaseChar(string key)
    {
        if (key.IsNull())
        {
            return Base62Char;
        }

        var keyBytes = SHA512.Create().Hash(key);
        var sb = new StringBuilder();
        var baseChar = Base62Char;
        while (baseChar.IsNotNull())
        {
            var index = sb.Length % keyBytes.Length;
            index = keyBytes[index] % baseChar.Length;
            sb.Append(baseChar[index]);
            baseChar = baseChar.Remove(index, 1);
        }

        return sb.ToString();
    }

    public static string Base62Encode(long value, string key = "", int length = 0, bool reverse = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 0);

        if (reverse && length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "if reversed, length must be greater than or equal to 1.");
        }

        var baseChar = ObfuscateBaseChar(key);
        var len = baseChar.Length;

        if (reverse)
        {
            var bit = Math.Pow(len, length).ToString().Length - 1;
            var num = value.ToString().PadLeft(bit, '0');
            value = num.Reverse().JoinStr(string.Empty).ToInt64();
        }

        var sb = new StringBuilder();
        do
        {
            var remainder = (int)(value % len);
            sb.Insert(0, baseChar[remainder]);
            value /= len;
        }
        while (value > 0);

        var result = sb.ToString();
        if (length > 0)
        {
            result = result.PadLeft(length, baseChar[0]);
        }

        return result;
    }

    public static long Base62Decode(string value, string key = "", bool reverse = false)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(value);

        var result = 0L;
        var multiplier = 1L;
        var baseChar = ObfuscateBaseChar(key);
        var len = baseChar.Length;
        for (var i = value.Length - 1; i >= 0; i--)
        {
            var digit = baseChar.IndexOf(value[i]);

            if (digit < 0)
            {
                throw new ArgumentException("value string contains invalid characters.", nameof(value));
            }

            result += digit * multiplier;
            multiplier *= len;
        }

        if (reverse)
        {
            var bit = Math.Pow(len, value.Length).ToString().Length - 1;
            result = result.ToString().PadLeft(bit, '0').Reverse().JoinStr(string.Empty).ToInt64();
        }

        return result;
    }

    public static string Hex36String(byte[] bytes)
    {
        var sb = new StringBuilder();
        foreach (var b in bytes)
        {
            sb.Append(Hex36Char[b / 36]);
            sb.Append(Hex36Char[b % 36]);
        }

        return sb.ToString();
    }

    public static byte[] Hex36Bytes(string input)
    {
        var len = input.Length / 2;
        var btsInput = new byte[len];

        foreach (var i in len)
        {
            var chars = input.Substring(i * 2, 2);
            var c = Hex36Char.IndexOf(chars[0], StringComparison.OrdinalIgnoreCase) * 36 + Hex36Char.IndexOf(chars[1], StringComparison.OrdinalIgnoreCase);
            btsInput[i] = (byte)c;
        }

        return btsInput;
    }

    public static string HexObfuscateString(byte[] bytes, string key)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(key);

        var keyBytes = SHA1.Create().Hash(key);
        var sb = new StringBuilder();
        foreach (var i in bytes.Length)
        {
            var c = 1295 - bytes[i] - keyBytes[i % keyBytes.Length];
            sb.Append(Hex36Char[c / 36]);
            sb.Append(Hex36Char[c % 36]);
        }

        return sb.ToString();
    }

    public static byte[] HexObfuscateBytes(string input, string key)
    {
        var len = input.Length / 2;
        var btsInput = new byte[len];
        var keyBytes = SHA1.Create().Hash(key);

        foreach (var i in len)
        {
            var chars = input.Substring(i * 2, 2);
            var c = Hex36Char.IndexOf(chars[0], StringComparison.OrdinalIgnoreCase) * 36 + Hex36Char.IndexOf(chars[1], StringComparison.OrdinalIgnoreCase);
            c = 1295 - c - keyBytes[i % keyBytes.Length];
            btsInput[i] = (byte)c;
        }

        return btsInput;
    }

    [GeneratedRegex(@"\\u\w{4}")]
    private static partial Regex MyRegex();

    /// <summary>
    /// entry string
    /// </summary>
    /// <param name="input">plaintext</param>
    /// <param name="key">secret key(support chinese)</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static string Encrypt(string input, string key)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(key);

        using var aes = Aes.Create();
        var keyBytes = Encoding.UTF8.GetBytes(key);
        aes.Key = new HMACMD5(keyBytes).Hash(key);
        aes.IV = aes.Key;

        using var encryptor = aes.CreateEncryptor();
        var buffer = Encoding.UTF8.GetBytes(input);
        var cipher = encryptor.TransformFinalBlock(buffer, 0, buffer.Length);
        var text = HexObfuscateString(cipher, key);
        var encode = new VigenereEncoding();
        return encode.Encode(text, key);
    }

    /// <summary>
    /// decrypt string
    /// </summary>
    /// <param name="input">ciphertext</param>
    /// <param name="key">secret key(support chinese)</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static string Decrypt(string input, string key)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(key);

        using var aes = Aes.Create();
        var keyBytes = Encoding.UTF8.GetBytes(key);
        aes.Key = new HMACMD5(keyBytes).Hash(key);
        aes.IV = aes.Key;
        var encode = new VigenereEncoding();
        input = encode.Decode(input, key);
        var buffer = HexObfuscateBytes(input, key);
        using var decryptor = aes.CreateDecryptor();
        var cipher = decryptor.TransformFinalBlock(buffer, 0, buffer.Length);
        return Encoding.UTF8.GetString(cipher);
    }


    #endregion

    #region Extensions
    /// <summary>
    /// indicates whether the specified string is null or an empty string ("").
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    [InjectLambda(typeof(InjectLambdaExtensions))]
    public static bool IsEmpty([NotNullWhen(false)] this string? aim)
    {
        return string.IsNullOrEmpty(aim);
    }

    [InjectLambda(typeof(InjectLambdaExtensions))]
    public static bool IsNotEmpty([NotNullWhen(true)] this string? aim)
    {
        return !aim.IsEmpty();
    }

    /// <summary>
    /// indicates whether a specified string is null, empty, or consists only of white-space characters.
    /// </summary>
    /// <param name="aim"></param>
    /// <returns></returns>
    [InjectLambda(typeof(InjectLambdaExtensions))]
    public static bool IsNull([NotNullWhen(false)] this string? aim)
    {
        return string.IsNullOrWhiteSpace(aim);
    }

    [InjectLambda(typeof(InjectLambdaExtensions))]
    public static bool IsNotNull([NotNullWhen(true)] this string? aim)
    {
        return !aim.IsNull();
    }

    /// <summary>
    /// determine whether two values are equal
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="value"></param>
    /// <param name="comparison"></param>
    /// <returns></returns>
    public static bool EqualsTo(this string aim, string value, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (aim.IsEmpty())
        {
            return value.IsEmpty();
        }

        return aim.Equals(value, comparison);
    }

    public static bool NotEqualsTo(this string aim, string value, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        return !aim.EqualsTo(value, comparison);
    }

    public static string TrimSafe(this string aim)
    {
        if (aim.IsNull())
        {
            return string.Empty;
        }

        return aim.Trim();
    }

    public static byte ToByte(this string? aim, byte defaultValue = 0)
    {
        if (aim.IsNull())
        {
            return defaultValue;
        }

        if (byte.TryParse(aim, out var result))
        {
            return result;
        }
        else
        {
            return defaultValue;
        }
    }

    public static short ToInt16(this string? aim, short defaultValue = 0)
    {
        if (aim.IsNull())
        {
            return defaultValue;
        }

        if (short.TryParse(aim, out var result))
        {
            return result;
        }
        else
        {
            return defaultValue;
        }
    }

    public static ushort ToUInt16(this string? aim, ushort defaultValue = 0)
    {
        if (aim.IsNull())
        {
            return defaultValue;
        }

        if (ushort.TryParse(aim, out var result))
        {
            return result;
        }
        else
        {
            return defaultValue;
        }
    }

    public static int ToInt32(this string? aim, int defaultValue = 0)
    {
        if (aim.IsNull())
        {
            return defaultValue;
        }

        if (int.TryParse(aim, out var result))
        {
            return result;
        }
        else
        {
            return defaultValue;
        }
    }

    public static long ToInt64(this string aim, long defaultValue = 0)
    {
        if (long.TryParse(aim, out var result))
        {
            return result;
        }
        else
        {
            return defaultValue;
        }
    }

    public static Guid ToGuid(this string aim)
    {
        if (string.IsNullOrWhiteSpace(aim) || !Guid.TryParse(aim, out var guid))
        {
            return Guid.Empty;
        }

        return guid;
    }

    public static decimal ToDecimal(this string aim, decimal defaultValue = 0)
    {
        if (decimal.TryParse(aim, out var result))
        {
            return result;
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
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static float ToSingle(this string aim, float defaultValue = 0)
    {
        if (float.TryParse(aim, out var result))
        {
            return result;
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to double
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static double ToDouble(this string aim, double defaultValue = 0)
    {
        if (double.TryParse(aim, out var result))
        {
            return result;
        }
        else
        {
            return defaultValue;
        }
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

    public static TimeOnly? ToTimeOnly(this string aim, TimeOnly? defaultValue = null)
    {
        if (TimeOnly.TryParse(aim, out var result))
        {
            return result;
        }
        else
        {
            return defaultValue;
        }
    }

    public static TimeSpan? ToTimeSpan(this string aim, TimeSpan? defaultValue = null)
    {
        if (TimeSpan.TryParse(aim, out var result))
        {
            return result;
        }
        else
        {
            return defaultValue;
        }
    }

    public static DateOnly? ToDateOnly(this string aim, DateOnly? defaultValue = null)
    {
        if (DateOnly.TryParse(aim, out var result))
        {
            return result;
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to datetime
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static DateTime? ToDateTime(this string aim, DateTime? defaultValue = null)
    {
        if (DateTime.TryParse(aim, out var result))
        {
            return result;
        }
        else
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// convert to datetimeoffset
    /// </summary>
    /// <param name="aim"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static DateTimeOffset? ToDateTimeOffset(this string aim, DateTimeOffset? defaultValue = null)
    {
        if (DateTimeOffset.TryParse(aim, out var result))
        {
            return result;
        }
        else
        {
            return defaultValue;
        }
    }

    #endregion
}
