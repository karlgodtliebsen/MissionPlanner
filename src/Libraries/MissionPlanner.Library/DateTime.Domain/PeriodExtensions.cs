namespace MissionPlanner.Library.DateTime.Domain;

/// <summary>
/// Provides extension methods for creating <see cref="Period"/> instances from various date and time representations.
/// </summary>
public static class PeriodExtensions
{
    /// <summary>
    /// Provides the public API for As24HourPeriod.
    /// </summary>
    public static Period As24HourPeriod(this DateTimeOffset dateTimeOffset, int span)
    {
        return new Period(dateTimeOffset.Date, dateTimeOffset.Date.AddDays(span));
    }

    /// <summary>
    /// Provides the public API for As24HourPeriod.
    /// </summary>
    public static Period As24HourPeriod(this IDateTimeProvider clock, int span)
    {
        return new Period(clock.UtcNow.Date, clock.UtcNow.Date.AddDays(span));
    }

    /// <summary>
    /// Provides the public API for AsPeriod.
    /// </summary>
    public static Period AsPeriod(this IDateTimeProvider clock, int span)
    {
        return new Period(clock.UtcNow, clock.UtcNow.AddDays(span));
    }

    /// <summary>
    /// Provides the public API for AsPeriod.
    /// </summary>
    public static Period AsPeriod(this IDateTimeProvider clock, TimeSpan dt)
    {
        return new Period(clock.UtcNow, clock.UtcNow.Add(dt));
    }
}
