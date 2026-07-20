using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Service for managing vehicle connections via MAVLink transport.
/// Orchestrates transport creation, connection establishment, and vehicle registration.
/// </summary>
public class VehicleConnectionService(
    IVehicleConnectionSession connectionSession,
    IDomainEventHub domainEventHub,
    IDateTimeProvider dateTimeProvider,
    IDomainFactory domainFactory,
    ILogger<VehicleConnectionService> logger)
    : IVehicleConnectionService
{
    // Single active connection (only one vehicle connection supported at a time)
    private ActiveConnection? activeConnection;
    private readonly SemaphoreSlim connectionLock = new(1, 1);

    /// <inheritdoc/>
    public bool IsConnected => activeConnection != null;

    /// <inheritdoc/>
    public IReadOnlyCollection<VehicleId> ConnectedVehicles => activeConnection != null ? [activeConnection.VehicleId] : [];


    /// <inheritdoc/>
    public async Task<VehicleConnectionResult> ConnectSerialAsync(string portName, int baudRate = 115200, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return new VehicleConnectionResult(false, null, null, "Port name cannot be empty");
        }

        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Disconnect existing connection if any
            if (activeConnection != null)
            {
                logger.LogInformation("Disconnecting existing connection before establishing new one");
                await DisconnectInternalAsync(cancellationToken);
            }

            logger.LogInformation("Connecting to vehicle using serial port {PortName} at {BaudRate} baud", portName, baudRate);
            var linkedCts = await connectionSession.CreateSerialConnection(portName, baudRate, cancellationToken: cancellationToken);

            var client = connectionSession.Client;
            var transport = connectionSession.Transport;
            // Wait for heartbeat to identify vehicle
            var vehicleId = await WaitForVehicleHeartbeatAsync(client, linkedCts.Token);

            if (vehicleId == null)
            {
                await connectionSession.DisconnectAsync(vehicleId, linkedCts.Token);
                await PublishConnectionFailed("SERIAL", $"{portName} {baudRate}", "No heartbeat received from vehicle");
                return new VehicleConnectionResult(false, null, null, "Timeout waiting for vehicle heartbeat");
            }

            // Request telemetry streams from vehicle
            await RequestTelemetryStreamsAsync(client, vehicleId.Value, linkedCts.Token);

            // Store active connection
            activeConnection = new ActiveConnection(vehicleId.Value, transport, client, "Serial", portName);

            // Publish success event
            await domainEventHub.PublishDomainEventAsync(new VehicleConnected(vehicleId.Value, "Serial", portName, dateTimeProvider.UtcNow), linkedCts.Token);

            logger.LogInformation("Successfully connected to vehicle {VehicleId} via serial port {PortName}", vehicleId, portName);
            return new VehicleConnectionResult(true, vehicleId.Value, connectionSession);
        }
        catch (Exception ex) //"A connection is already established."
        {
            logger.LogError(ex, "Failed to connect to vehicle via serial port {PortName}", portName);
            await PublishConnectionFailed("Serial", portName, ex.Message);
            return new VehicleConnectionResult(false, null, null, ex.Message);
        }
        finally
        {
            connectionLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<VehicleConnectionResult> ConnectTcpAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return new VehicleConnectionResult(false, null, null, "Host cannot be empty");
        }

        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Disconnect existing connection if any
            if (activeConnection != null)
            {
                logger.LogInformation("Disconnecting existing connection before establishing new one");
                await DisconnectInternalAsync(cancellationToken);
            }

            logger.LogInformation("Connecting to vehicle via TCP {Host}:{Port}", host, port);

            var endpoint = $"{host}:{port}";

            var linkedCts = await connectionSession.CreateTcpConnection(port, host, null, cancellationToken);
            var connection = connectionSession.Connection;
            var messagePump = connectionSession.MessagePump;
            var parameterService = connectionSession.ParameterService;
            var client = connectionSession.Client;
            var transport = connectionSession.Transport;
            // Wait for heartbeat to identify vehicle
            var vehicleId = await WaitForVehicleHeartbeatAsync(client, linkedCts.Token);

            if (vehicleId == null)
            {
                await connectionSession.DisconnectAsync(vehicleId, linkedCts.Token);
                await PublishConnectionFailed("TCP", endpoint, "No heartbeat received from vehicle");
                return new VehicleConnectionResult(false, null, null, "Timeout waiting for vehicle heartbeat");
            }


            // Request telemetry streams from vehicle
            await RequestTelemetryStreamsAsync(client, vehicleId.Value, linkedCts.Token);

            // Store active connection
            activeConnection = new ActiveConnection(vehicleId.Value, transport, client, "TCP", endpoint);

            // Publish success event
            await domainEventHub.PublishDomainEventAsync(new VehicleConnected(vehicleId.Value, "TCP", endpoint, dateTimeProvider.UtcNow), linkedCts.Token);

            logger.LogInformation("Successfully connected to vehicle {VehicleId} via TCP {Endpoint}", vehicleId, endpoint);
            return new VehicleConnectionResult(true, vehicleId.Value, connectionSession);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to vehicle via TCP {Host}:{Port}", host, port);
            await PublishConnectionFailed("TCP", $"{host}:{port}", ex.Message);
            return new VehicleConnectionResult(false, null, null, ex.Message);
        }
        finally
        {
            connectionLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<VehicleConnectionResult> ConnectUdpAsync(int localPort, string? remoteHost = null, int? remotePort = null, CancellationToken cancellationToken = default)
    {
        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Disconnect existing connection if any
            if (activeConnection != null)
            {
                logger.LogInformation("Disconnecting existing connection before establishing new one");
                await DisconnectInternalAsync(cancellationToken);
            }

            logger.LogInformation("Connecting to vehicle via UDP local port {LocalPort}", localPort);

            var endpoint = $"UDP:{localPort}";


            var linkedCts = await connectionSession.CreateUdpConnection(localPort, remoteHost ?? "127.0.0.1", remotePort ?? 14550, null, cancellationToken);
            var connection = connectionSession.Connection;
            var messagePump = connectionSession.MessagePump;
            var parameterService = connectionSession.ParameterService;
            var client = connectionSession.Client;
            var transport = connectionSession.Transport;
            // Wait for heartbeat to identify vehicle
            var vehicleId = await WaitForVehicleHeartbeatAsync(client, linkedCts.Token);

            if (vehicleId == null)
            {
                await connectionSession.DisconnectAsync(vehicleId, linkedCts.Token);
                await PublishConnectionFailed("UDP", endpoint, "No heartbeat received from vehicle");
                return new VehicleConnectionResult(false, null, null, "Timeout waiting for vehicle heartbeat");
            }

            // Request telemetry streams from vehicle
            await RequestTelemetryStreamsAsync(client, vehicleId.Value, linkedCts.Token);

            // Store active connection
            activeConnection = new ActiveConnection(vehicleId.Value, transport, client, "UDP", endpoint);
            await domainEventHub.PublishDomainEventAsync(new VehicleConnected(vehicleId.Value, "UDP", endpoint, dateTimeProvider.UtcNow), linkedCts.Token);

            logger.LogInformation("Successfully connected to vehicle {VehicleId} via UDP {Endpoint}", vehicleId, endpoint);
            return new VehicleConnectionResult(true, vehicleId.Value, connectionSession);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to vehicle via UDP local port {LocalPort}", localPort);
            await PublishConnectionFailed("UDP", $"UDP:{localPort}", ex.Message);
            return new VehicleConnectionResult(false, null, null, ex.Message);
        }
        finally
        {
            connectionLock.Release();
        }
    }

    private async Task<VehicleId?> WaitForVehicleHeartbeatAsync(IMavLinkClient client, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(10);
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        VehicleId? vehicleId = null;
        var tcs = new TaskCompletionSource<VehicleId?>();

        // Subscribe to vehicle registered event (fires when heartbeat handler identifies a vehicle)
        using var subscription = domainEventHub.SubscribeDomainEventAsync<VehicleRegistered>(async (evt, ct) =>
        {
            vehicleId = evt.VehicleId;
            tcs.TrySetResult(vehicleId);
        });

        try
        {
            // The connection session has already started MavLinkConnection, which starts the client.
            // Do not call client.StartAsync() here; doing so can race with connection.StartAsync() and create
            // multiple serial receive loops against the same COM port.
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout, timeoutCts.Token));

            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }

            logger.LogWarning("Timeout waiting for vehicle heartbeat");
            return null;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Cancelled while waiting for vehicle heartbeat");
            return null;
        }
        finally
        {
            subscription.Dispose();
            timeoutCts.Dispose();
        }
    }

    /// <summary>
    /// Requests essential telemetry streams from the vehicle after connection.
    /// </summary>
    private async Task RequestTelemetryStreamsAsync(IMavLinkClient client, VehicleId vehicleId, CancellationToken cancellationToken)
    {
        try
        {
            var commandService = domainFactory.Create<IMavLinkCommandService, IMavLinkClient>(client);

            await commandService.RequestDataStreamAsync(vehicleId, MavDataStream.Extra1, 10, true, cancellationToken);

            await commandService.RequestDataStreamAsync(vehicleId, MavDataStream.Extra2, 5, true, cancellationToken);

            await commandService.RequestDataStreamAsync(vehicleId, MavDataStream.Position, 5, true, cancellationToken);

            await commandService.RequestDataStreamAsync(vehicleId, MavDataStream.ExtendedStatus, 2, true, cancellationToken);

            await commandService.RequestDataStreamAsync(vehicleId, MavDataStream.RawSensors, 5, true, cancellationToken);

            await commandService.RequestDataStreamAsync(vehicleId, MavDataStream.RcChannels, 5, true, cancellationToken);

            // Home position is not streamed; ask for it once so distance-to-home readouts work.
            await commandService.RequestHomePositionAsync(vehicleId, cancellationToken);

            logger.LogInformation("Telemetry streams requested for vehicle {VehicleId}", vehicleId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to request telemetry streams for {VehicleId}", vehicleId);
        }
    }

    private async Task PublishConnectionFailed(string connectionType, string endpoint, string error)
    {
        await domainEventHub.PublishDomainEventAsync(new ConnectionFailed(connectionType, endpoint, error, dateTimeProvider.UtcNow));
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (activeConnection == null)
        {
            return;
        }

        //  VehicleId? vehicleId = activeConnection.VehicleId;
        // DomainException.ThrowIfNull(vehicleId);
        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            await DisconnectInternalAsync(cancellationToken);
        }
        finally
        {
            connectionLock.Release();
        }
    }

    /// <summary>
    /// Internal disconnect method - must be called with connectionLock held or from single-threaded context
    /// </summary>
    private async Task DisconnectInternalAsync(CancellationToken cancellationToken = default)
    {
        if (activeConnection == null)
        {
            return;
        }

        var vehicleId = activeConnection.VehicleId;
        try
        {
            logger.LogInformation("Disconnecting vehicle {VehicleId}", vehicleId);

            // Clear active connection
            activeConnection = null;
            await connectionSession.DisconnectAsync(vehicleId, cancellationToken);
            logger.LogInformation("Successfully disconnected vehicle {VehicleId}", vehicleId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while executing internal disconnecting vehicle {VehicleId}", vehicleId);
        }

        // Still clear the connection even if there were errors
        activeConnection = null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Disconnect the active connection (if any)
        if (activeConnection != null)
        {
            await DisconnectAsync();
        }

        activeConnection = null;
        // Dispose the semaphore and cancellation token source
        connectionLock.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Represents an active vehicle connection.
    /// </summary>
    private record ActiveConnection(VehicleId VehicleId, IMavLinkTransport Transport, IMavLinkClient Client, string ConnectionType, string Endpoint);
}
