using BeniceSoft.Core;
using System.Globalization;
using System.Text;

namespace BeniceSoft.Abp.EntityFrameworkCore;

/// <summary>
/// 名称重写器接口
/// </summary>
public interface INameRewriter
{
    string RewriteName(string name);
}

/// <summary>
/// 空名称重写器（不做任何转换）
/// </summary>
public sealed class EmptyNameRewriter : INameRewriter
{
    public string RewriteName(string name)
    {
        return name;
    }
}

/// <summary>
/// 驼峰命名重写器
/// </summary>
public sealed class CamelCaseNameRewriter(CultureInfo culture) : INameRewriter
{
    public string RewriteName(string name)
    {
        return name.IsEmpty() ? name : char.ToLower(name[0], culture) + name[1..];
    }
}

/// <summary>
/// 小写命名重写器
/// </summary>
public sealed class LowerCaseNameRewriter(CultureInfo culture) : INameRewriter
{
    public string RewriteName(string name)
    {
        return name.ToLower(culture);
    }
}

/// <summary>
/// 大写命名重写器
/// </summary>
public sealed class UpperCaseNameRewriter(CultureInfo culture) : INameRewriter
{
    public string RewriteName(string name)
    {
        return name.ToUpper(culture);
    }
}

/// <summary>
/// 蛇形命名重写器
/// </summary>
public class SnakeCaseNameRewriter(CultureInfo culture) : INameRewriter
{
    public virtual string RewriteName(string name)
    {
        if (name.IsEmpty())
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + Math.Min(2, name.Length / 5));
        var previousCategory = default(UnicodeCategory?);

        foreach (var currentIndex in name.Length)
        {
            var currentChar = name[currentIndex];
            if (currentChar == '_')
            {
                builder.Append('_');
                previousCategory = null;
                continue;
            }

            var currentCategory = char.GetUnicodeCategory(currentChar);
            switch (currentCategory)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                    if (previousCategory.In(UnicodeCategory.SpaceSeparator, UnicodeCategory.LowercaseLetter, UnicodeCategory.DecimalDigitNumber) || previousCategory != UnicodeCategory.DecimalDigitNumber && previousCategory != null && currentIndex > 0 && currentIndex + 1 < name.Length && char.IsLower(name[currentIndex + 1]))
                    {
                        builder.Append('_');
                    }

                    currentChar = char.ToLower(currentChar, culture);
                    break;

                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.DecimalDigitNumber:
                    if (previousCategory == UnicodeCategory.SpaceSeparator)
                    {
                        builder.Append('_');
                    }

                    break;

                default:
                    if (previousCategory != null)
                    {
                        previousCategory = UnicodeCategory.SpaceSeparator;
                    }

                    continue;
            }

            builder.Append(currentChar);
            previousCategory = currentCategory;
        }

        return builder.ToString();
    }
}

/// <summary>
/// 大写蛇形命名重写器
/// </summary>
public sealed class UpperSnakeCaseNameRewriter(CultureInfo culture) : SnakeCaseNameRewriter(culture)
{
    private readonly CultureInfo _culture = culture;

    public override string RewriteName(string name)
    {
        return base.RewriteName(name).ToUpper(_culture);
    }
}

