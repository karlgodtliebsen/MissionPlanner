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
using MissionPlanner.MavLink.Services;
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
/// <param name="connectionSessionFactory"></param>
/// <param name="logger"></param>
/// <param name="messagePumpCoordinator">Shared inbound MAVLink dispatch coordinator.</param>
/// <param name="resetRegistryOnLifecycle">Whether this session owns the complete vehicle registry lifecycle.</param>
public sealed class VehicleConnectionSession(
    IVehicleParameterRegistry parameterRegistry,
    IDomainFactory domainFactory,
    IServiceFactory serviceFactory,
    IDomainEventHub domainEventHub,
    IDateTimeProvider dateTimeProvider,
    IMavLinkConnectionSessionFactory connectionSessionFactory,
    ILogger<VehicleConnectionSession> logger,
    IVehicleMessagePumpCoordinator messagePumpCoordinator,
    bool resetRegistryOnLifecycle = true)
    : IVehicleConnectionSession
{
    private IVehicleMessagePump? messagePump;
    private IVehicleMessagePumpLease? messagePumpLease;
    private IVehicleParameterService? parameterService;
    private IVehicleParameterStreamService? parameterStreamService;
    private CancellationTokenSource serviceCts = new();
    private IMavLinkConnectionSession? connectionSession = null;

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
    /// Gets the established MAVLink connection. Throws an exception if no connection is established.
    /// </summary>
    public IMavLinkConnection Connection => connectionSession!.Connection ?? throw new InvalidOperationException("No connection established");

    /// <summary>
    /// Gets the established MAVLink client. Throws an exception if no client is established.
    /// </summary>
    public IMavLinkClient Client => connectionSession!.Client ?? throw new InvalidOperationException("No client established");

    /// <summary>
    /// Gets the established MAVLink transport. Throws an exception if no transport is established.
    /// </summary>
    public IMavLinkTransport Transport => connectionSession!.Transport ?? throw new InvalidOperationException("No transport established");


    /// <inheritdoc />
    public IVehicleFileSystemService? CreateMavFtpConnection()
    {
        if (connectionSession is null)
        {
            return null;
        }

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

        var registry = serviceFactory.Create<IVehicleRegistry>();

        if (resetRegistryOnLifecycle)
        {
            await registry.Reset(cancellationToken);
        }

        var transportOptions = serviceFactory.Create<IOptions<TransportEndpoint>>();
        transportOptions.Value.Protocol = "serial";
        transportOptions.Value.SerialPort = portName;
        transportOptions.Value.BaudRate = baudRate;
        configure?.Invoke(transportOptions.Value);

        connectionSession = await connectionSessionFactory.CreateSerialConnection(transportOptions, cancellationToken);

        messagePumpLease = await messagePumpCoordinator.AcquireAsync(cancellationToken).ConfigureAwait(false);
        messagePump = messagePumpLease.Pump;

        parameterService = domainFactory.Create<IVehicleParameterService, IVehicleConnectionSession>(this!);
        parameterStreamService = CreateParameterStreamService();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);

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

        if (resetRegistryOnLifecycle)
        {
            await registry.Reset(cancellationToken);
        }

        connectionSession = await connectionSessionFactory.CreateTcpConnection(transportOptions, cancellationToken);

        messagePumpLease = await messagePumpCoordinator.AcquireAsync(cancellationToken).ConfigureAwait(false);
        messagePump = messagePumpLease.Pump;

        parameterService = domainFactory.Create<IVehicleParameterService, IVehicleConnectionSession>(this!);
        parameterStreamService = CreateParameterStreamService();

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);

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

        if (resetRegistryOnLifecycle)
        {
            await registry.Reset(cancellationToken);
        }

        connectionSession = await connectionSessionFactory.CreateUdpConnection(transportOptions, cancellationToken);
        messagePumpLease = await messagePumpCoordinator.AcquireAsync(cancellationToken).ConfigureAwait(false);
        messagePump = messagePumpLease.Pump;

        parameterService = domainFactory.Create<IVehicleParameterService, IVehicleConnectionSession>(this!);
        parameterStreamService = CreateParameterStreamService();

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);

        return linkedCts;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Stop and dispose services
        if (connectionSession is not null)
        {
            await connectionSession.DisposeAsync().ConfigureAwait(false);
            connectionSession = null;
        }

        if (messagePumpLease is not null)
        {
            try
            {
                await messagePumpLease.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Non Critical Failure releasing shared message pump");
            }

            messagePumpLease = null;
            messagePump = null;
        }

        if (connectionSession is not null)
        {
            try
            {
                await connectionSession.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Non Critical Failure Disposing connection ");
            }

            connectionSession = null;
        }

        parameterStreamService = null;
        parameterService = null;
        connectionSession?.DisposeAsync();
    }

    /// <summary>
    /// Internal disconnect method - must be called with connectionLock held or from single-threaded context
    /// </summary>
    public async Task DisconnectAsync(VehicleId? vehicleId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Publish disconnect event
            if (vehicleId is not null)
            {
                try
                {
                    logger.LogInformation("Disconnecting vehicle {VehicleId}", vehicleId);
                    await domainEventHub.PublishDomainEventAsync(new VehicleDisconnected(vehicleId.Value, dateTimeProvider.UtcNow, "User requested disconnect"), cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error Publishing VehicleDisconnected while disconnecting Session {VehicleId}", vehicleId);
                }
            }

            // Stop background tasks gracefully. Cancel first; otherwise the wait below just waits for the timeout.
            await serviceCts.CancelAsync().ConfigureAwait(false);

            if (connectionSession is not null)
            {
                await connectionSession.DisconnectAsync(cancellationToken);
            }

            await DisposeAsync();

            // Remove registry/parameter state only after inbound processing has stopped, so a final
            // datagram cannot recreate a vehicle that this exact connection no longer owns.
            var registry = serviceFactory.Create<IVehicleRegistry>();
            if (resetRegistryOnLifecycle)
            {
                await registry.Reset(CancellationToken.None).ConfigureAwait(false);
            }
            else if (vehicleId is { } exactVehicleId)
            {
                await registry.RemoveAsync(exactVehicleId, CancellationToken.None).ConfigureAwait(false);
                parameterRegistry.ClearParameters(exactVehicleId);
            }
        }
        catch (Exception ex)
        {
            if (vehicleId is not null)
            {
                logger.LogError(ex, "Error while disconnecting Session {VehicleId}", vehicleId);
            }
        }

        // Always null the fields  even if there were errors
        connectionSession = null;
        messagePump = null;
        messagePumpLease = null;
        parameterStreamService = null;
        parameterService = null;
        logger.LogInformation("Successfully disconnected vehicle {VehicleId}", vehicleId);
    }

    private IVehicleParameterStreamService CreateParameterStreamService()
    {
        var mavFtpClient = connectionSession!.CreateMavFtpClient();
        var fileSystemService = domainFactory.Create<IVehicleFileSystemService, IMavFtpClient>(mavFtpClient!);
        return domainFactory.Create<IVehicleParameterStreamService, IVehicleParameterService, IVehicleFileSystemService>(ParameterService, fileSystemService);
    }
}
