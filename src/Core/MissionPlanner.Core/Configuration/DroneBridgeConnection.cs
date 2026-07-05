namespace MissionPlanner.Core.Configuration;

/// <summary>
/// Represents an endpoint for a transport connection, including protocol, address, and port information.
/// </summary>
public class DroneBridgeConnection
{
    /// <summary>
    /// The name of the transport endpoint.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The expected ID of the transport endpoint.
    /// </summary>
    public string ExpectedSystemId { get; set; } = string.Empty;


    /// <summary>
    /// The port number of the remote endpoint.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// The protocol used by the transport endpoint (e.g., "udp", "tcp").
    /// </summary>
    public string Protocol { get; set; } = string.Empty;


    /// <summary>
    /// The host address of the remote endpoint.
    /// </summary>
    public string Host { get; set; } = string.Empty;
}