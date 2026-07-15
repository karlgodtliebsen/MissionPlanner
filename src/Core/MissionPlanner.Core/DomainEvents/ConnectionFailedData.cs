namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Data payload for ConnectionFailed event.
/// </summary>
public record ConnectionFailedData(string ConnectionType, string Endpoint, string Error, DateTimeOffset FailedAt);
