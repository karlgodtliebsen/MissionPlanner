namespace MissionPlanner.Library.DateTime.Domain;

/// <summary>
/// Provides the public API for DateTimeProviderExtensions.
/// </summary>
public static class DateTimeProviderExtensions
{
    /// <summary>
    /// Provides the public API for AsDateTimeOffset.
    /// </summary>
    public static DateTimeOffset AsDateTimeOffset(this IDateTimeProvider clock)
    {
        return clock.UtcNow;
    }

    /// <summary>
    /// Provides the public API for AsFixedDateTime.
    /// </summary>
    public static IDateTimeProvider AsFixedDateTime(this DateTimeOffset dateTimeOffset)
    {
        return new DateTimeProvider(dateTimeOffset);
    }
}
