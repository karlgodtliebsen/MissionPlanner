using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.MavLink.Services;

/// <summary>
/// Represents a session for a connection, managing its state and handling updates.
/// </summary>
/// <param name="domainFactory"></param>
/// <param name="logger"></param>
public sealed class MavLinkConnectionSessionFactory(IDomainFactory domainFactory, ILogger<MavLinkConnectionSessionFactory> logger) :
    IMavLinkConnectionSessionFactory
{
    /// <summary>
    /// Creates a serial connection to a vehicle using the specified port name and baud rate. Optionally, a configuration action can be provided to customize the transport endpoint settings. The connection process is cancellable via the provided cancellation token.
    /// </summary>
    /// <param name="transportOptions"></param>
    /// <param name="cancellationToken"></param>
    public Task<IMavLinkConnectionSession> CreateSerialConnection(IOptions<TransportEndpoint> transportOptions, CancellationToken cancellationToken = default)
    {
        var serviceCts = new CancellationTokenSource();

        // Create serial transport
        var transport = domainFactory.Create<ISerialMavLinkTransport, IOptions<TransportEndpoint>>(transportOptions);
        // Create MAVLink client
        var client = domainFactory.Create<IMavLinkClient, ISerialMavLinkTransport>(transport);

        var connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);

        var connectionTask = Task.Run(() => connection.StartAsync(linkedCts.Token), linkedCts.Token);

        var session = domainFactory.Create<IMavLinkConnectionSession, IMavLinkTransport, IMavLinkClient, IMavLinkConnection, CancellationTokenSource, Task>(transport, client, connection, serviceCts, connectionTask);
        return Task.FromResult(session);
    }


    /// <inheritdoc/>
    public Task<IMavLinkConnectionSession> CreateTcpConnection(IOptions<TransportEndpoint> transportOptions, CancellationToken cancellationToken = default)
    {
        var serviceCts = new CancellationTokenSource();

        // Create TCP transport
        var transport = domainFactory.Create<ITcpMavLinkTransport, IOptions<TransportEndpoint>>(transportOptions);
        // Create MAVLink client
        var client = domainFactory.Create<IMavLinkClient, ITcpMavLinkTransport>(transport);

        var connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);

        var connectionTask = Task.Run(() => connection.StartAsync(linkedCts.Token), linkedCts.Token);
        var session = domainFactory.Create<IMavLinkConnectionSession, IMavLinkTransport, IMavLinkClient, IMavLinkConnection, CancellationTokenSource, Task>(transport, client, connection, serviceCts, connectionTask);
        return Task.FromResult(session);
    }


    /// <inheritdoc/>
    public Task<IMavLinkConnectionSession> CreateUdpConnection(IOptions<TransportEndpoint> transportOptions, CancellationToken cancellationToken = default)
    {
        var serviceCts = new CancellationTokenSource();
        // Create UDP transport
        var transport = domainFactory.Create<IUdpMavLinkTransport, IOptions<TransportEndpoint>>(transportOptions);
        // Create MAVLink client
        var client = domainFactory.Create<IMavLinkClient, IUdpMavLinkTransport>(transport);
        var connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);
        var connectionTask = Task.Run(() => connection.StartAsync(linkedCts.Token), linkedCts.Token);
        var session = domainFactory.Create<IMavLinkConnectionSession, IMavLinkTransport, IMavLinkClient, IMavLinkConnection, CancellationTokenSource, Task>(transport, client, connection, serviceCts, connectionTask);
        return Task.FromResult(session);
    }
}
