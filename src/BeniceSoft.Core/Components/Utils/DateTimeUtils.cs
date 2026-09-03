using System.Globalization;

namespace BeniceSoft.Core;

public static class DateTimeUtils
{
    #region Lunar Calendar Correlation
    /// <summary>
    /// 中国日历
    /// </summary>
    private static readonly Lazy<ChineseLunisolarCalendar> _china = new();

    /// <summary>
    /// get lunar year
    /// </summary>
    /// <param name="time">gregorian calendar</param>
    /// <returns>result</returns>
    public static string GetLunarYear(DateTime time)
    {
        var yearIndex = _china.Value.GetSexagenaryYear(time);
        var celestial = "甲乙丙丁戊己庚辛壬癸";
        var branch = "子丑寅卯辰巳午未申酉戌亥";
        var animal = "鼠牛虎兔龙蛇马羊猴鸡狗猪";
        var year = _china.Value.GetYear(time);
        var celestialNumber = _china.Value.GetCelestialStem(yearIndex);
        var branchNumber = _china.Value.GetTerrestrialBranch(yearIndex);
        var result = $"[{animal[branchNumber - 1]}]{celestial[celestialNumber - 1]}{branch[branchNumber - 1]}{year}";
        return result;
    }

    /// <summary>
    /// get lunar month
    /// </summary>
    /// <param name="time">gregorian calendar</param>
    /// <returns>result</returns>
    public static string GetLunarMonth(DateTime time)
    {
        var year = _china.Value.GetYear(time);
        var month = _china.Value.GetMonth(time);
        var leapMonth = _china.Value.GetLeapMonth(year);

        if (leapMonth != 0 && month >= leapMonth)
        {
            month--;
        }

        var monthHead = "正二三四五六七八九十";
        var isLeapMonth = month == leapMonth;
        var result = isLeapMonth ? "闰" : string.Empty;

        if (month <= 10)
        {
            result += monthHead[month - 1];
        }
        else if (month == 11)
        {
            result += "十一";
        }
        else
        {
            result += "腊";
        }

        result += "月";
        return result;
    }

    /// <summary>
    /// get lunar day
    /// </summary>
    /// <param name="time">gregorian calendar</param>
    /// <returns>result</returns>
    public static string GetLunarDay(DateTime time)
    {
        var day = _china.Value.GetDayOfMonth(time);
        var dayDecade = "初十廿三";
        var dayUnits = "一二三四五六七八九十";
        string result;

        if (day == 20)
        {
            result = "二十";
        }
        else if (day == 30)
        {
            result = "三十";
        }
        else
        {
            result = dayDecade[(day - 1) / 10].ToString();
            result += dayUnits[(day - 1) % 10];
        }

        return result;
    }

    /// <summary>
    /// get solar term
    /// </summary>
    /// <param name="time">gregorian calendar</param>
    /// <returns></returns>
    public static string GetSolarTerm(DateTime time)
    {
        var solarterms = new string[] { "小寒", "大寒", "立春", "雨水", "惊蛰", "春分", "清明", "谷雨", "立夏", "小满", "芒种", "夏至", "小暑", "大暑", "立秋", "处暑", "白露", "秋分", "寒露", "霜降", "立冬", "小雪", "大雪", "冬至" };

        var solartermsData = new int[] { 0, 21208, 42467, 63836, 85337, 107014, 128867, 150921, 173149, 195551, 218072, 240693, 263343, 285989, 308563, 331033, 353350, 375494, 397447, 419210, 440795, 462224, 483532, 504758 };

        var dtBase = new DateTime(1900, 1, 6, 2, 5, 0);
        var result = string.Empty;

        for (var i = 1; i <= 24; i++)
        {
            var num = 525948.76 * (time.Year - 1900) + solartermsData[i - 1];
            var dtNew = dtBase.AddMinutes(num);

            if (dtNew.DayOfYear == time.DayOfYear)
            {
                result = solarterms[i - 1];
            }
        }

        return result;
    }
    #endregion

    #region Common
    /// <summary>
    /// converts a DateTime to local time (with special handling for MinValue and MaxValue).
    /// </summary>
    /// <param name="dateTime">A DateTime.</param>
    /// <param name="kind">A DateTimeKind.</param>
    /// <returns>The DateTime in local time.</returns>
    public static DateTime SpecifyKind(DateTime dateTime, DateTimeKind kind = DateTimeKind.Utc)
    {
        if (dateTime.Kind == kind)
        {
            return dateTime;
        }
        else
        {
            if (dateTime == DateTime.MinValue)
            {
                return DateTime.SpecifyKind(DateTime.MinValue, kind);
            }
            else if (dateTime == DateTime.MaxValue)
            {
                return DateTime.SpecifyKind(DateTime.MaxValue, kind);
            }
            else
            {
                return DateTime.SpecifyKind(dateTime.ToLocalTime(), kind);
            }
        }
    }

    /// <summary>
    /// 获取目标时间周一
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static DateTime FirstDayOfWeek(DateTime value)
    {
        var offset = value.DayOfWeek - DayOfWeek.Monday;
        if (offset < 0)
        {
            offset = 6;
        }

        return value.Date.AddDays(-offset);
    }

    /// <summary>
    /// 获取目标时间的周日
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static DateTime LastDayOfWeek(DateTime value)
    {
        var offset = value.DayOfWeek - DayOfWeek.Sunday;
        if (offset == 0)
        {
            offset = 7;
        }

        offset = 7 - offset;
        return value.Date.AddDays(offset);
    }

    /// <summary>
    /// 获取目标时间当月第一天
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static DateTime FirstDayOfMonth(DateTime value)
    {
        return new(value.Year, value.Month, 1);
    }

    /// <summary>
    /// 获取目标时间当月第一天
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static DateTimeOffset FirstDayOfMonth(DateTimeOffset value)
    {
        return new(new DateOnly(value.Year, value.Month, 1), TimeOnly.MinValue, value.Offset);
    }

    /// <summary>
    /// 获取目标时间当月最后一天
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static DateTime LastDayOfMonth(DateTime value)
    {
        return new(value.Year, value.Month, DateTime.DaysInMonth(value.Year, value.Month));
    }

    /// <summary>
    /// 获取目标时间当月最后一天
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static DateTimeOffset LastDayOfMonth(DateTimeOffset value)
    {
        return new(new DateOnly(value.Year, value.Month, DateTime.DaysInMonth(value.Year, value.Month)), TimeOnly.MinValue, value.Offset);
    }

    public static long Timestamp(DateTime dateTime)
    {
        DateTimeOffset offset = dateTime;
        return offset.ToUnixTimeSeconds();
    }

    public static long TimestampMs(DateTime dateTime)
    {
        DateTimeOffset offset = dateTime;
        return offset.ToUnixTimeMilliseconds();
    }
    #endregion

    #region Extensions

    public static TimeOnly ToTimeOnly(this TimeSpan aim)
    {
        return TimeOnly.FromTimeSpan(aim);
    }

    public static TimeOnly ToTimeOnly(this DateTime aim)
    {
        return TimeOnly.FromDateTime(aim);
    }

    public static TimeOnly ToTimeOnly(this DateTimeOffset aim)
    {
        return aim.DateTime.ToTimeOnly();
    }

    public static DateOnly ToDateOnly(this DateTime aim)
    {
        return DateOnly.FromDateTime(aim);
    }

    public static DateOnly ToDateOnly(this DateTimeOffset aim)
    {
        return aim.DateTime.ToDateOnly();
    }

    public static int Subtract(this DateOnly aim, DateOnly value)
    {
        return aim.DayNumber - value.DayNumber;
    }

    public static DateTime ToDateTime(this DateOnly aim)
    {
        return aim.ToDateTime(TimeOnly.MinValue);
    }

    public static DateTimeOffset ToDateTimeOffset(this DateOnly aim)
    {
        return aim.ToDateTime();
    }

    public static DateTimeOffset ToDateTimeOffset(this DateOnly aim, TimeOnly time, DateTimeKind kind = DateTimeKind.Unspecified)
    {
        return aim.ToDateTime(time, kind);
    }
    #endregion
}
