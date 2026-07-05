namespace MissionPlanner.Transport;

/// <summary>
/// Represents an endpoint for a transport connection, including protocol, address, and port information.
/// </summary>
public class TransportEndpoint
{
    /// <summary>
    /// The name of the transport endpoint.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The expected ID of the transport endpoint.
    /// </summary>
    public string ExpectedId { get; set; }


    /// <summary>
    /// The port number of the remote endpoint.
    /// </summary>
    public int RemotePort { get; set; }

    /// <summary>
    /// The protocol used by the transport endpoint (e.g., "udp", "tcp").
    /// </summary>
    public string Protocol { get; set; }


    /// <summary>
    /// The host address of the remote endpoint.
    /// </summary>
    public string RemoteHost { get; set; }

    /// <summary>
    /// The port number of the local endpoint.
    /// </summary>
    public int LocalPort { get; set; }

    /// <summary>
    /// The host address of the local endpoint.
    /// </summary>
    public string LocalHost { get; set; } = "127.0.0.1"; // or "0.0.0.0"

    /// <summary>
    /// 
    /// </summary>
    public int ReceiveBufferSize { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransportEndpoint"/> class with the specified protocol, remote port, remote host, local port, local host, and receive buffer size.
    /// </summary>
    /// <param name="protocol">The protocol used by the transport endpoint (e.g., "udp", "tcp").</param>
    /// <param name="remotePort">The port number of the remote endpoint.</param>
    /// <param name="remoteHost">The host address of the remote endpoint.</param>
    /// <param name="localPort">The port number of the local endpoint.</param>
    /// <param name="localHost">The host address of the local endpoint.</param>
    /// <param name="receiveBufferSize">The size of the receive buffer.</param>
    public TransportEndpoint(string protocol = "udp", int remotePort = 14551, string remoteHost = "127.0.0.1", int localPort = 14550, string localHost = "0.0.0.0", int receiveBufferSize = 512)
    {
        Protocol = protocol;
        RemotePort = remotePort;
        RemoteHost = remoteHost;
        LocalPort = localPort;
        LocalHost = localHost;
        ReceiveBufferSize = receiveBufferSize;
    }
}