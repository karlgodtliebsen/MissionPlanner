namespace Domain.Library.DateTime.Domain;

/// <summary>
/// Represents a period of time with optional start and end dates.
/// </summary>
public class NullablePeriod
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NullablePeriod"/> class with no start or end dates.
    /// </summary>
    public NullablePeriod()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NullablePeriod"/> class with a specified start date and no end date.
    /// </summary>
    /// <param name="from"></param>
    public NullablePeriod(IDateTimeProvider from)
    {
        From = from.UtcNow.DateTime;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NullablePeriod"/> class with a specified start and end date.
    /// </summary>
    /// <param name="period"></param>
    public NullablePeriod((IDateTimeProvider from, IDateTimeProvider to) period)
    {
        From = period.from.UtcNow.DateTime;
        To = period.to.UtcNow.DateTime;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NullablePeriod"/> class with a specified start date and an optional end date.
    /// </summary>
    /// <param name="period"></param>
    public NullablePeriod((System.DateTime from, System.DateTime? to) period)
    {
        From = period.from;
        To = period.to;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NullablePeriod"/> class with a specified start date and no end date.
    /// </summary>
    /// <param name="from"></param>
    public NullablePeriod(System.DateTime? from)
    {
        From = from;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NullablePeriod"/> class with a specified start and end date.
    /// </summary>
    /// <param name="period"></param>
    public NullablePeriod((System.DateTime? from, System.DateTime? to) period)
    {
        From = period.from;
        To = period.to;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NullablePeriod"/> class with a specified start and end date.
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    public NullablePeriod(System.DateTime from, System.DateTime to)
    {
        From = from;
        To = to;
    }

    public NullablePeriod(DateTimeOffset from, DateTimeOffset to)
    {
        From = from.DateTime;
        To = to.DateTime;
    }

    public NullablePeriod(DateTimeOffset from, DateTimeOffset? to)
    {
        From = from.DateTime;
        if (to != null)
        {
            To = to.Value.DateTime;
        }
    }

    public System.DateTime? From { get; private set; }
    public System.DateTime? To { get; set; } = default!;

    public bool IsOpenEnded => To == default!;

    public void Close(System.DateTime closeAt)
    {
        To = closeAt;
    }
}
