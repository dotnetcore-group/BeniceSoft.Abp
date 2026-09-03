using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace BeniceSoft.Core;

public class ExprBuilder<T>
    where T : class
{
    #region Members
    private readonly List<ExprConfigure> _configs = [];
    #endregion

    #region Methods
    public ExprBuilder<T> With(string propertyName, string alias, ExprOperator eop = ExprOperator.None, Func<object, object>? formatValue = null, Func<object, bool>? ignoreValue = null)
    {
        Configure(new ExprConfigure(propertyName, alias, eop, formatValue, ignoreValue));
        return this;
    }

    public ExprBuilder<T> With(Expression<Func<T, object>> columnName, string alias, ExprOperator eop = ExprOperator.None, Func<object, object>? formatValue = null, Func<object, bool>? ignoreValue = null)
    {
        return With(columnName.GetMember().Name, alias, eop, formatValue, ignoreValue);
    }

    public ExprBuilder<T> With(string propertyName, ExprOperator eop, Func<object, object>? formatValue = null, Func<object, bool>? ignoreValue = null)
    {
        return With(propertyName, "", eop, formatValue, ignoreValue);
    }

    public ExprBuilder<T> With(Expression<Func<T, object>> columnName, ExprOperator eop, Func<object, object>? formatValue = null, Func<object, bool>? ignoreValue = null)
    {
        return With(columnName, "", eop, formatValue, ignoreValue);
    }

    public ExprBuilder<T> With(string propertyName, Func<object, object>? formatValue, Func<object, bool>? ignoreValue = null)
    {
        return With(propertyName, ExprOperator.None, formatValue, ignoreValue);
    }

    public ExprBuilder<T> With(Expression<Func<T, object>>? columnName, Func<object, object>? formatValue, Func<object, bool>? ignoreValue = null)
    {
        return With(columnName!, ExprOperator.None, formatValue, ignoreValue);
    }

    public ExprBuilder<T> With(string propertyName, Func<object, bool> ignoreValue)
    {
        return With(propertyName, null, ignoreValue);
    }

    public ExprBuilder<T> With(Expression<Func<T, object>> columnName, Func<object, bool> ignoreValue)
    {
        return With(columnName, null, ignoreValue);
    }

    public ExprBuilder<T> Ignore(Expression<Func<T, object>> columnName)
    {
        return Ignore(columnName.GetMember().Name);
    }

    public ExprBuilder<T> Ignore(string propertyName)
    {
        Configure(new ExprConfigure(propertyName));
        return this;
    }

    private void Configure(ExprConfigure setting)
    {
        if (_configs.Exists(t => t.PropertyName == setting.PropertyName))
        {
            throw new InvalidOperationException($"PropertyName:{setting.PropertyName} already configured");
        }

        _configs.Add(setting);
    }

    public Expression<Func<T, bool>> Generate(object data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var result = ExprBuilder.True<T>();
        var props = data.GetType().GetProperties();
        foreach (var prop in props)
        {
            if (prop.IsDefined<ExprIgnoreAttribute>())
            {
                continue;
            }

            var propertyName = prop.Name;
            var attr = prop.GetCustomAttribute<ExprPropertyAttribute>() ?? new();
            if (attr.FieldName.IsNull())
            {
                attr.FieldName = propertyName;
            }

            var config = _configs.Find(t => t.Alias.EqualsTo(attr.FieldName));

            if (config != null)
            {
                if (config.Ignore)
                {
                    continue;
                }

                attr.FieldName = config.PropertyName;
                if (config.Operator != ExprOperator.None)
                {
                    attr.Operator = config.Operator;
                }
            }

            if (attr.Operator == ExprOperator.None)
            {
                continue;
            }

            var ignoreValue = config?.IgnoreValue;
            if (ignoreValue == null)
            {
                ignoreValue = v =>
                {
                    if (v == null)
                    {
                        return true;
                    }

                    if (v.Equals(attr.IgnoreValue))
                    {
                        return true;
                    }

                    if (v is string str && str.IsNull())
                    {
                        return true;
                    }

                    if (v is ICollection { Count: 0 })
                    {
                        return true;
                    }

                    if (attr.IgnoreValue != null && attr.IgnoreValue.ToStringSafe().EqualsTo(v.ToString() ?? ""))
                    {
                        return true;
                    }

                    return false;
                };
            }

            var propertyValue = prop.GetValue(data);
            if (config?.FormatValue != null)
            {
                propertyValue = config.FormatValue(propertyValue!);
            }

            if (ignoreValue(propertyValue!))
            {
                continue;
            }

            result = result.And(attr.FieldName, propertyValue!, attr.Operator)!;
        }

        return result;
    }

    public Expression<Func<T, bool>> Generate(IEnumerable<ExprSearch> exprs)
    {
        var result = ExprBuilder.True<T>();
        if (exprs.IsNull())
        {
            return result;
        }

        foreach (var expr in exprs)
        {
            if (expr.Name.IsNull())
            {
                continue;
            }

            var config = _configs.Find(t => t.Alias.EqualsTo(expr.Name));
            if (config != null)
            {
                expr.Name = config.PropertyName;
                if (config.Operator != ExprOperator.None)
                {
                    expr.Operator = config.Operator;
                }
            }

            var ignoreValue = config?.IgnoreValue;
            if (ignoreValue == null)
            {
                ignoreValue = v =>
                {
                    if (v == null)
                    {
                        return true;
                    }

                    if (v is string str && str.IsNull())
                    {
                        return true;
                    }

                    if (v is ICollection { Count: 0 })
                    {
                        return true;
                    }

                    return false;
                };
            }

            if (config?.FormatValue != null)
            {
                expr.Value = config.FormatValue(expr.Value!);
            }

            if (ignoreValue(expr.Value!))
            {
                continue;
            }

            result = result.And(expr.Name, expr.Value!, expr.Operator)!;
        }

        return result;
    }
    #endregion

    private sealed class ExprConfigure
    {
        internal ExprConfigure(string propertyName, string alias, ExprOperator eop = ExprOperator.None, Func<object, object>? formatValue = null, Func<object, bool>? ignoreValue = null)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(propertyName);

            PropertyName = propertyName;
            if (alias.IsNull())
            {
                alias = propertyName;
            }
            Alias = alias;
            Operator = eop;
            FormatValue = formatValue;
            IgnoreValue = ignoreValue;
        }

        internal ExprConfigure(string propertyName)
        {
            PropertyName = propertyName;
            Alias = propertyName;
            Ignore = true;
        }

        internal string PropertyName { get; }

        internal string Alias { get; }

        internal ExprOperator Operator { get; } = ExprOperator.None;

        internal Func<object, object>? FormatValue { get; }

        internal Func<object, bool>? IgnoreValue { get; }

        internal bool Ignore { get; }
    }
}

public class ExprSearch
{
    public string Name { get; set; } = string.Empty;

    public object? Value { get; set; }

    public ExprOperator Operator { get; set; } = ExprOperator.Equal;
}