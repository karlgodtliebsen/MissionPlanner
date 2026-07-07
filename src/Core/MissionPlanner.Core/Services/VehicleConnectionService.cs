using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;

namespace MissionPlanner.Core.Services;

/// <summary>
/// Service for managing vehicle connections via MAVLink transport.
/// Orchestrates transport creation, connection establishment, and vehicle registration.
/// </summary>
public class VehicleConnectionService(
    IDomainEventHub domainEventHub,
    IDateTimeProvider dateTimeProvider,
    IDomainFactory domainFactory,
    IServiceFactory serviceFactory,
    ILogger<VehicleConnectionService> logger)
    : IVehicleConnectionService, IAsyncDisposable
{
    // Single active connection (only one vehicle connection supported at a time)
    private ActiveConnection? activeConnection;
    private readonly SemaphoreSlim connectionLock = new(1, 1);

    private IMavLinkConnection? connection;
    private IVehicleMessagePump? messagePump;

    // Background tasks that must live as long as this service instance
    private Task? messagePumpTask;
    private Task? connectionTask;
    private readonly CancellationTokenSource serviceCts = new();

    /// <inheritdoc/>
    public bool IsConnected => activeConnection != null;

    /// <inheritdoc/>
    public IReadOnlyCollection<VehicleId> ConnectedVehicles =>
        activeConnection != null ? [activeConnection.VehicleId] : [];


    /// <inheritdoc/>
    public async Task<VehicleConnectionResult> ConnectSerialAsync(string portName, int baudRate = 57600, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return new VehicleConnectionResult(false, null, "Port name cannot be empty");
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

            var registry = serviceFactory.Create<IVehicleRegistry>();

            // Publish Reset event
            registry.Reset();

            // Create serial transport options
            var transportOptions = serviceFactory.Create<IOptions<TransportEndpoint>>();
            transportOptions.Value.Protocol = "serial";

            // Create serial transport
            var transport = domainFactory.Create<ISerialMavLinkTransport, string, int>(portName, baudRate);
            // Create MAVLink client
            var client = domainFactory.Create<IMavLinkClient, ISerialMavLinkTransport, IOptions<TransportEndpoint>, IDateTimeProvider>(transport, transportOptions, dateTimeProvider);

            messagePump = serviceFactory.Create<IVehicleMessagePump>();
            connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);

            // Store background tasks so they live as long as this service instance
            // Link to service-level cancellation token to allow proper cleanup
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);

            messagePumpTask = Task.Run(() => messagePump.StartAsync(linkedCts.Token), linkedCts.Token);
            connectionTask = Task.Run(() => connection.StartAsync(linkedCts.Token), linkedCts.Token);

            // Wait for heartbeat to identify vehicle
            var vehicleId = await WaitForVehicleHeartbeatAsync(client, linkedCts.Token);

            if (vehicleId == null)
            {
                await transport.DisconnectAsync(linkedCts.Token);
                await PublishConnectionFailed("Serial", portName, "No heartbeat received from vehicle");
                registry.Reset();
                return new VehicleConnectionResult(false, null, "Timeout waiting for vehicle heartbeat");
            }

            // Request telemetry streams from vehicle
            await RequestTelemetryStreamsAsync(client, vehicleId.Value, linkedCts.Token);

            // Store active connection
            activeConnection = new ActiveConnection(vehicleId.Value, transport, client, "Serial", portName);

            // Publish success event
            await domainEventHub.PublishAsync(new VehicleConnected(vehicleId.Value, "Serial", portName, dateTimeProvider.UtcNow), linkedCts.Token);

            logger.LogInformation("Successfully connected to vehicle {VehicleId} via serial port {PortName}", vehicleId, portName);
            return new VehicleConnectionResult(true, vehicleId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to vehicle via serial port {PortName}", portName);
            await PublishConnectionFailed("Serial", portName, ex.Message);
            return new VehicleConnectionResult(false, null, ex.Message);
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
            return new VehicleConnectionResult(false, null, "Host cannot be empty");
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

            // Create transport options
            var transportOptions = Options.Create(new TransportEndpoint("tcp", port, host, 0, receiveBufferSize: 512));

            // Create TCP transport
            var transport = domainFactory.Create<ITcpMavLinkTransport, IOptions<TransportEndpoint>>(transportOptions);
            // Create MAVLink client
            var client = domainFactory.Create<IMavLinkClient, ITcpMavLinkTransport, IOptions<TransportEndpoint>, IDateTimeProvider>(transport, transportOptions, dateTimeProvider);

            messagePump = serviceFactory.Create<IVehicleMessagePump>();
            connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);

            // Store background tasks so they live as long as this service instance
            // Link to service-level cancellation token to allow proper cleanup
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);

            messagePumpTask = Task.Run(() => messagePump.StartAsync(linkedCts.Token), linkedCts.Token);
            connectionTask = Task.Run(() => connection.StartAsync(linkedCts.Token), linkedCts.Token);

            // Connect
            await transport.ConnectAsync(linkedCts.Token);

            // Wait for heartbeat to identify vehicle
            var vehicleId = await WaitForVehicleHeartbeatAsync(client, linkedCts.Token);

            if (vehicleId == null)
            {
                await transport.DisconnectAsync(linkedCts.Token);
                await PublishConnectionFailed("TCP", endpoint, "No heartbeat received from vehicle");
                return new VehicleConnectionResult(false, null, "Timeout waiting for vehicle heartbeat");
            }

            // Request telemetry streams from vehicle
            await RequestTelemetryStreamsAsync(client, vehicleId.Value, linkedCts.Token);

            // Store active connection
            activeConnection = new ActiveConnection(vehicleId.Value, transport, client, "TCP", endpoint);

            // Publish success event
            await domainEventHub.PublishAsync(new VehicleConnected(vehicleId.Value, "TCP", endpoint, dateTimeProvider.UtcNow), linkedCts.Token);

            logger.LogInformation("Successfully connected to vehicle {VehicleId} via TCP {Endpoint}", vehicleId, endpoint);
            return new VehicleConnectionResult(true, vehicleId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to vehicle via TCP {Host}:{Port}", host, port);
            await PublishConnectionFailed("TCP", $"{host}:{port}", ex.Message);
            return new VehicleConnectionResult(false, null, ex.Message);
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

            // Create transport options
            var transportOptions = Options.Create(new TransportEndpoint("udp", remotePort ?? 14550, remoteHost ?? "127.0.0.1", localPort, receiveBufferSize: 512));
            // Create UDP transport
            var transport = domainFactory.Create<IUdpMavLinkTransport, IOptions<TransportEndpoint>>(transportOptions);
            // Create MAVLink client
            var client = domainFactory.Create<IMavLinkClient, IUdpMavLinkTransport, IOptions<TransportEndpoint>, IDateTimeProvider>(transport, transportOptions, dateTimeProvider);

            messagePump = serviceFactory.Create<IVehicleMessagePump>();
            connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);

            // Store background tasks so they live as long as this service instance
            // Link to service-level cancellation token to allow proper cleanup
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, serviceCts.Token);

            messagePumpTask = Task.Run(() => messagePump.StartAsync(linkedCts.Token), linkedCts.Token);
            connectionTask = Task.Run(() => connection.StartAsync(linkedCts.Token), linkedCts.Token);

            // Connect
            await transport.ConnectAsync(linkedCts.Token);

            // Wait for heartbeat to identify vehicle
            var vehicleId = await WaitForVehicleHeartbeatAsync(client, linkedCts.Token);

            if (vehicleId == null)
            {
                await transport.DisconnectAsync(linkedCts.Token);
                await PublishConnectionFailed("UDP", endpoint, "No heartbeat received from vehicle");
                return new VehicleConnectionResult(false, null, "Timeout waiting for vehicle heartbeat");
            }

            // Request telemetry streams from vehicle
            await RequestTelemetryStreamsAsync(client, vehicleId.Value, linkedCts.Token);

            // Store active connection
            activeConnection = new ActiveConnection(vehicleId.Value, transport, client, "UDP", endpoint);

            // Publish success event
            await domainEventHub.PublishAsync(new VehicleConnected(vehicleId.Value, "UDP", endpoint, dateTimeProvider.UtcNow), linkedCts.Token);

            logger.LogInformation("Successfully connected to vehicle {VehicleId} via UDP {Endpoint}", vehicleId, endpoint);
            return new VehicleConnectionResult(true, vehicleId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to vehicle via UDP local port {LocalPort}", localPort);
            await PublishConnectionFailed("UDP", $"UDP:{localPort}", ex.Message);
            return new VehicleConnectionResult(false, null, ex.Message);
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
        var subscription = domainEventHub.SubscribeDomainEvent<VehicleRegistered>(evt =>
        {
            vehicleId = evt.VehicleId;
            tcs.TrySetResult(vehicleId);
        });

        try
        {
            // Start the client (begins receiving data)
            await client.StartAsync(timeoutCts.Token);

            // Wait for vehicle registration or timeout
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
            // Create command service with the active MAVLink client
            var commandService = domainFactory.Create<IMavLinkCommandService, IMavLinkClient>(client);

            // Request ATTITUDE stream (roll, pitch, yaw) at 10 Hz
            await commandService.RequestDataStreamAsync(
                vehicleId,
                MavDataStream.Extra1,
                10,
                true,
                cancellationToken);

            // Request POSITION stream (GPS, altitude) at 5 Hz
            await commandService.RequestDataStreamAsync(
                vehicleId,
                MavDataStream.Position,
                5,
                true,
                cancellationToken);

            // Request EXTENDED_STATUS stream (battery, system status) at 2 Hz
            await commandService.RequestDataStreamAsync(
                vehicleId,
                MavDataStream.ExtendedStatus,
                2,
                true,
                cancellationToken);

            // Request RAW_SENSORS stream (GPS raw, IMU) at 5 Hz
            await commandService.RequestDataStreamAsync(
                vehicleId,
                MavDataStream.RawSensors,
                5,
                true,
                cancellationToken);

            logger.LogInformation("✅ Telemetry streams requested for vehicle {VehicleId}", vehicleId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to request telemetry streams for {VehicleId} - continuing anyway", vehicleId);
            // Don't fail the connection if telemetry requests fail
        }
    }

    private async Task PublishConnectionFailed(string connectionType, string endpoint, string error)
    {
        await domainEventHub.PublishAsync(new ConnectionFailed(connectionType, endpoint, error, dateTimeProvider.UtcNow));
    }

    private async Task DisconnectAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (activeConnection == null)
            {
                logger.LogWarning("No active connection to disconnect");
                return;
            }

            if (activeConnection.VehicleId != vehicleId)
            {
                logger.LogWarning("Attempted to disconnect vehicle {VehicleId} but current connection is {CurrentVehicleId}", vehicleId, activeConnection.VehicleId);
                return;
            }

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
        var conn = activeConnection;

        try
        {
            logger.LogInformation("Disconnecting vehicle {VehicleId}", vehicleId);

            // Stop background tasks gracefully
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
            await conn.Client.StopAsync();
            await conn.Transport.DisconnectAsync(cancellationToken);
            await conn.Transport.DisposeAsync();

            // Clear active connection
            activeConnection = null;

            // Publish disconnect event
            await domainEventHub.PublishAsync(
                new VehicleDisconnected(vehicleId, dateTimeProvider.UtcNow, "User requested disconnect"),
                cancellationToken);

            logger.LogInformation("Successfully disconnected vehicle {VehicleId}", vehicleId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while disconnecting vehicle {VehicleId}", vehicleId);
            // Still clear the connection even if there were errors
            activeConnection = null;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Cancel the service-level token to signal background tasks to stop
        if (!serviceCts.IsCancellationRequested)
        {
            await serviceCts.CancelAsync();
        }

        // Disconnect the active connection (if any)
        if (activeConnection != null)
        {
            await DisconnectAsync(activeConnection.VehicleId);
        }

        // Dispose the semaphore and cancellation token source
        connectionLock.Dispose();
        serviceCts.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Represents an active vehicle connection.
    /// </summary>
    private record ActiveConnection(VehicleId VehicleId, IMavLinkTransport Transport, IMavLinkClient Client, string ConnectionType, string Endpoint);
}
