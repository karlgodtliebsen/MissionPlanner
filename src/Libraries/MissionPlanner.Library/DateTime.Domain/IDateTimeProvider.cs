namespace Domain.Library.DateTime.Domain;

/// <summary>
/// Provides the current date and time.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current time.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
