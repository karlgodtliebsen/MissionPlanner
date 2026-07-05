using System.Globalization;

namespace Domain.Library.Math;

/// <summary>
/// Provides methods for performing various mathematical calculations.
/// </summary>
public static class Formulas
{
    // This presumes that weeks start with Monday.
    // Week 1 is the 1st week of the year with a Thursday in it.
    /// <summary>
    /// Calculates the ISO 8601 week number for a given date.
    /// </summary>
    /// <param name="time">The date for which to calculate the week number.</param>
    /// <returns>The ISO 8601 week number.</returns>
    public static int GetIso8601WeekOfYear(System.DateTime time)
    {
        // Seriously cheat.  If it is Monday, Tuesday or Wednesday, then it'll 
        // be the same week# as whatever Thursday, Friday or Saturday are,
        // and we always get those right
        var day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(time);
        if (day is >= DayOfWeek.Monday and <= DayOfWeek.Wednesday)
        {
            time = time.AddDays(3);
        }

        // Return the week of our adjusted day
        return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(time, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }

    /// <summary>
    /// Calculates the first date of a given ISO 8601 week in a specified year.
    /// </summary>
    /// <param name="year">The year for which to calculate the first date of the week.</param>
    /// <param name="weekOfYear">The ISO 8601 week number.</param>
    /// <returns>The first date of the specified ISO 8601 week.</returns>
    public static System.DateTime FirstDateOfWeekISO8601(int year, int weekOfYear)
    {
        var jan1 = new System.DateTime(year, 1, 1); //, DateTimeKind.Utc
        var daysOffset = DayOfWeek.Thursday - jan1.DayOfWeek;

        var firstThursday = jan1.AddDays(daysOffset);
        var cal = CultureInfo.CurrentCulture.Calendar;
        var firstWeek = cal.GetWeekOfYear(firstThursday, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

        var weekNum = weekOfYear;
        if (firstWeek <= 1)
        {
            weekNum -= 1;
        }

        var result = firstThursday.AddDays(weekNum * 7);
        return result.AddDays(-3);
    }

    // <summary>
    /// Returns the first day of the week that the specified
    /// date is in using the current culture. 
    /// </summary>
    /// <param name="dayInWeek">The date for which to find the first day of the week.</param>
    /// <returns>The first day of the week for the specified date.</returns>
    public static System.DateTime GetFirstDayOfWeek(this System.DateTime dayInWeek)
    {
        var defaultCultureInfo = CultureInfo.CurrentCulture;
        return GetFirstDayOfWeek(dayInWeek, defaultCultureInfo);
    }

    /// <summary>
    /// Returns the first day of the week that the specified date 
    /// is in. 
    /// </summary>
    public static System.DateTime GetFirstDayOfWeek(this System.DateTime dayInWeek, CultureInfo cultureInfo)
    {
        var firstDay = cultureInfo.DateTimeFormat.FirstDayOfWeek;
        var firstDayInWeek = dayInWeek.Date;
        while (firstDayInWeek.DayOfWeek != firstDay)
        {
            firstDayInWeek = firstDayInWeek.AddDays(-1);
        }

        return firstDayInWeek;
    }
}
