namespace Domain.Library.DateTime.Domain;

public static class DateTimeProviderExtensions
{
    public static DateTimeOffset AsDateTimeOffset(this IDateTimeProvider clock)
    {
        return clock.UtcNow;
    }

    public static IDateTimeProvider AsFixedDateTime(this DateTimeOffset dateTimeOffset)
    {
        return new DateTimeProvider(dateTimeOffset);
    }
}
