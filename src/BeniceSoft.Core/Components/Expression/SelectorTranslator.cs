using System.Linq.Expressions;

namespace BeniceSoft.Core;

/// <summary>
/// Provides methods for combining and translating lambda expressions representing selectors.
/// </summary>
public static class SelectorTranslator
{
    /// <summary>
    /// Starts translation of a given selector.
    /// </summary>
    /// <typeparam name="TSource">The type of the selector's source parameter.</typeparam>
    /// <typeparam name="TResult">The type of the selector's result parameter.</typeparam>
    /// <param name="selector">The selector expression to translate.</param>
    /// <returns>A translation object for the given selector.</returns>
    public static SelectorTranslation<TSource, TResult> Translate<TSource, TResult>(this Expression<Func<TSource, TResult>> selector)
    {
        return new(selector);
    }

    /// <summary>
    /// Combines two given selectors by merging their member bindings.
    /// </summary>
    /// <typeparam name="TSource">The type of the selector's source parameter.</typeparam>
    /// <typeparam name="TResult">The type of the selector's result parameter.</typeparam>
    /// <param name="left">The first selector expression to combine.</param>
    /// <param name="right">The second selector expression to combine.</param>
    /// <returns>A single combined selector expression.</returns>
    public static Expression<Func<TSource, TResult>> Apply<TSource, TResult>(this Expression<Func<TSource, TResult>> left, Expression<Func<TSource, TResult>> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftInit = left.Body as MemberInitExpression;
        var rightInit = right.Body as MemberInitExpression;

        var leftNew = left.Body as NewExpression ?? leftInit?.NewExpression;
        var rightNew = right.Body as NewExpression ?? rightInit?.NewExpression;

        if (leftNew is null || rightNew is null)
        {
            throw new NotSupportedException("Only member init expressions and new expressions are supported yet.");
        }

        if (leftNew.Arguments.Count > 0 || rightNew.Arguments.Count > 0)
        {
            throw new NotSupportedException("Only parameterless constructors are supported yet.");
        }

        var leftBindings = leftInit?.Bindings ?? Enumerable.Empty<MemberBinding>();
        var rightBindings = rightInit?.Bindings ?? Enumerable.Empty<MemberBinding>();

        var l = left.Parameters[0];
        var r = right.Parameters[0];

        var binder = new ParameterBinder(l, r);

        return Expression.Lambda<Func<TSource, TResult>>(binder.Visit(Expression.MemberInit(Expression.New(typeof(TResult)), leftBindings.Concat(rightBindings))), r);
    }
}

/// <summary>
/// Represents a translation of a given selector.
/// </summary>
/// <typeparam name="TSource">The type of the selector's source parameter.</typeparam>
/// <typeparam name="TResult">The type of the selector's result parameter.</typeparam>
public class SelectorTranslation<TSource, TResult>
{
    private readonly Expression<Func<TSource, TResult>> _selector;

    /// <summary>
    /// Starts translation of a given selector.
    /// </summary>
    /// <param name="selector">The selector expression to translate.</param>
    public SelectorTranslation(Expression<Func<TSource, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        _selector = selector;
    }

    /// <summary>
    /// Translates a given selector for a given subtype using it's source parameter.
    /// </summary>
    /// <typeparam name="TTranslatedSource">The type of the translated selector's source parameter.</typeparam>
    /// <returns>A translated selector expression.</returns>
    public Expression<Func<TTranslatedSource, TResult>> Source<TTranslatedSource>()
        where TTranslatedSource : TSource
    {
        var s = _selector.Parameters[0];
        var t = Expression.Parameter(typeof(TTranslatedSource), s.Name);

        var binder = new ParameterBinder(s, t);

        return Expression.Lambda<Func<TTranslatedSource, TResult>>(binder.Visit(_selector.Body), t);
    }

    /// <summary>
    /// Translates a given selector for a given related type using it's source parameter.
    /// </summary>
    /// <typeparam name="TTranslatedSource">The type of the translated selector's source parameter.</typeparam>
    /// <param name="path">The path from the desired type to the given type.</param>
    /// <returns>A translated selector expression.</returns>
    public Expression<Func<TTranslatedSource, TResult>> Source<TTranslatedSource>(Expression<Func<TTranslatedSource, TSource>> path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var s = _selector.Parameters[0];
        var t = path.Parameters[0];

        var binder = new ParameterBinder(s, path.Body);

        return Expression.Lambda<Func<TTranslatedSource, TResult>>(binder.Visit(_selector.Body), t);
    }

    /// <summary>
    /// Translates a given selector for a given related type using it's source parameter.
    /// </summary>
    /// <typeparam name="TTranslatedSource">The type of the translated selector's source parameter.</typeparam>
    /// <param name="translation">The translation from the desired type to the given type,
    /// using the initially given selector to be injected into a new selector.</param>
    /// <returns>A translated selector expression.</returns>
    public Expression<Func<TTranslatedSource, TResult>> Source<TTranslatedSource>(Expression<Func<TTranslatedSource, Func<TSource, TResult>, TResult>> translation)
    {
        ArgumentNullException.ThrowIfNull(translation);

        var t = translation.Parameters[0];
        var s = translation.Parameters[1];

        var binder = new ParameterBinder(s, _selector);

        return Expression.Lambda<Func<TTranslatedSource, TResult>>(binder.Visit(translation.Body), t);
    }

    /// <summary>
    /// Translates a given selector for a given related type using it's source parameter.
    /// </summary>
    /// <typeparam name="TTranslatedSource">The type of the translated selector's source parameter.</typeparam>
    /// <param name="translation">The translation from the desired type to the given type,
    /// using the initially given selector to be injected into a new selector.</param>
    /// <returns>A translated selector expression.</returns>
    public Expression<Func<TTranslatedSource, IEnumerable<TResult>>> Source<TTranslatedSource>(Expression<Func<TTranslatedSource, Func<TSource, TResult>, IEnumerable<TResult>>> translation)
    {
        ArgumentNullException.ThrowIfNull(translation);

        var t = translation.Parameters[0];
        var s = translation.Parameters[1];

        var binder = new ParameterBinder(s, _selector);

        return Expression.Lambda<Func<TTranslatedSource, IEnumerable<TResult>>>(binder.Visit(translation.Body), t);
    }

    /// <summary>
    /// Translates a given selector for a given subtype using it's result parameter.
    /// </summary>
    /// <typeparam name="TTranslatedResult">The type of the translated selector's result parameter.</typeparam>
    /// <returns>A translated selector expression.</returns>
    public Expression<Func<TSource, TTranslatedResult>> Result<TTranslatedResult>()
        where TTranslatedResult : TResult
    {
        if (_selector.Body is MemberInitExpression init)
        {
            if (init.NewExpression.Arguments.Count > 0)
            {
                throw new NotSupportedException("Only parameterless constructors are supported yet.");
            }

            var s = _selector.Parameters[0];

            return Expression.Lambda<Func<TSource, TTranslatedResult>>(Expression.MemberInit(Expression.New(typeof(TTranslatedResult)), init.Bindings), s);
        }

        throw new NotSupportedException("Only member init expressions are supported yet.");
    }

    /// <summary>
    /// Translates a given selector for a given related type using it's result parameter.
    /// </summary>
    /// <typeparam name="TTranslatedResult">The type of the translated selector's result parameter.</typeparam>
    /// <param name="path">The path from the desired type to the given type.</param>
    /// <returns>A translated selector expression.</returns>
    public Expression<Func<TSource, TTranslatedResult>> Result<TTranslatedResult>(Expression<Func<TTranslatedResult, TResult>> path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.Body is MemberExpression member)
        {
            var s = _selector.Parameters[0];

            var bind = Expression.Bind(member.Member, _selector.Body);

            return Expression.Lambda<Func<TSource, TTranslatedResult>>(Expression.MemberInit(Expression.New(typeof(TTranslatedResult)), bind), s);
        }

        throw new NotSupportedException("Only member expressions are supported yet.");
    }

    /// <summary>
    /// Translates a given selector for a given related type using it's result parameter.
    /// </summary>
    /// <typeparam name="TTranslatedResult">The type of the translated selector's result parameter.</typeparam>
    /// <param name="translation">The translation from the desired type to the given type,
    /// using the initially given selector to be injected into a new selector.</param>
    /// <returns>A translated selector expression.</returns>
    public Expression<Func<TSource, TTranslatedResult>> Result<TTranslatedResult>(Expression<Func<TSource, Func<TSource, TResult>, TTranslatedResult>> translation)
    {
        ArgumentNullException.ThrowIfNull(translation);

        var s = translation.Parameters[0];
        var t = translation.Parameters[1];

        var binder = new ParameterBinder(t, _selector);

        return Expression.Lambda<Func<TSource, TTranslatedResult>>(binder.Visit(translation.Body), s);
    }

    /// <summary>
    /// Continues translation of a given selector for a given subtype using it's source parameter.
    /// </summary>
    /// <typeparam name="TTranslatedSource">The type of the translated selector's source parameter.</typeparam>
    /// <returns>Another translation object for the given selector.</returns>
    public SelectorTranslation<TTranslatedSource, TResult> Cross<TTranslatedSource>()
        where TTranslatedSource : TSource
    {
        return Source<TTranslatedSource>().Translate();
    }

    /// <summary>
    /// Continues translation of a given selector for a given related type using it's source parameter.
    /// </summary>
    /// <typeparam name="TTranslatedSource">The type of the translated selector's source parameter.</typeparam>
    /// <param name="path">The path from the desired type to the given type.</param>
    /// <returns>Another translation object for the given selector.</returns>
    public SelectorTranslation<TTranslatedSource, TResult> Cross<TTranslatedSource>(Expression<Func<TTranslatedSource, TSource>> path)
    {
        return Source(path).Translate();
    }

    /// <summary>
    /// Continues translation of a given selector for a given related type using it's source parameter.
    /// </summary>
    /// <typeparam name="TTranslatedSource">The type of the translated selector's source parameter.</typeparam>
    /// <param name="translation">The translation from the desired type to the given type,
    /// using the initially given selector to be injected into a new selector.</param>
    /// <returns>Another translation object for the given selector.</returns>
    public SelectorTranslation<TTranslatedSource, TResult> Cross<TTranslatedSource>(Expression<Func<TTranslatedSource, Func<TSource, TResult>, TResult>> translation)
    {
        return Source(translation).Translate();
    }

    /// <summary>
    /// Continues translation of a given selector for a given related type using it's source parameter.
    /// </summary>
    /// <typeparam name="TTranslatedSource">The type of the translated selector's source parameter.</typeparam>
    /// <param name="translation">The translation from the desired type to the given type,
    /// using the initially given selector to be injected into a new selector.</param>
    /// <returns>Another translation object for the given selector.</returns>
    public SelectorTranslation<TTranslatedSource, IEnumerable<TResult>> Cross<TTranslatedSource>(Expression<Func<TTranslatedSource, Func<TSource, TResult>, IEnumerable<TResult>>> translation)
    {
        return Source(translation).Translate();
    }

    /// <summary>
    /// Translates a given selector for a given subtype using it's result parameter
    /// and combines it with another given selector by merging their member bindings.
    /// </summary>
    /// <typeparam name="TTranslatedResult">The type of the translated selector's result parameter.</typeparam>
    /// <param name="value">The additional selector expression to combine.</param>
    /// <returns>A single translated and combined selector expression.</returns>
    public Expression<Func<TSource, TTranslatedResult>> Apply<TTranslatedResult>(Expression<Func<TSource, TTranslatedResult>> value)
        where TTranslatedResult : TResult
    {
        return Result<TTranslatedResult>().Apply(value);
    }

    /// <summary>
    /// Translates a given selector for a given related type using it's result parameter
    /// and combines it with another given selector by merging their member bindings.
    /// </summary>
    /// <typeparam name="TTranslatedResult">The type of the translated selector's result parameter.</typeparam>
    /// <param name="path">The path from the desired type to the given type.</param>
    /// <param name="value">The additional selector expression to combine.</param>
    /// <returns>A single translated and combined selector expression.</returns>
    public Expression<Func<TSource, TTranslatedResult>> Apply<TTranslatedResult>(Expression<Func<TTranslatedResult, TResult>> path, Expression<Func<TSource, TTranslatedResult>> value)
    {
        return Result(path).Apply(value);
    }

    /// <summary>
    /// Translates a given selector for a given related type using it's result parameter
    /// and combines it with another given selector by merging their member bindings.
    /// </summary>
    /// <typeparam name="TTranslatedResult">The type of the translated selector's result parameter.</typeparam>
    /// <param name="translation">The translation from the desired type to the given type,
    /// using the initially given selector to be injected into a new selector.</param>
    /// <param name="value">The additional selector expression to combine.</param>
    /// <returns>A single translated and combined selector expression.</returns>
    public Expression<Func<TSource, TTranslatedResult>> Apply<TTranslatedResult>(Expression<Func<TSource, Func<TSource, TResult>, TTranslatedResult>> translation, Expression<Func<TSource, TTranslatedResult>> value)
    {
        return Result(translation).Apply(value);
    }

    /// <summary>
    /// Translates a given selector for given subtypes using it's source and result parameter
    /// and combines it with another given selector by merging their member bindings.
    /// </summary>
    /// <typeparam name="TTranslatedSource">The type of the translated selector's source parameter.</typeparam>
    /// <typeparam name="TTranslatedResult">The type of the translated selector's result parameter.</typeparam>
    /// <param name="value">The additional selector expression to combine.</param>
    /// <returns>A single translated and combined selector expression.</returns>
    public Expression<Func<TTranslatedSource, TTranslatedResult>> To<TTranslatedSource, TTranslatedResult>(Expression<Func<TTranslatedSource, TTranslatedResult>>? value = null)
        where TTranslatedSource : TSource
        where TTranslatedResult : TResult
    {
        var result = Cross<TTranslatedSource>().Result<TTranslatedResult>();

        return value is not null ? result.Apply(value) : result;
    }

    /// <summary>
    /// Translates a given selector for a given related type using it's source and result parameter
    /// and combines it with another given selector by merging their member bindings.
    /// </summary>
    /// <typeparam name="TTranslatedSource">The type of the translated selector's source parameter.</typeparam>
    /// <typeparam name="TTranslatedResult">The type of the translated selector's result parameter.</typeparam>
    /// <param name="sourcePath">The path from the desired source type to the given type.</param>
    /// <param name="resultPath">The path from the desired result type to the given type.</param>
    /// <param name="value">The additional selector expression to combine.</param>
    /// <returns>A single translated and combined selector expression.</returns>
    public Expression<Func<TTranslatedSource, TTranslatedResult>> To<TTranslatedSource, TTranslatedResult>(Expression<Func<TTranslatedSource, TSource>> sourcePath, Expression<Func<TTranslatedResult, TResult>> resultPath, Expression<Func<TTranslatedSource, TTranslatedResult>>? value = null)
    {
        var result = Cross(sourcePath).Result(resultPath);

        return value is not null ? result.Apply(value) : result;
    }

    /// <summary>
    /// Translates a given selector for a given related type using it's source and result parameter
    /// and combines it with another given selector by merging their member bindings.
    /// </summary>
    /// <typeparam name="TTranslatedSource">The type of the translated selector's source parameter.</typeparam>
    /// <typeparam name="TTranslatedResult">The type of the translated selector's result parameter.</typeparam>
    /// <param name="translation">The translation from the desired type to the given type,
    /// using the initially given selector to be injected into a new selector.</param>
    /// <param name="value">The additional selector expression to combine.</param>
    /// <returns>A single translated and combined selector expression.</returns>
    public Expression<Func<TTranslatedSource, TTranslatedResult>> To<TTranslatedSource, TTranslatedResult>(Expression<Func<TTranslatedSource, Func<TSource, TResult>, TTranslatedResult>> translation, Expression<Func<TTranslatedSource, TTranslatedResult>>? value = null)
    {
        ArgumentNullException.ThrowIfNull(translation);

        var s = translation.Parameters[0];
        var t = translation.Parameters[1];

        var binder = new ParameterBinder(t, _selector);

        var result = Expression.Lambda<Func<TTranslatedSource, TTranslatedResult>>(binder.Visit(translation.Body), s);

        return value is not null ? result.Apply(value) : result;
    }
}

/// <summary>
/// Rebinds a parameter expression to any expression.
/// </summary>
public class ParameterBinder : ExpressionVisitor
{
    private readonly ParameterExpression _parameter;
    private readonly Expression _replacement;

    /// <summary>
    /// Create an new binder.
    /// </summary>
    /// <param name="parameter">Parameter to find.</param>
    /// <param name="replacement">Expression to insert.</param>
    public ParameterBinder(ParameterExpression parameter, Expression replacement)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        ArgumentNullException.ThrowIfNull(replacement);

        _parameter = parameter;
        _replacement = replacement;
    }

    /// <inheritdoc />
    protected override Expression VisitParameter(ParameterExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node == _parameter ? _replacement : base.VisitParameter(node);
    }

    /// <inheritdoc />
    protected override Expression VisitInvocation(InvocationExpression node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (node.Expression == _parameter && _replacement is LambdaExpression lambda)
        {
            var binders = lambda.Parameters.Zip(node.Arguments,
                (p, a) => new ParameterBinder(p, a));

            return binders.Aggregate(lambda.Body, (e, b) => b.Visit(e));
        }

        return base.VisitInvocation(node);
    }
}

/// <summary>
/// Represents a translation of a given predicate.
/// </summary>
/// <typeparam name="T">The type of the predicate's parameter.</typeparam>
public class PredicateTranslation<T>
{
    private readonly Expression<Func<T, bool>> _predicate;

    /// <summary>
    /// Creates a new predicate translation.
    /// </summary>
    /// <param name="predicate">The predicate to translate.</param>
    public PredicateTranslation(Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        _predicate = predicate;
    }

    /// <summary>
    /// Translates a given predicate for a given subtype.
    /// </summary>
    /// <typeparam name="TR">The type of the translated predicate's parameter.</typeparam>
    /// <returns>A translated predicate expression.</returns>
    public Expression<Func<TR, bool>> To<TR>()
        where TR : T
    {
        var s = _predicate.Parameters[0];
        var t = Expression.Parameter(typeof(TR), s.Name);
        var binder = new ParameterBinder(s, t);

        return Expression.Lambda<Func<TR, bool>>(binder.Visit(_predicate.Body), t);
    }

    /// <summary>
    /// Translates a given predicate for a given related type.
    /// </summary>
    /// <typeparam name="TSource">The type of the translated predicate's parameter.</typeparam>
    /// <param name="path">The path from the desired type to the given type.</param>
    /// <returns>A translated predicate expression.</returns>
    public Expression<Func<TSource, bool>> To<TSource>(Expression<Func<TSource, T>> path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var s = _predicate.Parameters[0];
        var t = path.Parameters[0];

        var binder = new ParameterBinder(s, path.Body);

        return Expression.Lambda<Func<TSource, bool>>(binder.Visit(_predicate.Body), t);
    }

    /// <summary>
    /// Translates a given predicate for a given related type.
    /// </summary>
    /// <typeparam name="TSource">The type of the translated predicate's parameter.</typeparam>
    /// <param name="translation">The translation from the desired type to the given type,
    /// using the initially given predicate to be injected into a new predicate.</param>
    /// <returns>A translated predicate expression.</returns>
    public Expression<Func<TSource, bool>> To<TSource>(Expression<Func<TSource, Func<T, bool>, bool>> translation)
    {
        ArgumentNullException.ThrowIfNull(translation);

        var t = translation.Parameters[0];
        var s = translation.Parameters[1];

        var binder = new ParameterBinder(s, _predicate);

        return Expression.Lambda<Func<TSource, bool>>(binder.Visit(translation.Body), t);
    }
}
