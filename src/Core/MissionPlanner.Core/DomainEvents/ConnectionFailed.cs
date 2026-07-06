using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Domain event published when a connection attempt fails.
/// </summary>
public class ConnectionFailed : DomainEvent<ConnectionFailedData>
{
    public string ConnectionType => ((ConnectionFailedData)Payload!).ConnectionType;
    public string Endpoint => ((ConnectionFailedData)Payload!).Endpoint;
    public string Error => ((ConnectionFailedData)Payload!).Error;
    public DateTimeOffset FailedAt => ((ConnectionFailedData)Payload!).FailedAt;

    public ConnectionFailed(string connectionType, string endpoint, string error, DateTimeOffset failedAt)
        : base("ConnectionFailed", new ConnectionFailedData(connectionType, endpoint, error, failedAt))
    {
    }
}

/// <summary>
/// Data payload for ConnectionFailed event.
/// </summary>
public record ConnectionFailedData(
    string ConnectionType,
    string Endpoint,
    string Error,
    DateTimeOffset FailedAt);
