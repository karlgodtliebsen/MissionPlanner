namespace Domain.Library.DateTime.Domain;

/// <summary>
/// Represents a period of time with a start date and an optional end date.
/// </summary>
public sealed class Period
{
    public Period()
    {
    }

    public Period(IDateTimeProvider from)
    {
        From = from.UtcNow.DateTime;
    }

    public Period((IDateTimeProvider from, IDateTimeProvider to) period)
    {
        From = period.from.UtcNow.DateTime;
        To = period.to.UtcNow.DateTime;
    }

    public Period((DateTimeOffset from, DateTimeOffset? to) period)
    {
        From = period.from;
        To = period.to;
    }


    public Period(DateTimeOffset from, DateTimeOffset to)
    {
        From = from;
        To = to;
    }

    public Period(DateTimeOffset from, DateTimeOffset? to)
    {
        From = from.DateTime;
        if (to != null)
        {
            To = to.Value.DateTime;
        }
    }

    public DateTimeOffset From { get; private set; }
    public DateTimeOffset? To { get; set; } = null!;

    public bool IsOpenEnded => To == null!;

    public void Close(DateTimeOffset closeAt)
    {
        To = closeAt;
    }

    public static implicit operator Period((DateTimeOffset from, DateTimeOffset to) periodTuple)
    {
        return new Period(periodTuple);
    }

    public static implicit operator Period((DateTimeOffset from, DateTimeOffset? to) periodTuple)
    {
        return new Period(periodTuple);
    }


    public bool Contains(DateTimeOffset date)
    {
        return date >= From && (!To.HasValue || date <= To.Value);
    }

    public bool Overlaps(Period? other)
    {
        return other != null && ((other.From <= From && (!other.To.HasValue || other.To >= From)) ||
                                 (other.From >= From && (other.From <= To || !To.HasValue)));
    }

    public string FromAsString => From.ToString();

    public string ToAsString => To.HasValue ? To.Value.ToString() : string.Empty;
    public string AsString => $"{From}-{To}";


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
