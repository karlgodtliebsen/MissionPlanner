namespace MissionPlanner.Library.DateTime.Domain;

/// <summary>
/// Represents a period of time with a start date and an optional end date.
/// </summary>
public sealed class OpenPeriod
{
    /// <summary>
    /// Provides the public API for OpenPeriod.
    /// </summary>
    public OpenPeriod()
    {
    }

    /// <summary>
    /// Provides the public API for OpenPeriod.
    /// </summary>
    public OpenPeriod((System.DateTime from, System.DateTime to) period)
    {
        From = period.from;
        To = period.to;
    }

    /// <summary>
    /// Provides the public API for OpenPeriod.
    /// </summary>
    public OpenPeriod(System.DateTime from)
    {
        From = from;
    }

    /// <summary>
    /// Provides the public API for OpenPeriod.
    /// </summary>
    public OpenPeriod(IDateTimeProvider from)
    {
        From = from.UtcNow.DateTime;
    }

    /// <summary>
    /// Provides the public API for OpenPeriod.
    /// </summary>
    public OpenPeriod((IDateTimeProvider from, IDateTimeProvider to) period)
    {
        From = period.from.UtcNow.DateTime;
        To = period.to.UtcNow.DateTime;
    }

    /// <summary>
    /// Provides the public API for From.
    /// </summary>
    public DateTimeOffset From { get; private init; }
    /// <summary>
    /// Provides the public API for To.
    /// </summary>
    public DateTimeOffset? To { get; set; } = default!;

    /// <summary>
    /// Provides the public API for IsOpen.
    /// </summary>
    public bool IsOpen => From == default && To == default!;
    /// <summary>
    /// Provides the public API for IsOpenEnded.
    /// </summary>
    public bool IsOpenEnded => To == default!;

    /// <summary>
    /// Provides the public API for Close.
    /// </summary>
    public void Close(IDateTimeProvider closeAt)
    {
        To = closeAt.UtcNow.DateTime;
    }

    /// <summary>
    /// Provides the public API for Close.
    /// </summary>
    public void Close(DateTimeOffset closeAt)
    {
        To = closeAt;
    }

    /// <summary>
    /// Provides the public API for implicit operator OpenPeriod.
    /// </summary>
    public static implicit operator OpenPeriod((System.DateTime from, System.DateTime? to) periodTuple)
    {
        return new OpenPeriod { From = periodTuple.from, To = periodTuple.to };
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
        return other == null
            ? false
            : (other.From <= From && (!other.To.HasValue || other.To >= From)) ||
              (other.From >= From && (other.From <= To || !To.HasValue));
    }

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
