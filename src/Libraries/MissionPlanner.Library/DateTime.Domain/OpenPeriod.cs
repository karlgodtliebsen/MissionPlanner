namespace Domain.Library.DateTime.Domain;

/// <summary>
/// Represents a period of time with a start date and an optional end date.
/// </summary>
public sealed class OpenPeriod
{
    public OpenPeriod()
    {
    }

    public OpenPeriod((System.DateTime from, System.DateTime to) period)
    {
        From = period.from;
        To = period.to;
    }

    public OpenPeriod(System.DateTime from)
    {
        From = from;
    }

    public OpenPeriod(IDateTimeProvider from)
    {
        From = from.UtcNow.DateTime;
    }

    public OpenPeriod((IDateTimeProvider from, IDateTimeProvider to) period)
    {
        From = period.from.UtcNow.DateTime;
        To = period.to.UtcNow.DateTime;
    }

    public DateTimeOffset From { get; private init; }
    public DateTimeOffset? To { get; set; } = default!;

    public bool IsOpen => From == default && To == default!;
    public bool IsOpenEnded => To == default!;

    public void Close(IDateTimeProvider closeAt)
    {
        To = closeAt.UtcNow.DateTime;
    }

    public void Close(DateTimeOffset closeAt)
    {
        To = closeAt;
    }

    public static implicit operator OpenPeriod((System.DateTime from, System.DateTime? to) periodTuple)
    {
        return new OpenPeriod
        {
            From = periodTuple.from,
            To = periodTuple.to
        };
    }

    public bool Contains(DateTimeOffset date)
    {
        return date >= From && (!To.HasValue || date <= To.Value);
    }

    public bool Overlaps(Period? other)
    {
        return other == null
            ? false
            : (other.From <= From && (!other.To.HasValue || other.To >= From)) ||
              (other.From >= From && (other.From <= To || !To.HasValue));
    }

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
