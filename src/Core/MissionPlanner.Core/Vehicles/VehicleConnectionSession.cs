using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.MavFtp.Abstractions;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Represents a session for a vehicle connection, managing its state and handling updates.
/// </summary>
/// <param name="parameterRegistry"></param>
/// <param name="domainFactory"></param>
/// <param name="serviceFactory"></param>
/// <param name="domainEventHub"></param>
/// <param name="dateTimeProvider"></param>
/// <param name="logger"></param>
public sealed class VehicleConnectionSession(
    IVehicleParameterRegistry parameterRegistry,
    IDomainFactory domainFactory,
    IServiceFactory serviceFactory,
    IDomainEventHub domainEventHub,
    IDateTimeProvider dateTimeProvider,
    ILogger<VehicleConnectionSession> logger)
    : IVehicleConnectionSession
{
    private IMavLinkConnection? connection;
    private IVehicleMessagePump? messagePump;
    private IVehicleParameterService? parameterService;
    private IVehicleParameterStreamService? parameterStreamService;

    private CancellationTokenSource serviceCts = new();

    // Background tasks that must live as long as this service instance
    private Task? messagePumpTask;
    private Task? connectionTask;
    private IMavLinkTransport? transport;
    private IMavLinkClient? client;

    /// <summary>
    /// Gets the established MAVLink connection. Throws an exception if no connection is established.
    /// </summary>
    public IMavLinkConnection Connection => connection ?? throw new InvalidOperationException("No connection established");

    /// <summary>
    /// Gets the established message pump. Throws an exception if no message pump is established.
    /// </summary>
    public IVehicleMessagePump MessagePump => messagePump ?? throw new InvalidOperationException("No message pump established");

    /// <summary>
    /// Gets the established parameter service. Throws an exception if no parameter service is established.
    /// </summary>
    public IVehicleParameterService ParameterService => parameterService ?? throw new InvalidOperationException("No parameter service established");

    /// <inheritdoc />
    public IVehicleParameterRegistry ParameterRegistry => parameterRegistry ?? throw new InvalidOperationException("No parameter registry established");

    /// <inheritdoc />
    public IVehicleParameterStreamService ParameterStreamService => parameterStreamService ?? throw new InvalidOperationException("No parameter StreamService established");


    /// <summary>
    /// Gets the established MAVLink client. Throws an exception if no client is established.
    /// </summary>
    public IMavLinkClient Client => client ?? throw new InvalidOperationException("No client established");

    /// <summary>
    /// Gets the established MAVLink transport. Throws an exception if no transport is established.
    /// </summary>
    public IMavLinkTransport Transport => transport ?? throw new InvalidOperationException("No transport established");


    /// <inheritdoc />
    public IVehicleFileSystemService CreateMavFtpConnection()
    {
        var mavClient = domainFactory.Create<IMavFtpClient, IMavLinkConnection>(Connection);
        var service = domainFactory.Create<IVehicleFileSystemService, IMavFtpClient>(mavClient);
        return service;
    }

    /// <summary>
    /// Creates a serial connection to a vehicle using the specified port name and baud rate. Optionally, a configuration action can be provided to customize the transport endpoint settings. The connection process is cancellable via the provided cancellation token.
    /// </summary>
    /// <param name="portName"></param>
    /// <param name="baudRate"></param>
    /// <param name="configure"></param>
    /// <param name="cancellationToken"></param>
    public async Task<CancellationTokenSource> CreateSerialConnection(string portName, int baudRate = 57600, Action<TransportEndpoint>? configure = null, CancellationToken cancellationToken = default)
    {
        serviceCts = new CancellationTokenSource();
        client?.DisposeAsync();

        var registry = serviceFactory.Create<IVehicleRegistry>();

        // Publish Reset event
        await registry.Reset(cancellationToken);

        // Create serial transport
        transport = domainFactory.Create<ISerialMavLinkTransport, string, int>(portName, baudRate);
        // Create MAVLink client
        client = domainFactory.Create<IMavLinkClient, ISerialMavLinkTransport>((ISerialMavLinkTransport)transport);

        messagePump = serviceFactory.Create<IVehicleMessagePump>();
        connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);
        parameterService = domainFactory.Create<IVehicleParameterService, IMavLinkClient>(client);

        parameterStreamService = domainFactory.Create<IVehicleParameterStreamService, IVehicleParameterService>(parameterService);
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);

        messagePumpTask = Task.Run(() => messagePump.StartAsync(linkedCts.Token), linkedCts.Token);
        connectionTask = Task.Run(() => connection.StartAsync(linkedCts.Token), linkedCts.Token);
        return linkedCts;
    }


    /// <inheritdoc/>
    public async Task<CancellationTokenSource> CreateTcpConnection(int port, string host, Action<TransportEndpoint>? configure = null, CancellationToken cancellationToken = default)
    {
        serviceCts = new CancellationTokenSource();
        var transportOptions = serviceFactory.Create<IOptions<TransportEndpoint>>();
        transportOptions.Value.Protocol = "tcp";
        transportOptions.Value.RemoteHost = host;
        transportOptions.Value.RemotePort = port;
        configure?.Invoke(transportOptions.Value);
        var registry = serviceFactory.Create<IVehicleRegistry>();

        // Publish Reset event
        await registry.Reset(cancellationToken);

        // Create TCP transport
        transport = domainFactory.Create<ITcpMavLinkTransport, IOptions<TransportEndpoint>>(transportOptions);
        // Create MAVLink client
        client = domainFactory.Create<IMavLinkClient, ITcpMavLinkTransport>((ITcpMavLinkTransport)transport);

        messagePump = serviceFactory.Create<IVehicleMessagePump>();
        connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);

        //connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);
        parameterService = domainFactory.Create<IVehicleParameterService, IMavLinkClient>(client);

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);

        messagePumpTask = Task.Run(() => messagePump.StartAsync(linkedCts.Token), linkedCts.Token);
        connectionTask = Task.Run(() => connection.StartAsync(linkedCts.Token), linkedCts.Token);
        return linkedCts;
    }


    /// <inheritdoc/>
    public async Task<CancellationTokenSource> CreateUdpConnection(int localPort, string? remoteHost = null, int? remotePort = null, Action<TransportEndpoint>? configure = null, CancellationToken cancellationToken = default)
    {
        serviceCts = new CancellationTokenSource();
        var transportOptions = serviceFactory.Create<IOptions<TransportEndpoint>>();
        transportOptions.Value.Protocol = "udp";
        transportOptions.Value.LocalPort = localPort;
        transportOptions.Value.RemoteHost = remoteHost ?? "127.0.0.1";
        transportOptions.Value.RemotePort = remotePort ?? 14550;
        configure?.Invoke(transportOptions.Value);
        var registry = serviceFactory.Create<IVehicleRegistry>();

        // Publish Reset event
        await registry.Reset(cancellationToken);

        // Create UDP transport
        transport = domainFactory.Create<IUdpMavLinkTransport, IOptions<TransportEndpoint>>(transportOptions);
        // Create MAVLink client
        client = domainFactory.Create<IMavLinkClient, IUdpMavLinkTransport>((IUdpMavLinkTransport)transport);

        messagePump = serviceFactory.Create<IVehicleMessagePump>();
        connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);

        //connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);
        parameterService = domainFactory.Create<IVehicleParameterService, IMavLinkClient>(client);

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);

        messagePumpTask = Task.Run(() => messagePump.StartAsync(linkedCts.Token), linkedCts.Token);
        connectionTask = Task.Run(() => connection.StartAsync(linkedCts.Token), linkedCts.Token);
        return linkedCts;
    }


    /// <summary>
    /// Internal disconnect method - must be called with connectionLock held or from single-threaded context
    /// </summary>
    public async Task DisconnectAsync(VehicleId? vehicleId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Disconnecting vehicle {VehicleId}", vehicleId);
            var registry = serviceFactory.Create<IVehicleRegistry>();

            // Publish Reset event
            await registry.Reset(cancellationToken);


            // Stop background tasks gracefully. Cancel first; otherwise the wait below just waits for the timeout.
            await serviceCts.CancelAsync().ConfigureAwait(false);

            var tasksToWait = new List<Task>();
            if (messagePumpTask is not null && !messagePumpTask.IsCompleted)
            {
                tasksToWait.Add(messagePumpTask);
            }

            if (connectionTask is not null && !connectionTask.IsCompleted)
            {
                tasksToWait.Add(connectionTask);
            }

            if (tasksToWait.Any())
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await Task.WhenAll(tasksToWait).WaitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("Background tasks did not complete within timeout period during disconnect");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error waiting for background tasks to complete");
                }
            }

            // Clean up task references
            messagePumpTask = null;
            connectionTask = null;

            // Stop and dispose services
            if (messagePump is not null)
            {
                await messagePump.DisposeAsync();
                messagePump = null;
            }

            if (connection is not null)
            {
                await connection.DisposeAsync();
                connection = null;
            }

            // Stop client and disconnect transport
            if (client is not null)
            {
                await client.StopAsync();
            }

            if (transport is not null)
            {
                await transport.DisconnectAsync(cancellationToken);
                await transport.DisposeAsync();
            }

            client = null;
            transport = null;

            // Publish disconnect event
            if (vehicleId is not null)
            {
                await domainEventHub.PublishDomainEventAsync(new VehicleDisconnected(vehicleId.Value, dateTimeProvider.UtcNow, "User requested disconnect"), cancellationToken);
                logger.LogInformation("Successfully disconnected vehicle {VehicleId}", vehicleId);
            }
        }
        catch (Exception ex)
        {
            if (vehicleId is not null)
            {
                logger.LogError(ex, "Error while disconnecting vehicle {VehicleId}", vehicleId);
            }
            // Still clear the connection even if there were errors
        }
    }
}
