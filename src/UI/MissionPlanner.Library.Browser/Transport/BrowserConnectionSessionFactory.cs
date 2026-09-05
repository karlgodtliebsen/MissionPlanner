using Microsoft.Extensions.Options;
using MissionPlanner.Library.Browser.Interop;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.Library.Browser.Transport;

public sealed class BrowserConnectionSessionFactory(IDomainFactory factory) : IMavLinkConnectionSessionFactory
{
    public Task<IMavLinkConnectionSession> CreateSerialConnection(IOptions<TransportEndpoint> options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Serial connections require the desktop app. Select UDP for the local browser bridge.");

    public Task<IMavLinkConnectionSession> CreateTcpConnection(IOptions<TransportEndpoint> options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Direct TCP connections require the desktop app. Select UDP for the local browser bridge.");

    public async Task<IMavLinkConnectionSession> CreateUdpConnection(IOptions<TransportEndpoint> options, CancellationToken cancellationToken = default)
    {
        var uri = new Uri(BrowserInterop.GetBridgeUrl() + "?port=" + options.Value.LocalPort);
        var transport = new BrowserUdpTransport(uri);
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IMavLinkClient? client = null;
        IMavLinkConnection? connection = null;
        try
        {
            // Await the handshake here, so connection errors reach the UI before
            // it begins waiting for a vehicle heartbeat.
            await transport.ConnectAsync(lifetime.Token);
            client = factory.Create<IMavLinkClient, IUdpMavLinkTransport>(transport);
            connection = factory.Create<IMavLinkConnection, IMavLinkClient>(client);
            await connection.StartAsync(lifetime.Token);
            return factory.Create<IMavLinkConnectionSession, IMavLinkTransport, IMavLinkClient,
                IMavLinkConnection, CancellationTokenSource, Task>(transport, client, connection, lifetime, Task.CompletedTask);
        }
        catch
        {
            await lifetime.CancelAsync();
            if (connection is not null) await connection.DisposeAsync();
            if (client is not null) await client.DisposeAsync();
            await transport.DisposeAsync();
            lifetime.Dispose();
            throw;
        }
    }
}
