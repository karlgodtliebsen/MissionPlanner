using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Domain event published when a connection attempt fails.
/// </summary>
public class ConnectionFailed : DomainEvent<ConnectionFailedData>
{
    /// <summary>
    /// Provides the public API for ConnectionType.
    /// </summary>
    public string ConnectionType => ((ConnectionFailedData)Payload!).ConnectionType;
    /// <summary>
    /// Provides the public API for Endpoint.
    /// </summary>
    public string Endpoint => ((ConnectionFailedData)Payload!).Endpoint;
    /// <summary>
    /// Provides the public API for Error.
    /// </summary>
    public string Error => ((ConnectionFailedData)Payload!).Error;
    /// <summary>
    /// Provides the public API for FailedAt.
    /// </summary>
    public DateTimeOffset FailedAt => ((ConnectionFailedData)Payload!).FailedAt;

    /// <summary>
    /// Provides the public API for ConnectionFailed.
    /// </summary>
    public ConnectionFailed(string connectionType, string endpoint, string error, DateTimeOffset failedAt)
        : base("ConnectionFailed", new ConnectionFailedData(connectionType, endpoint, error, failedAt))
    {
    }
}
