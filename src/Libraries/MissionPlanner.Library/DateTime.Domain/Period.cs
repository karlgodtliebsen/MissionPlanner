namespace MissionPlanner.Library.DateTime.Domain;

/// <summary>
/// Represents a period of time with a start date and an optional end date.
/// </summary>
public sealed class Period
{
    /// <summary>
    /// Provides the public API for Period.
    /// </summary>
    public Period()
    {
    }

    /// <summary>
    /// Provides the public API for Period.
    /// </summary>
    public Period(IDateTimeProvider from)
    {
        From = from.UtcNow.DateTime;
    }

    /// <summary>
    /// Provides the public API for Period.
    /// </summary>
    public Period((IDateTimeProvider from, IDateTimeProvider to) period)
    {
        From = period.from.UtcNow.DateTime;
        To = period.to.UtcNow.DateTime;
    }

    /// <summary>
    /// Provides the public API for Period.
    /// </summary>
    public Period((DateTimeOffset from, DateTimeOffset? to) period)
    {
        From = period.from;
        To = period.to;
    }


    /// <summary>
    /// Provides the public API for Period.
    /// </summary>
    public Period(DateTimeOffset from, DateTimeOffset to)
    {
        From = from;
        To = to;
    }

    /// <summary>
    /// Provides the public API for Period.
    /// </summary>
    public Period(DateTimeOffset from, DateTimeOffset? to)
    {
        From = from.DateTime;
        if (to != null)
        {
            To = to.Value.DateTime;
        }
    }

    /// <summary>
    /// Provides the public API for From.
    /// </summary>
    public DateTimeOffset From { get; private set; }
    /// <summary>
    /// Provides the public API for To.
    /// </summary>
    public DateTimeOffset? To { get; set; } = null!;

    /// <summary>
    /// Provides the public API for IsOpenEnded.
    /// </summary>
    public bool IsOpenEnded => To == null!;

    /// <summary>
    /// Provides the public API for Close.
    /// </summary>
    public void Close(DateTimeOffset closeAt)
    {
        To = closeAt;
    }

    /// <summary>
    /// Provides the public API for implicit operator Period.
    /// </summary>
    public static implicit operator Period((DateTimeOffset from, DateTimeOffset to) periodTuple)
    {
        return new Period(periodTuple);
    }

    /// <summary>
    /// Provides the public API for implicit operator Period.
    /// </summary>
    public static implicit operator Period((DateTimeOffset from, DateTimeOffset? to) periodTuple)
    {
        return new Period(periodTuple);
    }


    /// <summary>
    /// Provides the public API for Contains.
    /// </summary>
    public bool Contains(DateTimeOffset date)
    {
        return date >= From && (!To.HasValue || date <= To.Value);
    }

    /// <summary>
    /// Provides the public API for Overlaps.
    /// </summary>
    public bool Overlaps(Period? other)
    {
        return other != null && ((other.From <= From && (!other.To.HasValue || other.To >= From)) ||
                                 (other.From >= From && (other.From <= To || !To.HasValue)));
    }

    /// <summary>
    /// Provides the public API for FromAsString.
    /// </summary>
    public string FromAsString => From.ToString();

    /// <summary>
    /// Provides the public API for ToAsString.
    /// </summary>
    public string ToAsString => To.HasValue ? To.Value.ToString() : string.Empty;
    /// <summary>
    /// Provides the public API for AsString.
    /// </summary>
    public string AsString => $"{From}-{To}";


    /// <summary>
    /// Provides the public API for GetOverlap.
    /// </summary>
    public Period? GetOverlap(Period other)
    {
        if (!Overlaps(other))
        {
            return null;
        }

        var maxFrom = From > other.From ? From : other.From;
        var minTo = !To.HasValue ? other.To :
            !other.To.HasValue ? To :
            To < other.To ? To : other.To;

        return new Period(maxFrom, minTo);
    }
}
