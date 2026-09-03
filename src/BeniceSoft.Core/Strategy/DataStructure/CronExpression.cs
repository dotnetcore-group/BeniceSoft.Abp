using System.Collections;
using System.Globalization;
using System.Runtime.Serialization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace BeniceSoft.Core.Strategy;

[Serializable]
public sealed partial class CronExpression : ISerializable
{
    private static readonly int _maxYear = DateTime.Now.Year + 100;
    private static readonly Regex _regex = MyRegex(); //e.g. LW L-0W L-4 L-12W LW-4 LW-12
    private static readonly Regex _offsetRegex = MyRegex1();

    private TimeZoneInfo? _timeZone;

    [NonSerialized]
    private readonly CronField _seconds = new();

    [NonSerialized]
    private readonly CronField _minutes = new();

    [NonSerialized]
    private readonly CronField _hours = new();

    [NonSerialized]
    private readonly CronField _daysOfMonth = new();

    [NonSerialized]
    private readonly CronField _months = new();

    [NonSerialized]
    private readonly CronField _daysOfWeek = new();

    [NonSerialized]
    private readonly CronField _years = new();

    /// <summary>
    /// Last day of week.
    /// </summary>
    [NonSerialized]
    private bool _lastDayOfWeek;

    /// <summary>
    /// N number of weeks.
    /// </summary>
    [NonSerialized]
    private int _everyNthWeek;

    /// <summary>
    /// Nth day of week.
    /// </summary>
    [NonSerialized]
    private int _nthdayOfWeek;

    /// <summary>
    /// Last day of month.
    /// </summary>
    [NonSerialized]
    private bool _lastDayOfMonth;

    /// <summary>
    /// Nearest weekday.
    /// </summary>
    [NonSerialized]
    private bool _nearestWeekday;

    [NonSerialized]
    private int _lastDayOffset;

    [NonSerialized]
    private int _lastWeekdayOffset;

    ///<summary>
    /// Constructs a new <see cref="CronExpressionString" /> based on the specified
    /// parameter.
    /// </summary>
    /// <param name="cronExpression">
    /// String representation of the cron expression the new object should represent
    /// </param>
    /// <see cref="CronExpressionString" />
    public CronExpression(string cronExpression)
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(cronExpression);

        CronExpressionString = CultureInfo.InvariantCulture.TextInfo.ToUpper(cronExpression).Trim();
        BuildExpression(CronExpressionString);
    }

    private static int GetVersion(SerializationInfo info)
    {
        try
        {
            return info.GetInt32("version");
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Serialization constructor.
    /// </summary>
    private CronExpression(SerializationInfo info, StreamingContext context)
    {
        var version = GetVersion(info);
        switch (version)
        {
            case 0:
                CronExpressionString = info.GetValue<string>("cronExpressionString")
                    ?? throw new SerializationException("Missing cronExpressionString.");
                TimeZone = info.GetValue<TimeZoneInfo>("timeZone") ?? TimeZoneInfo.Local;
                break;

            case 1:
                CronExpressionString = info.GetValue<string>("cronExpression")
                    ?? throw new SerializationException("Missing cronExpression.");
                var timeZoneId = info.GetValue<string>("timeZoneId");
                if (timeZoneId.IsNotNull())
                {
                    _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                }

                break;

            default:
                throw new NotSupportedException($"Unknown serialization version {version}");
        }

        BuildExpression(CronExpressionString);
    }

    [SecurityCritical]
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue("version", 1);
        info.AddValue("cronExpression", CronExpressionString);
        info.AddValue("timeZoneId", TimeZone.Id);
    }

    /// <summary>
    /// Indicates whether the given date satisfies the cron expression.
    /// </summary>
    /// <remarks>
    /// Note that  milliseconds are ignored, so two Dates falling on different milliseconds
    /// of the same second will always have the same result here.
    /// </remarks>
    /// <param name="dateUtc">The date to evaluate.</param>
    /// <returns>a boolean indicating whether the given date satisfies the cron expression</returns>
    public bool IsSatisfiedBy(DateTimeOffset dateUtc)
    {
        var withoutMilliseconds = new DateTimeOffset(dateUtc.Year, dateUtc.Month, dateUtc.Day, dateUtc.Hour, dateUtc.Minute, dateUtc.Second, dateUtc.Offset);
        var test = withoutMilliseconds.AddSeconds(-1);
        var timeAfter = GetTimeAfter(test);

        return timeAfter.HasValue && timeAfter.Value.Equals(withoutMilliseconds);
    }

    /// <summary>
    /// Returns the next date/time <i>after</i> the given date/time which
    /// satisfies the cron expression.
    /// </summary>
    /// <param name="date">the date/time at which to begin the search for the next valid date/time</param>
    /// <returns>the next valid date/time</returns>
    public DateTimeOffset? GetNextValidTimeAfter(DateTimeOffset date)
    {
        return GetTimeAfter(date);
    }

    /// <summary>
    /// Returns the next date/time <i>after</i> the given date/time which does
    /// <i>not</i> satisfy the expression.
    /// </summary>
    /// <param name="date">the date/time at which to begin the search for the next invalid date/time</param>
    /// <returns>the next valid date/time</returns>
    public DateTimeOffset? GetNextInvalidTimeAfter(DateTimeOffset date)
    {
        long difference = 1000;

        // move back to the nearest second so differences will be accurate
        var lastDate = new DateTimeOffset(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, date.Offset).AddSeconds(-1);

        //IMPROVE THIS! The following is a BAD solution to this problem. Performance will be very bad here, depending on the cron expression. It is, however A solution.

        //keep getting the next included time until it's farther than one second
        // apart. At that point, lastDate is the last valid fire time. We return
        // the second immediately following it.
        while (difference == 1000)
        {
            var newDate = GetTimeAfter(lastDate);

            if (newDate == null)
            {
                break;
            }

            difference = (long)(newDate.Value - lastDate).TotalMilliseconds;

            if (difference == 1000)
            {
                lastDate = newDate.Value;
            }
        }

        return lastDate.AddSeconds(1);
    }

    /// <summary>
    /// Sets or gets the time zone for which the <see cref="CronExpression" /> of this
    /// </summary>
    public TimeZoneInfo TimeZone
    {
        set => _timeZone = value;
        get => _timeZone ??= TimeZoneInfo.Local;
    }

    /// <summary>
    /// Returns the string representation of the <see cref="CronExpression" />
    /// </summary>
    /// <returns>The string representation of the <see cref="CronExpression" /></returns>
    public override string ToString()
    {
        return CronExpressionString;
    }

    /// <summary>
    /// Indicates whether the specified cron expression can be parsed into a
    /// valid cron expression
    /// </summary>
    /// <param name="cronExpression">the expression to evaluate</param>
    /// <returns>a boolean indicating whether the given expression is a valid cron
    ///         expression</returns>
    public static bool IsValidExpression(string cronExpression)
    {
        try
        {
            _ = new CronExpression(cronExpression);
        }
        catch (FormatException)
        {
            return false;
        }

        return true;
    }

    public static void ValidateExpression(string cronExpression)
    {
        _ = new CronExpression(cronExpression);
    }

    ////////////////////////////////////////////////////////////////////////////
    //
    // Expression Parsing Functions
    //
    ////////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Builds the expression.
    /// </summary>
    /// <param name="expression">The expression.</param>
    private void BuildExpression(string expression)
    {
        try
        {
            _seconds.Clear();
            _minutes.Clear();
            _hours.Clear();
            _daysOfMonth.Clear();
            _months.Clear();
            _daysOfWeek.Clear();
            _years.Clear();

            var exprOn = Constants.Second;

            foreach (var expStr in expression.Split(' ', '\t'))
            {
                var expr = expStr.AsSpan();

                if (exprOn > Constants.Year)
                {
                    break;
                }

                // throw an exception if L is used with other days of the month
                if (exprOn == Constants.DayOfMonth)
                {
                    if (expr.IndexOf('L') != -1 && expr.Length > 1 && expr.IndexOf(',') >= 0 && expr[(expr.IndexOf('L') + 1)..].IndexOf('L') != -1)
                    {
                        throw new FormatException("Support for specifying 'L' with other days of the month is limited to one instance of L");
                    }
                }

                // throw an exception if L is used with other days of the week
                if (exprOn == Constants.DayOfWeek && expr.IndexOf('L') != -1 && expr.Length > 1 && expr.IndexOf(',') >= 0)
                {
                    throw new FormatException("Support for specifying 'L' with other days of the week is not implemented");
                }

                if (exprOn == Constants.DayOfWeek && expr.IndexOf('#') != -1 && expr[(expr.IndexOf('#') + 1 + 1)..].IndexOf('#') != -1)
                {
                    throw new FormatException("Support for specifying multiple \"nth\" days is not implemented.");
                }

                if (expr.IndexOf(',') != -1)
                {
                    foreach (var v in expStr.Split(','))
                    {
                        StoreExpressionValues(0, v, exprOn);
                    }
                }
                else
                {
                    // simple field
                    StoreExpressionValues(0, expr, exprOn);
                }

                exprOn++;
            }

            if (exprOn <= Constants.DayOfWeek)
            {
                throw new FormatException("Unexpected end of expression.");
            }

            if (exprOn <= Constants.Year)
            {
                StoreExpressionValues(0, "*".AsSpan(), Constants.Year);
            }
        }
        catch (FormatException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new FormatException($"Illegal cron expression format ({e.Message})", e);
        }
    }

    private void StoreExpressionQuestionMark(int type, ReadOnlySpan<char> s, int i)
    {
        i++;
        if (i + 1 <= s.Length && s[i] != ' ' && s[i] != '\t')
        {
            throw new FormatException("Illegal character after '?': " + s[i]);
        }

        if (type.NotIn(Constants.DayOfWeek, Constants.DayOfMonth))
        {
            throw new FormatException("'?' can only be specified for Day-of-Month or Day-of-Week.");
        }

        if (type == Constants.DayOfWeek && !_lastDayOfMonth)
        {
            var val = _daysOfMonth.LastOrDefault();
            if (val == Constants.NoSpec)
            {
                throw new FormatException("'?' can only be specified for Day-of-Month -OR- Day-of-Week.");
            }
        }

        AddToSet(Constants.NoSpec, -1, 0, type);
    }

    private void StoreExpressionStarOrSlash(int type, ReadOnlySpan<char> s, int i)
    {
        var c = s[i];
        var incr = 0;
        var startsWithAsterisk = c == '*';
        if (startsWithAsterisk && i + 1 >= s.Length)
        {
            AddToSet(Constants.AllSpec, -1, incr, type);
            return;
        }

        if (c == '/' && (i + 1 >= s.Length || s[i + 1] == ' ' || s[i + 1] == '\t'))
        {
            throw new FormatException("'/' must be followed by an integer.");
        }

        if (startsWithAsterisk)
        {
            i++;
        }

        c = s[i];
        if (c == '/')
        {
            // is an increment specified?
            i++;
            if (i >= s.Length)
            {
                throw new FormatException("Unexpected end of string.");
            }

            incr = GetNumericValue(s, i);
            CheckIncrementRange(incr, type);
        }
        else
        {
            if (startsWithAsterisk)
            {
                throw new FormatException("Illegal characters after asterisk: " + s.ToString());
            }

            incr = 1;
        }

        AddToSet(Constants.AllSpec, -1, incr, type);
    }

    private void StoreExpressionL(int type, ReadOnlySpan<char> s, int i)
    {
        i++;
        switch (type)
        {
            case Constants.DayOfMonth:
                {
                    _lastDayOfMonth = true;
                    if (s.Length > i)
                    {
                        var c = s[i];
                        if (c == '-')
                        {
                            (_lastDayOffset, i) = GetValue(0, s, i + 1);
                            if (_lastDayOffset > 30)
                            {
                                throw new FormatException("Offset from last day must be <= 30");
                            }
                        }

                        if (s.Length > i)
                        {
                            c = s[i];
                            if (c == 'W')
                            {
                                _nearestWeekday = true;
                            }

                            var match = _offsetRegex.Match(s.ToString());
                            if (match.Success)
                            {
                                var offSetGroup = match.Groups["offset"];
                                if (offSetGroup.Success)
                                {
                                    _lastWeekdayOffset = int.Parse(offSetGroup.Value);
                                }
                            }
                        }
                    }

                    break;
                }

            case Constants.DayOfWeek:
                AddToSet(7, 7, 0, type);
                break;
            default:
                throw new FormatException($"'L' option is not valid here. (pos={i})");
        }
    }

    private void StoreExpressionNumeric(int type, ReadOnlySpan<char> s, int i)
    {
        if (int.TryParse(s, out var temp))
        {
            AddToSet(temp, -1, -1, type);
            return;
        }

        var c = s[i];
        var val = ToInt32(c);
        i++;
        if (i >= s.Length)
        {
            AddToSet(val, -1, -1, type);
        }
        else
        {
            c = s[i];
            if (char.IsDigit(c))
            {
                (val, i) = GetValue(val, s, i);
            }

            CheckNext(i, s, val, type);
        }
    }

    private void StoreExpressionGeneralValue(int type, ReadOnlySpan<char> s, int i)
    {
        var incr = 0;
        var sub = s.Slice(i, 3);
        int sval;
        var eval = -1;
        if (type == Constants.Month)
        {
            sval = GetMonthNumber(sub) + 1;
            if (sval <= 0)
            {
                throw new FormatException($"Invalid Month value: '{sub.ToString()}'");
            }

            if (s.Length > i + 3)
            {
                if (s[i + 3] == '-')
                {
                    i += 4;
                    sub = s.Slice(i, 3);
                    eval = GetMonthNumber(sub) + 1;
                    if (eval <= 0)
                    {
                        throw new FormatException($"Invalid Month value: '{sub.ToString()}'");
                    }
                }
            }
        }
        else if (type == Constants.DayOfWeek)
        {
            sval = GetDayOfWeekNumber(sub);
            if (sval < 0)
            {
                throw new FormatException($"Invalid Day-of-Week value: '{sub.ToString()}'");
            }

            if (s.Length > i + 3)
            {
                var c = s[i + 3];
                switch (c)
                {
                    case '-':
                        i += 4;
                        sub = s.Slice(i, 3);
                        eval = GetDayOfWeekNumber(sub);
                        if (eval < 0)
                        {
                            throw new FormatException($"Invalid Day-of-Week value: '{sub.ToString()}'");
                        }

                        break;
                    case '#':
                        try
                        {
                            i += 4;
                            _nthdayOfWeek = ToInt32(s[i..]);
                            if (_nthdayOfWeek is < 1 or > 5)
                            {
                                throw new FormatException("nthdayOfWeek is < 1 or > 5");
                            }
                        }
                        catch
                        {
                            throw new FormatException("A numeric value between 1 and 5 must follow the '#' option");
                        }

                        break;
                    case '/':
                        try
                        {
                            i += 4;
                            _everyNthWeek = ToInt32(s[i..]);
                            if (_everyNthWeek is < 1 or > 5)
                            {
                                throw new FormatException("everyNthWeek is < 1 or > 5");
                            }
                        }
                        catch
                        {
                            throw new FormatException("A numeric value between 1 and 5 must follow the '/' option");
                        }

                        break;
                    case 'L':
                        _lastDayOfWeek = true;
                        break;
                    default:
                        throw new FormatException($"Illegal characters for this position: '{sub.ToString()}'");
                }
            }
        }
        else
        {
            throw new FormatException($"Illegal characters for this position: '{sub.ToString()}'");
        }

        if (eval != -1)
        {
            incr = 1;
        }

        AddToSet(sval, eval, incr, type);
    }

    private void StoreExpressionValues(int pos, ReadOnlySpan<char> s, int type)
    {
        var i = pos;
        if (i < s.Length && char.IsWhiteSpace(s[i]))
        {
            i = SkipWhiteSpace(pos, s);
        }

        if (i >= s.Length)
        {
            return;
        }

        switch (s[i])
        {
            case >= 'A' and <= 'Z' when !s.SequenceEqual("L".AsSpan()) && !_regex.IsMatch(s.ToString()):
                StoreExpressionGeneralValue(type, s, i);
                break;

            case '?':
                StoreExpressionQuestionMark(type, s, i);
                break;

            case '*':
            case '/':
                StoreExpressionStarOrSlash(type, s, i);
                break;

            case 'L':
                StoreExpressionL(type, s, i);
                break;

            case >= '0' and <= '9':
                StoreExpressionNumeric(type, s, i);
                break;
            default:
                throw new FormatException($"Unexpected character: {s[i]}");
        }
    }

    // ReSharper disable once UnusedParameter.Local
    private static void CheckIncrementRange(int incr, int type)
    {
        if (incr > 59 && (type == Constants.Second || type == Constants.Minute))
        {
            throw new FormatException($"Increment > 60 : {incr}");
        }

        if (incr > 23 && type == Constants.Hour)
        {
            throw new FormatException($"Increment > 24 : {incr}");
        }

        if (incr > 31 && type == Constants.DayOfMonth)
        {
            throw new FormatException($"Increment > 31 : {incr}");
        }

        if (incr > 7 && type == Constants.DayOfWeek)
        {
            throw new FormatException($"Increment > 7 : {incr}");
        }

        if (incr > 12 && type == Constants.Month)
        {
            throw new FormatException($"Increment > 12 : {incr}");
        }
    }

    private void CheckNext(int pos, ReadOnlySpan<char> s, int val, int type)
    {
        var end = -1;
        var i = pos;

        if (i >= s.Length)
        {
            AddToSet(val, end, -1, type);
            return;
        }

        switch (s[pos])
        {
            case 'L':
                {
                    if (type == Constants.DayOfWeek)
                    {
                        if (val is < 1 or > 7)
                        {
                            throw new FormatException("Day-of-Week values must be between 1 and 7");
                        }

                        _lastDayOfWeek = true;
                    }
                    else
                    {
                        throw new FormatException($"'L' option is not valid here. (pos={i})");
                    }

                    var data = GetSet(type);
                    data.Add(val);
                    return;
                }

            case 'W':
                {
                    if (type == Constants.DayOfMonth)
                    {
                        _nearestWeekday = true;
                    }
                    else
                    {
                        throw new FormatException($"'W' option is not valid here. (pos={i})");
                    }

                    if (val > 31)
                    {
                        throw new FormatException("The 'W' option does not make sense with values larger than 31 (max number of days in a month)");
                    }

                    var data = GetSet(type);
                    data.Add(val);
                    return;
                }

            case '#':
                {
                    if (type != Constants.DayOfWeek)
                    {
                        throw new FormatException($"'#' option is not valid here. (pos={i})");
                    }

                    i++;
                    try
                    {
                        _nthdayOfWeek = ToInt32(s[i..]);
                        if (_nthdayOfWeek is < 1 or > 5)
                        {
                            throw new FormatException("nthdayOfWeek is < 1 or > 5");
                        }

                        // check first char is numeric and is a valid Day of week (1-7)
                        if (int.TryParse(s[..pos], out val))
                        {
                            if (val is < 1 or > 7)
                            {
                                throw new FormatException("Day-of-Week values must be between 1 and 7");
                            }
                        }
                    }
                    catch
                    {
                        throw new FormatException("A numeric value between 1 and 5 must follow the '#' option");
                    }

                    var data = GetSet(type);
                    data.Add(val);
                    return;
                }

            case 'C':
                {
                    switch (type)
                    {
                        case Constants.DayOfWeek:
                        case Constants.DayOfMonth:
                            break;
                        default:
                            throw new FormatException($"'C' option is not valid here. (pos={i})");
                    }

                    var data = GetSet(type);
                    data.Add(val);
                    return;
                }

            case '-':
                {
                    i++;
                    var c = s[i];
                    var v = ToInt32(c);
                    end = v;
                    i++;
                    if (i >= s.Length)
                    {
                        AddToSet(val, end, 1, type);
                        return;
                    }

                    c = s[i];
                    if (char.IsDigit(c))
                    {
                        (end, i) = GetValue(v, s, i);
                    }

                    if (i < s.Length && s[i] == '/')
                    {
                        i++;
                        c = s[i];
                        var v2 = ToInt32(c);
                        i++;
                        if (i >= s.Length)
                        {
                            AddToSet(val, end, v2, type);
                            return;
                        }

                        c = s[i];
                        if (char.IsDigit(c))
                        {
                            var (v3, _) = GetValue(v2, s, i);
                            AddToSet(val, end, v3, type);
                            return;
                        }

                        AddToSet(val, end, v2, type);
                        return;
                    }

                    AddToSet(val, end, 1, type);
                    return;
                }

            case '/':
                {
                    if (i + 1 >= s.Length || s[i + 1] == ' ' || s[i + 1] == '\t')
                    {
                        throw new FormatException("\'/\' must be followed by an integer.");
                    }

                    i++;
                    var c = s[i];
                    var v2 = ToInt32(c);
                    i++;
                    if (i >= s.Length)
                    {
                        CheckIncrementRange(v2, type);
                        AddToSet(val, end, v2, type);
                        return;
                    }

                    c = s[i];
                    if (char.IsDigit(c))
                    {
                        var (v3, _) = GetValue(v2, s, i);
                        CheckIncrementRange(v3, type);
                        AddToSet(val, end, v3, type);
                        return;
                    }

                    throw new FormatException($"Unexpected character '{c}' after '/'");
                }
        }

        AddToSet(val, end, 0, type);
    }

    /// <summary>
    /// Gets the cron expression string.
    /// </summary>
    /// <value>The cron expression string.</value>
    public string CronExpressionString { get; }

    private static int SkipWhiteSpace(int position, ReadOnlySpan<char> str)
    {
        for (; position < str.Length && (str[position] == ' ' || str[position] == '\t'); position++)
        {
        }

        return position;
    }

    private static int FindNextWhiteSpace(int position, ReadOnlySpan<char> str)
    {
        for (; position < str.Length && (str[position] != ' ' || str[position] != '\t'); position++)
        {
        }

        return position;
    }

    private void AddToSet(int val, int end, int incr, int type)
    {
        var data = GetSet(type);

        if (type.In(Constants.Second, Constants.Minute))
        {
            if ((val < 0 || val > 59 || end > 59) && val != Constants.AllSpec)
            {
                throw new FormatException("Minute and CronExpressionConstants.Second values must be between 0 and 59");
            }
        }
        else if (type == Constants.Hour)
        {
            if ((val < 0 || val > 23 || end > 23) && val != Constants.AllSpec)
            {
                throw new FormatException("Hour values must be between 0 and 23");
            }
        }
        else if (type == Constants.DayOfMonth)
        {
            if ((val < 1 || val > 31 || end > 31) && val != Constants.AllSpec && val != Constants.NoSpec)
            {
                throw new FormatException("Day of month values must be between 1 and 31");
            }
        }
        else if (type == Constants.Month)
        {
            if ((val < 1 || val > 12 || end > 12) && val != Constants.AllSpec)
            {
                throw new FormatException("Month values must be between 1 and 12");
            }
        }
        else if (type == Constants.DayOfWeek)
        {
            if ((val == 0 || val > 7 || end > 7) && val != Constants.AllSpec
                                                 && val != Constants.NoSpec)
            {
                throw new FormatException("Day-of-Week values must be between 1 and 7");
            }
        }

        if ((incr == 0 || incr == -1) && val != Constants.AllSpec)
        {
            data.Add(val != -1 ? val : Constants.NoSpec);
            return;
        }

        var startAt = val;
        var stopAt = end;

        if (val == Constants.AllSpec && incr <= 0)
        {
            data.Add(Constants.AllSpec);
            // skip adding other data, we check this wildcard in TryGetMinValueStartingFrom
            return;
        }

        if (type is Constants.Second or Constants.Minute)
        {
            if (stopAt == -1)
            {
                stopAt = 59;
            }

            if (startAt is (-1) or Constants.AllSpec)
            {
                startAt = 0;
            }
        }
        else if (type == Constants.Hour)
        {
            if (stopAt == -1)
            {
                stopAt = 23;
            }

            if (startAt is (-1) or Constants.AllSpec)
            {
                startAt = 0;
            }
        }
        else if (type == Constants.DayOfMonth)
        {
            if (stopAt == -1)
            {
                stopAt = 31;
            }

            if (startAt is (-1) or Constants.AllSpec)
            {
                startAt = 1;
            }
        }
        else if (type == Constants.Month)
        {
            if (stopAt == -1)
            {
                stopAt = 12;
            }

            if (startAt is (-1) or Constants.AllSpec)
            {
                startAt = 1;
            }
        }
        else if (type == Constants.DayOfWeek)
        {
            if (stopAt == -1)
            {
                stopAt = 7;
            }

            if (startAt is (-1) or Constants.AllSpec)
            {
                startAt = 1;
            }
        }
        else if (type == Constants.Year)
        {
            if (stopAt == -1)
            {
                stopAt = _maxYear;
            }

            if (startAt is -1 or Constants.AllSpec)
            {
                startAt = 1970;
            }
        }

        // if the end of the range is before the start, then we need to overflow into
        // the next day, month etc. This is done by adding the maximum amount for that
        // type, and using modulus max to determine the value being added.
        var max = -1;
        if (stopAt < startAt)
        {
            max = type switch
            {
                Constants.Second => 60,
                Constants.Minute => 60,
                Constants.Hour => 24,
                Constants.Month => 12,
                Constants.DayOfWeek => 7,
                Constants.DayOfMonth => 31,
                Constants.Year => throw new ArgumentException("start year must be less than stop year"),
                _ => throw new ArgumentException("unexpected type encountered"),
            };
            stopAt += max;
        }

        for (var i = startAt; i <= stopAt; i += incr)
        {
            if (max == -1)
            {
                // ie: there's no max to overflow over
                data.Add(i);
            }
            else
            {
                // take the modulus to get the real value
                var i2 = i % max;

                // 1-indexed ranges should not include 0, and should include their max
                if (i2 == 0 && type.In(Constants.Month, Constants.DayOfWeek, Constants.DayOfMonth))
                {
                    i2 = max;
                }

                data.Add(i2);
            }
        }
    }

    /// <summary>
    /// Gets the set of given type.
    /// </summary>
    private CronField GetSet(int type)
    {
        var field = type switch
        {
            Constants.Second => _seconds,
            Constants.Minute => _minutes,
            Constants.Hour => _hours,
            Constants.DayOfMonth => _daysOfMonth,
            Constants.Month => _months,
            Constants.DayOfWeek => _daysOfWeek,
            Constants.Year => _years,
            _ => default
        };

        if (field is null)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        return field;
    }

    private static ValueSet GetValue(int v, ReadOnlySpan<char> s, int i)
    {
        var c = s[i];

        var builder = new StringBuilder(s.Length);
        builder.Append(v);

        while (char.IsDigit(c))
        {
            builder.Append(c);
            i++;
            if (i >= s.Length)
            {
                break;
            }

            c = s[i];
        }

        var value = Convert.ToInt32(builder.ToString(), CultureInfo.InvariantCulture);
        var pos = i < s.Length ? i : i + 1;
        return new ValueSet(value, pos);
    }

    /// <summary>
    /// Gets the numeric value from string.
    /// </summary>
    private static int GetNumericValue(ReadOnlySpan<char> s, int i)
    {
        var endOfVal = FindNextWhiteSpace(i, s);
        return ToInt32(s[i..endOfVal]);
    }

    /// <summary>
    /// Gets the month number.
    /// </summary>
    /// <param name="s">The string to map with.</param>
    /// <returns></returns>
    private static int GetMonthNumber(ReadOnlySpan<char> s)
    {
        return s switch
        {
            "JAN" => 0,
            "FEB" => 1,
            "MAR" => 2,
            "APR" => 3,
            "MAY" => 4,
            "JUN" => 5,
            "JUL" => 6,
            "AUG" => 7,
            "SEP" => 8,
            "OCT" => 9,
            "NOV" => 1,
            "DEC" => 1,
            _ => -1
        };
    }

    private static int GetDayOfWeekNumber(ReadOnlySpan<char> s)
    {
        return s switch
        {
            "SUN" => 1,
            "MON" => 2,
            "TUE" => 3,
            "WED" => 4,
            "THU" => 5,
            "FRI" => 6,
            "SAT" => 7,
            _ => -1
        };
    }

    /// <summary>
    /// Progress next fire time seconds
    /// </summary>
    private NextFireTimeCursor ProgressNextFireTimeSecond(DateTimeOffset d)
    {
        var sec = d.Second;
        if (_seconds.TryGetMinValueStartingFrom(sec, out var min))
        {
            sec = min;
        }
        else
        {
            sec = _seconds.Min;
            d = d.AddMinutes(1);
        }

        return new NextFireTimeCursor(false, new DateTimeOffset(d.Year, d.Month, d.Day, d.Hour, d.Minute, sec, d.Millisecond, d.Offset));
    }

    /// <summary>
    /// Progress next Fire time Minutes
    /// </summary>
    /// <param name="d">NextFireTimeCheck</param>
    private NextFireTimeCursor ProgressNextFireTimeMinute(DateTimeOffset d)
    {
        var minute = d.Minute;
        var hr = d.Hour;
        var t = -1;

        if (_minutes.TryGetMinValueStartingFrom(minute, out var min))
        {
            t = minute;
            minute = min;
        }
        else
        {
            minute = _minutes.Min;
            hr++;
        }

        if (minute != t)
        {
            d = new DateTimeOffset(d.Year, d.Month, d.Day, d.Hour, minute, 0, d.Millisecond, d.Offset);
            d = SetCalendarHour(d, hr);
            return new NextFireTimeCursor(true, d);
        }

        return new NextFireTimeCursor(false, new DateTimeOffset(d.Year, d.Month, d.Day, d.Hour, minute, d.Second, d.Millisecond, d.Offset));
    }

    /// <summary>
    /// Progress next fire time Hour
    /// </summary>
    /// <param name="d">NextFireTimeCheck</param>
    private NextFireTimeCursor ProgressNextFireTimeHour(DateTimeOffset d)
    {
        int hour;
        var day = d.Day;
        var t = -1;

        if (_hours.TryGetMinValueStartingFrom(d.Hour, out var min))
        {
            t = d.Hour;
            hour = min;
        }
        else
        {
            hour = _hours.Min;
            day++;
        }

        if (hour != t)
        {
            var daysInMonth = DateTime.DaysInMonth(d.Year, d.Month);
            if (day > daysInMonth)
            {
                d = new DateTimeOffset(d.Year, d.Month, daysInMonth, d.Hour, 0, 0, d.Millisecond, d.Offset).AddDays(day - daysInMonth);
            }
            else
            {
                d = new DateTimeOffset(d.Year, d.Month, day, d.Hour, 0, 0, d.Millisecond, d.Offset);
            }

            d = SetCalendarHour(d, hour);
            return new NextFireTimeCursor(true, d);
        }

        return new NextFireTimeCursor(false, new DateTimeOffset(d.Year, d.Month, d.Day, hour, d.Minute, d.Second, d.Millisecond, d.Offset));
    }

    private SortedSet<int> CalculateDaysOfMonth(DateTimeOffset dt)
    {
        var results = new SortedSet<int>(_daysOfMonth);
        if (_lastDayOfMonth)
        {
            var lastDayOfMonth = GetLastDayOfMonth(dt.Month, dt.Year);
            var lastDayOfMonthWithOffset = lastDayOfMonth - _lastDayOffset;

            if (_nearestWeekday)
            {
                var checkDay = new DateTimeOffset(dt.Year, dt.Month, lastDayOfMonthWithOffset, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Offset);
                var calculatedDay = lastDayOfMonthWithOffset;
                switch (checkDay.DayOfWeek)
                {
                    case DayOfWeek.Saturday:
                        calculatedDay -= 1;
                        break;
                    case DayOfWeek.Sunday:
                        calculatedDay -= 2;
                        break;
                }

                var calculatedLastDayWithOffset = calculatedDay - _lastWeekdayOffset;
                // If the day has crossed to the prior month, reset to 1st.
                if (calculatedLastDayWithOffset <= 0)
                {
                    calculatedLastDayWithOffset = 1;
                }

                results.Add(calculatedLastDayWithOffset);
            }
            else
            {
                results.Add(lastDayOfMonthWithOffset);
            }
        }
        else if (_nearestWeekday) //AND not lastDay
        {
            var day = _daysOfMonth.Min;
            var tcal = new DateTimeOffset(dt.Year, dt.Month, day, 0, 0, 0, dt.Offset);
            var lastDayOfMonth = GetLastDayOfMonth(dt.Month, dt.Year);
            var dayOfWeek = tcal.DayOfWeek;

            // evict original date since it has a weekDayModifier
            results.Remove(day);

            switch (dayOfWeek)
            {
                case DayOfWeek.Saturday when day == 1:
                    day += 2;
                    break;
                case DayOfWeek.Saturday:
                    day -= 1;
                    break;
                case DayOfWeek.Sunday when day == lastDayOfMonth:
                    day -= 2;
                    break;
                case DayOfWeek.Sunday:
                    day += 1;
                    break;
            }

            results.Add(day);
        }

        return results;
    }

    private NextFireTimeCursor ProgressNextFireTimeDayOfMonth(DateTimeOffset d)
    {
        var day = d.Day;
        var mon = d.Month;
        var t = -1;
        var tmon = mon;

        static bool TryGetMinValueStartingFrom(SortedSet<int> set, int start, out int min)
        {
            min = set.Min;

            if (set.Contains(Constants.AllSpec) || set.Contains(start))
            {
                min = start;
                return true;
            }

            if (set.Count == 0 || set.Max < start)
            {
                return false;
            }

            if (set.Min >= start)
            {
                // value is contained and would be returned from view
                return true;
            }

            // slow path
            var view = set.GetViewBetween(start, int.MaxValue);
            if (view.Count > 0)
            {
                min = view.Min;
                return true;
            }

            return false;
        }

        // get day by day of month rule
        var daysOfMonthCalculated = CalculateDaysOfMonth(d);
        if (TryGetMinValueStartingFrom(daysOfMonthCalculated, d.Day, out var min))
        {
            t = day;
            day = min;

            // make sure we don't over-run a short month, such as february
            var lastDay = GetLastDayOfMonth(mon, d.Year);
            if (day > lastDay)
            {
                day = daysOfMonthCalculated.Min;
                mon++;
            }
        }
        else
        {
            if (_lastDayOfMonth)
            {
                day = daysOfMonthCalculated.Min; //for lastDayOfMonth use calculated fields
            }
            else
            {
                day = _daysOfMonth.Min; //if not then initial set of days uncalculated (to avoid issue with stale weekday in wrong month value)
            }

            mon++;
        }

        if (day != t || mon != tmon)
        {
            if (mon > 12)
            {
                d = new DateTimeOffset(d.Year, 12, day, 0, 0, 0, d.Offset).AddMonths(mon - 12);
            }
            else
            {
                // This is to avoid a bug when moving from a month
                // with 30 or 31 days to a month with less. Causes an invalid datetime to be instantiated.
                // ex. 0 29 0 30 1 ? 2009 with clock set to 1/30/2009
                var lDay = DateTime.DaysInMonth(d.Year, mon);
                if (day <= lDay)
                {
                    d = new DateTimeOffset(d.Year, mon, day, 0, 0, 0, d.Offset);
                }
                else
                {
                    d = new DateTimeOffset(d.Year, mon, lDay, 0, 0, 0, d.Offset).AddDays(day - lDay);
                }
            }

            return new NextFireTimeCursor(true, d);
        }

        return new NextFireTimeCursor(false, d);
    }

    private NextFireTimeCursor ProgressNextFireTimeDayOfWeek(DateTimeOffset d)
    {
        var day = d.Day;
        var mon = d.Month;

        // get day by day of week rule
        if (_lastDayOfWeek)
        {
            // are we looking for the last XXX day of
            // the month?
            var dow = _daysOfWeek.Min; // desired
                                       // d-o-w
            var cDow = (int)d.DayOfWeek + 1; // current d-o-w
            var daysToAdd = 0;
            if (cDow < dow)
            {
                daysToAdd = dow - cDow;
            }

            if (cDow > dow)
            {
                daysToAdd = dow + (7 - cDow);
            }

            var lDay = GetLastDayOfMonth(mon, d.Year);

            if (day + daysToAdd > lDay)
            {
                // did we already miss the
                // last one?
                if (mon == 12)
                {
                    //will we pass the end of the year?
                    d = new DateTimeOffset(d.Year, mon - 11, 1, 0, 0, 0, d.Offset).AddYears(1);
                }
                else
                {
                    d = new DateTimeOffset(d.Year, mon + 1, 1, 0, 0, 0, d.Offset);
                }

                // we are promoting the month
                return new NextFireTimeCursor(true, d);
            }

            // find date of last occurrence of this day in this month...
            while (day + daysToAdd + 7 <= lDay)
            {
                daysToAdd += 7;
            }

            day += daysToAdd;

            if (daysToAdd > 0)
            {
                // we are not promoting the month
                return new NextFireTimeCursor(true, new DateTimeOffset(d.Year, mon, day, 0, 0, 0, d.Offset));
            }
        }
        else if (_nthdayOfWeek != 0)
        {
            // are we looking for the Nth XXX day in the month?
            var dow = _daysOfWeek.Min; // desired
                                       // d-o-w
            var cDow = (int)d.DayOfWeek + 1; // current d-o-w
            var daysToAdd = 0;
            if (cDow < dow)
            {
                daysToAdd = dow - cDow;
            }
            else if (cDow > dow)
            {
                daysToAdd = dow + (7 - cDow);
            }

            var dayShifted = daysToAdd > 0;

            day += daysToAdd;
            var weekOfMonth = day / 7;
            if (day % 7 > 0)
            {
                weekOfMonth++;
            }

            daysToAdd = (_nthdayOfWeek - weekOfMonth) * 7;
            day += daysToAdd;
            if (daysToAdd < 0 || day > GetLastDayOfMonth(mon, d.Year))
            {
                if (mon == 12)
                {
                    d = new DateTimeOffset(d.Year, mon - 11, 1, 0, 0, 0, d.Offset).AddYears(1);
                }
                else
                {
                    d = new DateTimeOffset(d.Year, mon + 1, 1, 0, 0, 0, d.Offset);
                }

                // we are promoting the month
                return new NextFireTimeCursor(true, d);
            }

            if (daysToAdd > 0 || dayShifted)
            {
                // we are NOT promoting the month
                return new NextFireTimeCursor(true, new DateTimeOffset(d.Year, mon, day, 0, 0, 0, d.Offset));
            }
        }
        else if (_everyNthWeek != 0)
        {
            var cDow = (int)d.DayOfWeek + 1; // current d-o-w
            var dow = _daysOfWeek.Min; // desired d-o-w
            if (_daysOfWeek.TryGetMinValueStartingFrom(cDow, out var min))
            {
                dow = min;
            }

            var daysToAdd = 0;
            if (cDow < dow)
            {
                daysToAdd = dow - cDow + 7 * (_everyNthWeek - 1);
            }

            if (cDow > dow)
            {
                daysToAdd = dow + (7 - cDow) + 7 * (_everyNthWeek - 1);
            }

            if (daysToAdd > 0)
            {
                // are we switching days?
                d = new DateTimeOffset(d.Year, mon, day, 0, 0, 0, d.Offset);
                d = d.AddDays(daysToAdd);
                return new NextFireTimeCursor(true, d);
            }
        }
        else
        {
            var cDow = (int)d.DayOfWeek + 1; // current d-o-w
            var dow = _daysOfWeek.Min; // desired d-o-w
            if (_daysOfWeek.TryGetMinValueStartingFrom(cDow, out var min))
            {
                dow = min;
            }

            var daysToAdd = 0;
            if (cDow < dow)
            {
                daysToAdd = dow - cDow;
            }

            if (cDow > dow)
            {
                daysToAdd = dow + (7 - cDow);
            }

            var lDay = GetLastDayOfMonth(mon, d.Year);

            if (day + daysToAdd > lDay)
            {
                // will we pass the end of the month?

                if (mon == 12)
                {
                    //will we pass the end of the year?
                    d = new DateTimeOffset(d.Year, mon - 11, 1, 0, 0, 0, d.Offset).AddYears(1);
                }
                else
                {
                    d = new DateTimeOffset(d.Year, mon + 1, 1, 0, 0, 0, d.Offset);
                }

                // we are promoting the month
                return new NextFireTimeCursor(true, d);
            }

            if (daysToAdd > 0)
            {
                // are we switching days?
                return new NextFireTimeCursor(true, new DateTimeOffset(d.Year, mon, day + daysToAdd, 0, 0, 0, d.Offset));
            }
        }

        return new NextFireTimeCursor(false, new DateTimeOffset(d.Year, d.Month, day, d.Hour, d.Minute, d.Second, d.Offset));
    }

    /// <summary>
    /// Progress next fire time day
    /// </summary>
    /// <param name="d">NextFireTimeCheck</param>
    private NextFireTimeCursor ProgressNextFireTimeDay(DateTimeOffset d)
    {
        var dayOfMSpec = !_daysOfMonth.Contains(Constants.NoSpec);
        var dayOfWSpec = !_daysOfWeek.Contains(Constants.NoSpec);
        if (dayOfMSpec && !dayOfWSpec)
        {
            return ProgressNextFireTimeDayOfMonth(d);
        }

        if (dayOfWSpec && !dayOfMSpec)
        {
            return ProgressNextFireTimeDayOfWeek(d);
        }

        var dayOfMonthProgressResult = ProgressNextFireTimeDayOfMonth(d);
        var dayOfWeekProgressResult = ProgressNextFireTimeDayOfWeek(d);
        if (dayOfMonthProgressResult.RestartLoop && dayOfWeekProgressResult.RestartLoop)
        {
            return dayOfWeekProgressResult.Date < dayOfMonthProgressResult.Date ? dayOfWeekProgressResult : dayOfMonthProgressResult;
        }

        // only 1 result has value then return it
        if (dayOfWeekProgressResult is { Date: { }, RestartLoop: false })
        {
            return dayOfWeekProgressResult;
        }

        if (dayOfMonthProgressResult is { Date: { }, RestartLoop: false })
        {
            return dayOfMonthProgressResult;
        }

        // both results have value, return earliest
        var weekDate = dayOfWeekProgressResult.Date;
        var monthDate = dayOfMonthProgressResult.Date;
        if (weekDate is null)
        {
            return dayOfMonthProgressResult;
        }

        if (monthDate is null)
        {
            return dayOfWeekProgressResult;
        }

        return weekDate.Value < monthDate.Value ? dayOfWeekProgressResult : dayOfMonthProgressResult;
    }

    /// <summary>
    /// Progress next fire time Month
    /// </summary>
    /// <param name="d">NextFireTimeCheck</param>
    private NextFireTimeCursor ProgressNextFireTimeMonth(DateTimeOffset d)
    {
        var mon = d.Month;
        var year = d.Year;
        var t = -1;

        if (_months.TryGetMinValueStartingFrom(mon, out var min))
        {
            t = mon;
            mon = min;
        }
        else
        {
            mon = _months.Min;
            year++;
        }

        return mon != t ? new NextFireTimeCursor(true, new DateTimeOffset(year, mon, 1, 0, 0, 0, d.Offset)) : new NextFireTimeCursor(false, new DateTimeOffset(d.Year, mon, d.Day, d.Hour, d.Minute, d.Second, d.Offset));
    }

    private NextFireTimeCursor ProgressNextFireTimeYear(DateTimeOffset d)
    {
        var year = d.Year;
        int t;
        if (_years.TryGetMinValueStartingFrom(d.Year, out var min))
        {
            t = year;
            year = min;
        }
        else
        {
            // ran out of years...
            return new NextFireTimeCursor(false, null);
        }

        if (year != t)
        {
            return new NextFireTimeCursor(true, new DateTimeOffset(year, 1, 1, 0, 0, 0, d.Offset));
        }

        return new NextFireTimeCursor(false, new DateTimeOffset(year, d.Month, d.Day, d.Hour, d.Minute, d.Second, d.Offset));
    }

    /// <summary>
    /// Gets the next fire time after the given time.
    /// </summary>
    /// <param name="afterTimeUtc">The UTC time to start searching from.</param>
    /// <returns></returns>
    public DateTimeOffset? GetTimeAfter(DateTimeOffset afterTimeUtc)
    {
        // move ahead one second, since we're computing the time *after* the
        // given time
        afterTimeUtc = afterTimeUtc.AddSeconds(1);

        // CronTrigger does not deal with milliseconds
        var d = CreateDateTimeWithoutMilliseconds(afterTimeUtc);

        // change to specified time zone
        d = TimeZoneInfo.ConvertTime(d, TimeZone);

        var nextFireTimeProgressors = new[]
        {
            ProgressNextFireTimeSecond,
            ProgressNextFireTimeMinute,
            ProgressNextFireTimeHour,
            ProgressNextFireTimeDay,
            ProgressNextFireTimeMonth,
            ProgressNextFireTimeYear
        };

        var nextFireTimeCursor = new NextFireTimeCursor(false, d);
        var foundNextFireTime = false;

        // loop until we've computed the next time, or we've past the endTime
        while (!foundNextFireTime)
        {
            foreach (var progressor in nextFireTimeProgressors)
            {
                if (nextFireTimeCursor.Date.HasValue)
                {
                    nextFireTimeCursor = progressor(nextFireTimeCursor.Date.Value);
                }
                else
                {
                    break;
                }

                if (nextFireTimeCursor.RestartLoop)
                {
                    break;
                }
            }

            // test for expressions that never generate a valid fire date,
            if (nextFireTimeCursor.Date == null || nextFireTimeCursor.Date.Value.Year > _maxYear)
            {
                return null; // ran out of years
            }

            if (nextFireTimeCursor.RestartLoop)
            {
                continue;
            }

            var dateTime = nextFireTimeCursor.Date.Value.DateTime;
            var offset = TimeZone.IsAmbiguousTime(dateTime) ? TimeZone.GetAmbiguousTimeOffsets(dateTime).Max() : TimeZone.GetUtcOffset(dateTime);

            // apply the proper offset for this date
            d = new DateTimeOffset(nextFireTimeCursor.Date.Value.DateTime, offset);
            foundNextFireTime = true;
        }

        return d.ToUniversalTime();
    }

    /// <summary>
    /// Creates the date time without milliseconds.
    /// </summary>
    /// <param name="time">The time.</param>
    /// <returns></returns>
    private static DateTimeOffset CreateDateTimeWithoutMilliseconds(DateTimeOffset time)
    {
        return new DateTimeOffset(time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second, time.Offset);
    }

    /// <summary>
    /// Advance the calendar to the particular hour paying particular attention
    /// to daylight saving problems.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="hour">The hour.</param>
    /// <returns></returns>
    private static DateTimeOffset SetCalendarHour(DateTimeOffset date, int hour)
    {
        // Java version of Quartz uses lenient calendar
        // so hour 24 creates day increment and zeroes hour
        var hourToSet = hour;
        if (hourToSet == 24)
        {
            hourToSet = 0;
        }

        var d = new DateTimeOffset(date.Year, date.Month, date.Day, hourToSet, date.Minute, date.Second, date.Millisecond, date.Offset);
        if (hour == 24)
        {
            // increment day
            d = d.AddDays(1);
        }

        return d;
    }

    /// <summary>
    /// Gets the last day of month.
    /// </summary>
    private static int GetLastDayOfMonth(int monthNum, int year)
    {
        return DateTime.DaysInMonth(year, monthNum);
    }

    private static int ToInt32(char c)
    {
        return c - '0';
    }

    private static int ToInt32(ReadOnlySpan<char> span)
    {
        return int.Parse(span);
    }

    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    /// <returns>
    /// A new object that is a copy of this instance.
    /// </returns>
    public object Clone()
    {
        var copy = new CronExpression(CronExpressionString)
        {
            TimeZone = TimeZone
        };

        return copy;
    }

    /// <summary>
    /// Determines whether the specified <see cref="CronExpression"/> is equal to the current <see cref="CronExpression"/>.
    /// </summary>
    /// <returns>
    /// true if the specified <see cref="CronExpression"/> is equal to the current <see cref="CronExpression"/>; otherwise, false.
    /// </returns>
    /// <param name="other">The <see cref="CronExpression"/> to compare with the current <see cref="CronExpression"/>. </param>
    public bool Equals(CronExpression other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Equals(other.CronExpressionString, CronExpressionString) && Equals(other.TimeZone, TimeZone);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != typeof(CronExpression))
        {
            return false;
        }

        return Equals((CronExpression)obj);
    }

    /// <summary>
    /// Serves as a hash function for a particular type.
    /// </summary>
    /// <returns>
    /// A hash code for the current
    /// </returns>
    /// <filterpriority>2</filterpriority>
    public override int GetHashCode()
    {
        return HashCode.Combine(CronExpressionString, _timeZone);
    }

    private readonly record struct ValueSet(int Value, int Position);

    /// <summary>
    /// firetime struct
    /// </summary>
    /// <param name="RestartLoop">Indicate if the Next fire date progressor loop should restart</param>
    /// <param name="Date">NextFireDate calculated progress result</param>
    private readonly record struct NextFireTimeCursor(bool RestartLoop, DateTimeOffset? Date);

    /// <summary>
    /// Optimized structure to hold either one value or multiple.
    /// </summary>
    private sealed class CronField : IEnumerable<int>
    {
        // null == not set, all spec or individual value
        private int? _singleValue;
        private SortedSet<int>? _values;
        private bool _hasAllOrNoSpec;

        public CronField()
        {
            Clear();
        }

        internal int Count
        {
            get
            {
                if (_singleValue is not null)
                {
                    return 1;
                }

                return _values?.Count ?? 0;
            }
        }

        internal int Min
        {
            get
            {
                if (_singleValue is not null)
                {
                    return _hasAllOrNoSpec ? 0 : _singleValue.Value;
                }

                if (_values is not null)
                {
                    return _hasAllOrNoSpec ? 0 : _values.Min;
                }

                return 0;
            }
        }

        internal void Clear()
        {
            _singleValue = null;
            _values = null;
            _hasAllOrNoSpec = false;
        }

        internal bool TryGetMinValueStartingFrom(int start, out int min)
        {
            min = 0;

            if (_singleValue == Constants.AllSpec)
            {
                min = start;
                return true;
            }

            if (_singleValue != null)
            {
                if (_singleValue >= start)
                {
                    min = _singleValue.Value;
                    return true;
                }

                // didn't match
                return false;
            }

            var set = _values;

            if (set == null)
            {
                return false;
            }

            min = set.Min;

            if (set.Contains(start))
            {
                min = start;
                return true;
            }

            if (set.Count == 0 || set.Max < start)
            {
                return false;
            }

            if (set.Min >= start)
            {
                // value is contained and would be returned from view
                return true;
            }

            // slow path
            var view = set.GetViewBetween(start, int.MaxValue);
            if (view.Count > 0)
            {
                min = view.Min;
                return true;
            }

            return false;
        }

        public void Add(int value)
        {
            _hasAllOrNoSpec = value is Constants.AllSpec or Constants.NoSpec;

            if (_singleValue is null)
            {
                if (_values is null)
                {
                    _singleValue = value;
                }
                else
                {
                    _values.Add(value);
                }
            }
            else if (_singleValue != value)
            {
                _values =
                [
                    _singleValue.Value,
                    value
                ];
                _singleValue = null;
            }
        }

        public bool Contains(int value)
        {
            if (_singleValue == value || value != Constants.AllSpec && value != Constants.NoSpec && _hasAllOrNoSpec)
            {
                return true;
            }

            return _values != null && _values.Contains(value);
        }

        public IEnumerator<int> GetEnumerator()
        {
            if (_singleValue is not null)
            {
                yield return _singleValue.Value;
                yield break;
            }

            if (_values is not null)
            {
                foreach (var value in _values)
                {
                    yield return value;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private static class Constants
    {
        /// <summary>
        /// Field specification for second.
        /// </summary>
        public const int Second = 0;

        /// <summary>
        /// Field specification for minute.
        /// </summary>
        public const int Minute = 1;

        /// <summary>
        /// Field specification for hour.
        /// </summary>
        public const int Hour = 2;

        /// <summary>
        /// Field specification for day of month.
        /// </summary>
        public const int DayOfMonth = 3;

        /// <summary>
        /// Field specification for month.
        /// </summary>
        public const int Month = 4;

        /// <summary>
        /// Field specification for day of week.
        /// </summary>
        public const int DayOfWeek = 5;

        /// <summary>
        /// Field specification for year.
        /// </summary>
        public const int Year = 6;

        /// <summary>
        /// Field specification for wildcard '*'.
        /// </summary>
        public const int AllSpec = 99;

        /// <summary>
        /// Field specification for no specification at all '?'.
        /// </summary>
        public const int NoSpec = 98;
    }

    [GeneratedRegex("^L(-\\d{1,2})?(W(-\\d{1,2})?)?$", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
    [GeneratedRegex("LW-(?<offset>[0-9]+)", RegexOptions.Compiled)]
    private static partial Regex MyRegex1();
}