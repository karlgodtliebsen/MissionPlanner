using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Transport;

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
    private readonly ConcurrentDictionary<VehicleId, ActiveConnection> activeConnections = new();

    /// <inheritdoc/>
    public bool IsConnected => activeConnections.Any();

    /// <inheritdoc/>
    public IReadOnlyCollection<VehicleId> ConnectedVehicles => activeConnections.Keys.ToList();


    /// <inheritdoc/>
    public async Task<VehicleConnectionResult> ConnectSerialAsync(string portName, int baudRate = 57600, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return new VehicleConnectionResult(false, null, "Port name cannot be empty");
        }

        try
        {
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

            var messagePump = serviceFactory.Create<IVehicleMessagePump>();
            var connection = domainFactory.Create<IMavLinkConnection, IMavLinkClient>(client);

            await Task.Run(() => messagePump.StartAsync(cancellationToken), cancellationToken);
            await Task.Run(() => connection.StartAsync(cancellationToken), cancellationToken);


            // Wait for heartbeat to identify vehicle
            var vehicleId = await WaitForVehicleHeartbeatAsync(client, cancellationToken);

            if (vehicleId == null)
            {
                await transport.DisconnectAsync(cancellationToken);
                await PublishConnectionFailed("Serial", portName, "No heartbeat received from vehicle");
                registry.Reset();
                return new VehicleConnectionResult(false, null, "Timeout waiting for vehicle heartbeat");
            }

            // Store active connection
            var activeConnection = new ActiveConnection(vehicleId.Value, transport, client, "Serial", portName);
            activeConnections[vehicleId.Value] = activeConnection;

            // Publish success event
            await domainEventHub.PublishAsync(new VehicleConnected(vehicleId.Value, "Serial", portName, dateTimeProvider.UtcNow), cancellationToken);

            logger.LogInformation("Successfully connected to vehicle {VehicleId} via serial port {PortName}", vehicleId, portName);
            return new VehicleConnectionResult(true, vehicleId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to vehicle via serial port {PortName}", portName);
            await PublishConnectionFailed("Serial", portName, ex.Message);
            return new VehicleConnectionResult(false, null, ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<VehicleConnectionResult> ConnectTcpAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return new VehicleConnectionResult(false, null, "Host cannot be empty");
        }

        try
        {
            logger.LogInformation("Connecting to vehicle via TCP {Host}:{Port}", host, port);

            var endpoint = $"{host}:{port}";

            // Create transport options
            var transportOptions = Options.Create(new TransportEndpoint("tcp", port, host, 0, receiveBufferSize: 512));

            // Create TCP transport
            var transport = domainFactory.Create<ITcpMavLinkTransport, IOptions<TransportEndpoint>>(transportOptions);
            // Create MAVLink client
            var client = domainFactory.Create<IMavLinkClient, ITcpMavLinkTransport, IOptions<TransportEndpoint>, IDateTimeProvider>(transport, transportOptions, dateTimeProvider);


            // Connect
            await transport.ConnectAsync(cancellationToken);

            // Wait for heartbeat to identify vehicle
            var vehicleId = await WaitForVehicleHeartbeatAsync(client, cancellationToken);

            if (vehicleId == null)
            {
                await transport.DisconnectAsync(cancellationToken);
                await PublishConnectionFailed("TCP", endpoint, "No heartbeat received from vehicle");
                return new VehicleConnectionResult(false, null, "Timeout waiting for vehicle heartbeat");
            }

            // Store active connection
            var connection = new ActiveConnection(vehicleId.Value, transport, client, "TCP", endpoint);
            activeConnections[vehicleId.Value] = connection;

            // Publish success event
            await domainEventHub.PublishAsync(new VehicleConnected(vehicleId.Value, "TCP", endpoint, dateTimeProvider.UtcNow), cancellationToken);

            logger.LogInformation("Successfully connected to vehicle {VehicleId} via TCP {Endpoint}", vehicleId, endpoint);
            return new VehicleConnectionResult(true, vehicleId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to vehicle via TCP {Host}:{Port}", host, port);
            await PublishConnectionFailed("TCP", $"{host}:{port}", ex.Message);
            return new VehicleConnectionResult(false, null, ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<VehicleConnectionResult> ConnectUdpAsync(int localPort, string? remoteHost = null, int? remotePort = null, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Connecting to vehicle via UDP local port {LocalPort}", localPort);

            var endpoint = $"UDP:{localPort}";

            // Create transport options
            var transportOptions = Options.Create(new TransportEndpoint("udp", remotePort ?? 14550, remoteHost ?? "127.0.0.1", localPort, receiveBufferSize: 512));
            // Create UDP transport
            var transport = domainFactory.Create<IUdpMavLinkTransport, IOptions<TransportEndpoint>>(transportOptions);
            // Create MAVLink client
            var client = domainFactory.Create<IMavLinkClient, IUdpMavLinkTransport, IOptions<TransportEndpoint>, IDateTimeProvider>(transport, transportOptions, dateTimeProvider);

            // Connect
            await transport.ConnectAsync(cancellationToken);

            // Wait for heartbeat to identify vehicle
            var vehicleId = await WaitForVehicleHeartbeatAsync(client, cancellationToken);

            if (vehicleId == null)
            {
                await transport.DisconnectAsync(cancellationToken);
                await PublishConnectionFailed("UDP", endpoint, "No heartbeat received from vehicle");
                return new VehicleConnectionResult(false, null, "Timeout waiting for vehicle heartbeat");
            }

            // Store active connection
            var connection = new ActiveConnection(vehicleId.Value, transport, client, "UDP", endpoint);
            activeConnections[vehicleId.Value] = connection;

            // Publish success event
            await domainEventHub.PublishAsync(new VehicleConnected(vehicleId.Value, "UDP", endpoint, dateTimeProvider.UtcNow), cancellationToken);

            logger.LogInformation("Successfully connected to vehicle {VehicleId} via UDP {Endpoint}", vehicleId, endpoint);
            return new VehicleConnectionResult(true, vehicleId.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to vehicle via UDP local port {LocalPort}", localPort);
            await PublishConnectionFailed("UDP", $"UDP:{localPort}", ex.Message);
            return new VehicleConnectionResult(false, null, ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        if (!activeConnections.TryRemove(vehicleId, out var connection))
        {
            logger.LogWarning("Attempted to disconnect vehicle {VehicleId} but it was not found in active connections", vehicleId);
            return;
        }

        try
        {
            logger.LogInformation("Disconnecting vehicle {VehicleId}", vehicleId);

            await connection.Client.StopAsync();
            await connection.Transport.DisconnectAsync(cancellationToken);
            await connection.Transport.DisposeAsync();

            await domainEventHub.PublishAsync(new VehicleDisconnected(vehicleId, dateTimeProvider.UtcNow, "User requested disconnect"), cancellationToken);

            logger.LogInformation("Successfully disconnected vehicle {VehicleId}", vehicleId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while disconnecting vehicle {VehicleId}", vehicleId);
        }
    }

    /// <inheritdoc/>
    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Disconnecting all vehicles");

        var disconnectTasks = activeConnections.Keys
            .Select(vehicleId => DisconnectAsync(vehicleId, cancellationToken))
            .ToList();

        await Task.WhenAll(disconnectTasks);

        logger.LogInformation("All vehicles disconnected");
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

    private async Task PublishConnectionFailed(string connectionType, string endpoint, string error)
    {
        await domainEventHub.PublishAsync(new ConnectionFailed(connectionType, endpoint, error, dateTimeProvider.UtcNow));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAllAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Represents an active vehicle connection.
    /// </summary>
    private record ActiveConnection(
        VehicleId VehicleId,
        IMavLinkTransport Transport,
        IMavLinkClient Client,
        string ConnectionType,
        string Endpoint);
}
