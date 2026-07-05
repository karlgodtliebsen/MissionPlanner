namespace Domain.Library.DateTime.Domain;

/// <summary>
/// Provides the current date and time. This class can be used
/// </summary>
public class DateTimeProvider : IDateTimeProvider
{
    private readonly Func<DateTimeOffset> getDateTimeOffset;

    //
    /// <summary>
    ///  Default constructor returns the current UTC time.
    /// </summary>
    public DateTimeProvider()
    {
        getDateTimeOffset = () => DateTimeOffset.UtcNow;
    }

    //
    /// <summary>
    ///  Parameterized constructor returns a fixed UTC time.
    /// </summary>
    /// <param name="utcNow"></param>
    public DateTimeProvider(DateTimeOffset utcNow)
    {
        getDateTimeOffset = () => utcNow;
    }

    /// <inheritdoc/>
    public DateTimeOffset UtcNow => getDateTimeOffset();
}
