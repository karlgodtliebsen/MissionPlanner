using System.Net;

namespace MissionPlanner.Transport;

/// <summary>
/// Represents a combined endpoint that can be initialized with either a string or an <see cref="IPEndPoint"/>.
/// </summary>
public sealed class TransportEndPoint
{
    private readonly IPEndPoint? iPEndPoint;
    private readonly string? endpoint;

    /// <summary>
    /// Gets the name of the transport.
    /// </summary>
    public string? TransportName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransportEndPoint"/> class with the specified endpoint string.
    /// </summary>
    /// <param name="endpoint">The endpoint string to use.</param>
    public TransportEndPoint(string endpoint)
    {
        this.endpoint = endpoint;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransportEndPoint"/> class with the specified transport name and endpoint string.
    /// </summary>
    /// <param name="transportName">The name of the transport.</param>
    /// <param name="endpoint">The endpoint string to use.</param>
    public TransportEndPoint(string transportName, string endpoint)
    {
        TransportName = transportName;
        this.endpoint = endpoint;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransportEndPoint"/> class with the specified transport name, address, and port.
    /// </summary>
    /// <param name="transportName">The name of the transport.</param>
    /// <param name="address">The address of the endpoint.</param>
    /// <param name="port">The port of the endpoint.</param>
    public TransportEndPoint(string transportName, string address, int port)
    {
        TransportName = transportName;
        endpoint = $"{address}:{port}";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransportEndPoint"/> class with the specified <see cref="IPEndPoint"/>.
    /// </summary>
    /// <param name="iPEndPoint">The <see cref="IPEndPoint"/> to use.</param>
    public TransportEndPoint(IPEndPoint iPEndPoint)
    {
        this.iPEndPoint = iPEndPoint;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransportEndPoint"/> class with the specified transport name and <see cref="IPEndPoint"/>.
    /// </summary>
    /// <param name="transportName">The name of the transport.</param>
    /// <param name="iPEndPoint">The <see cref="IPEndPoint"/> to use.</param>
    public TransportEndPoint(string transportName, IPEndPoint iPEndPoint)
    {
        TransportName = transportName;
        this.iPEndPoint = iPEndPoint;
    }


    /// <inheritdoc />
    public override string ToString()
    {
        return endpoint ?? iPEndPoint?.ToString() ?? throw new InvalidOperationException("Both endpoint and iPEndPoint are null.");
    }

    /// <summary>
    /// Returns the endpoint as an IP endpoint .
    /// </summary>
    /// <returns>The IP endpoint.</returns>
    public IPEndPoint ToIPEndPoint()
    {
        if (iPEndPoint is not null)
        {
            return iPEndPoint;
        }

        if (endpoint is not null)
        {
            var parts = endpoint.Split(":");
            var ipEndpoint = new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
            return ipEndpoint;
        }

        throw new InvalidOperationException("Both endpoint and iPEndPoint are null.");
    }
}

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
