using Microsoft.Extensions.Logging;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Models;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Client;
using MissionPlanner.Shared.Models.Vehicles.Models;
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
    IVehicleRegistry vehicleRegistry,
    IPlannerSettingsService plannerSettings,
    IVehicleParameterLoadStatusContext parameterLoadStatus,
    ILogger<VehicleConnectionService> logger)
    : IVehicleConnectionService
{
    // Single active connection (only one vehicle connection supported at a time)
    private ActiveConnection? activeConnection;
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private CancellationTokenSource? parameterPreloadCancellation;
    private Task? parameterPreloadTask;
    private int lastPublishedParameterPercent = -1;

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
            var vehicleId = await WaitForVehicleHeartbeatAsync(linkedCts.Token);

            if (vehicleId == null)
            {
                await connectionSession.DisconnectAsync(vehicleId, linkedCts.Token);
                await PublishConnectionFailed("SERIAL", $"{portName} {baudRate}", "No heartbeat received from vehicle");
                return new VehicleConnectionResult(false, null, null, "Timeout waiting for vehicle heartbeat");
            }

            await RequestFirmwareIdentityAsync(vehicleId.Value, linkedCts.Token);

            // Request telemetry streams from vehicle
            await RequestTelemetryStreamsAsync(vehicleId.Value, linkedCts.Token);

            // Store active connection
            var connectionId = Guid.NewGuid();
            activeConnection = new ActiveConnection(connectionId, vehicleId.Value, transport, client, "Serial", portName);

            // Publish success event
            await domainEventHub.PublishDomainEventAsync(new VehicleConnected(vehicleId.Value, "Serial", portName, dateTimeProvider.UtcNow), linkedCts.Token);
            StartParameterPreload(vehicleId.Value);

            logger.LogInformation("Successfully connected to vehicle {VehicleId} via serial port {PortName}", vehicleId, portName);
            return new VehicleConnectionResult(true, vehicleId.Value, connectionSession, ConnectionId: connectionId);
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
            var client = connectionSession.Client;
            var transport = connectionSession.Transport;
            // Wait for heartbeat to identify vehicle
            var vehicleId = await WaitForVehicleHeartbeatAsync(linkedCts.Token);

            if (vehicleId == null)
            {
                await connectionSession.DisconnectAsync(vehicleId, linkedCts.Token);
                await PublishConnectionFailed("TCP", endpoint, "No heartbeat received from vehicle");
                return new VehicleConnectionResult(false, null, null, "Timeout waiting for vehicle heartbeat");
            }


            await RequestFirmwareIdentityAsync(vehicleId.Value, linkedCts.Token);

            // Request telemetry streams from vehicle
            await RequestTelemetryStreamsAsync(vehicleId.Value, linkedCts.Token);

            // Store active connection
            var connectionId = Guid.NewGuid();
            activeConnection = new ActiveConnection(connectionId, vehicleId.Value, transport, client, "TCP", endpoint);

            // Publish success event
            await domainEventHub.PublishDomainEventAsync(new VehicleConnected(vehicleId.Value, "TCP", endpoint, dateTimeProvider.UtcNow), linkedCts.Token);
            StartParameterPreload(vehicleId.Value);

            logger.LogInformation("Successfully connected to vehicle {VehicleId} via TCP {Endpoint}", vehicleId, endpoint);
            return new VehicleConnectionResult(true, vehicleId.Value, connectionSession, ConnectionId: connectionId);
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
    public Task<VehicleConnectionResult> ConnectUdpAsync(int localPort, string? remoteHost = null, int? remotePort = null, CancellationToken cancellationToken = default)
    {
        return ConnectUdpCoreAsync(localPort, remoteHost, remotePort, true, cancellationToken);
    }

    /// <inheritdoc />
    public Task<VehicleConnectionResult> ConnectUdpExclusiveAsync(int localPort, string? remoteHost = null, int? remotePort = null, CancellationToken cancellationToken = default)
    {
        return ConnectUdpCoreAsync(localPort, remoteHost, remotePort, false, cancellationToken);
    }

    private async Task<VehicleConnectionResult> ConnectUdpCoreAsync(int localPort, string? remoteHost, int? remotePort, bool replaceExisting, CancellationToken cancellationToken)
    {
        var token = cancellationToken;
        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Disconnect existing connection if any
            if (activeConnection != null)
            {
                if (!replaceExisting)
                {
                    return new VehicleConnectionResult(
                        false,
                        null,
                        null,
                        "A vehicle connection is already active and was left unchanged.");
                }

                logger.LogInformation("Disconnecting existing connection before establishing new one");
                await DisconnectInternalAsync(cancellationToken);
            }

            logger.LogInformation("Connecting to vehicle via UDP local port {LocalPort}", localPort);

            var endpoint = $"UDP:{localPort}";

            var linkedCts = await connectionSession.CreateUdpConnection(localPort, remoteHost ?? "127.0.0.1", remotePort ?? 14550, null, token);
            var client = connectionSession.Client;
            var transport = connectionSession.Transport;
            // Wait for heartbeat to identify vehicle
            var vehicleId = await WaitForVehicleHeartbeatAsync(linkedCts.Token);

            if (vehicleId == null)
            {
                await connectionSession.DisconnectAsync(vehicleId, linkedCts.Token);
                await PublishConnectionFailed("UDP", endpoint, "No heartbeat received from vehicle");
                return new VehicleConnectionResult(false, null, null, "Timeout waiting for vehicle heartbeat");
            }

            await RequestFirmwareIdentityAsync(vehicleId.Value, linkedCts.Token);

            // Request telemetry streams from vehicle
            await RequestTelemetryStreamsAsync(vehicleId.Value, linkedCts.Token);

            // Store active connection
            var connectionId = Guid.NewGuid();
            activeConnection = new ActiveConnection(connectionId, vehicleId.Value, transport, client, "UDP", endpoint);
            await domainEventHub.PublishDomainEventAsync(new VehicleConnected(vehicleId.Value, "UDP", endpoint, dateTimeProvider.UtcNow), linkedCts.Token);
            StartParameterPreload(vehicleId.Value);

            logger.LogInformation("Successfully connected to vehicle {VehicleId} via UDP {Endpoint}", vehicleId, endpoint);
            return new VehicleConnectionResult(true, vehicleId.Value, connectionSession, ConnectionId: connectionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to vehicle via UDP local port {LocalPort}", localPort);
            try
            {
                await connectionSession.DisconnectAsync(activeConnection?.VehicleId, token);
            }
            catch (Exception cleanupException)
            {
                logger.LogWarning(cleanupException, "Failed to clean up unsuccessful UDP connection attempt on port {LocalPort}", localPort);
            }

            activeConnection = null;
            await PublishConnectionFailed("UDP", $"UDP:{localPort}", ex.Message);
            return new VehicleConnectionResult(false, null, null, ex.Message);
        }
        finally
        {
            connectionLock.Release();
        }
    }

    private async Task<VehicleId?> WaitForVehicleHeartbeatAsync(CancellationToken cancellationToken)
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
            timeoutCts.Dispose();
        }
    }

    /// <summary>
    /// Requests essential telemetry streams from the vehicle after connection.
    /// </summary>
    private async Task RequestTelemetryStreamsAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        try
        {
            var commandService = domainFactory.Create<IMavLinkCommandService, IVehicleConnectionSession>(connectionSession);

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

    private async Task RequestFirmwareIdentityAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        const int maxAttempts = 2;
        var commandService = domainFactory.Create<IMavLinkCommandService, IVehicleConnectionSession>(connectionSession);

        for (var attempt = 1; attempt <= maxAttempts && !cancellationToken.IsCancellationRequested; attempt++)
        {
            if (!await commandService.RequestAutopilotVersionAsync(vehicleId, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            var deadline = dateTimeProvider.UtcNow + TimeSpan.FromSeconds(1);
            while (dateTimeProvider.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                if (vehicleRegistry.GetRequired(vehicleId)?.State.Identity.Firmware.FlightVersion is not null)
                {
                    return;
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }

        logger.LogWarning("AUTOPILOT_VERSION was not received from {VehicleId}", vehicleId);
    }

    private async Task PublishConnectionFailed(string connectionType, string endpoint, string error)
    {
        await domainEventHub.PublishDomainEventAsync(new ConnectionFailed(connectionType, endpoint, error, dateTimeProvider.UtcNow));
    }

    // private bool isDisposing = false;

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (activeConnection == null)
        {
            return;
        }

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

    /// <inheritdoc />
    public async Task<bool> DisconnectOwnedAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (activeConnection?.ConnectionId != connectionId)
            {
                logger.LogInformation("Ignored owned disconnect for stale connection generation {ConnectionId}.", connectionId);
                return false;
            }

            await DisconnectInternalAsync(cancellationToken);
            return true;
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

            await CancelParameterPreloadAsync().ConfigureAwait(false);

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

    private void StartParameterPreload(VehicleId vehicleId)
    {
        if (!plannerSettings.Current.Legacy.DownloadParametersInBackground)
        {
            logger.LogDebug(
                "Background parameter loading is disabled for {VehicleId}.",
                vehicleId);
            return;
        }

        parameterPreloadCancellation?.Cancel();
        parameterPreloadCancellation?.Dispose();

        var cancellation = new CancellationTokenSource();
        parameterPreloadCancellation = cancellation;
        parameterPreloadTask = PreloadParametersAsync(vehicleId, cancellation.Token);
    }

    private async Task PreloadParametersAsync(
        VehicleId vehicleId,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "Preloading parameters for connected vehicle {VehicleId}.",
                vehicleId);

            lastPublishedParameterPercent = -1;
            await PublishParameterLoadStatusAsync(
                vehicleId,
                ParameterLoadState.Starting,
                message: "Preparing to download vehicle parameters…",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var progress = new CallbackProgress<ParameterStreamProgress>(value =>
            {
                var percent = Math.Clamp(value.PercentComplete, 0, 100);
                if (value.Message is null && !value.IsComplete &&
                    Interlocked.Exchange(ref lastPublishedParameterPercent, percent) == percent)
                {
                    return;
                }

                var message = value.Message ?? (value.TotalCount > 0
                    ? $"Downloading parameters… {value.ReceivedCount}/{value.TotalCount} ({percent}%)"
                    : "Waiting for parameter data…");
                PublishParameterLoadStatus(
                    new ParameterLoadStatus(
                        vehicleId,
                        ParameterLoadState.Downloading,
                        value.ReceivedCount,
                        value.TotalCount,
                        percent,
                        message,
                        dateTimeProvider.UtcNow));
            });

            var result = await connectionSession.ParameterStreamService
                .StreamAllParametersWithRetryAsync(
                    vehicleId,
                    progress,
                    maxRetries: 3,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                await PublishParameterLoadStatusAsync(
                    vehicleId,
                    ParameterLoadState.Cancelled,
                    result.Parameters.Count,
                    result.TotalCount,
                    result.TotalCount > 0 ? result.Parameters.Count * 100 / result.TotalCount : 0,
                    "Parameter loading was cancelled.").ConfigureAwait(false);
                return;
            }

            if (result.Success)
            {
                await PublishParameterLoadStatusAsync(
                    vehicleId,
                    ParameterLoadState.Completed,
                    result.Parameters.Count,
                    result.TotalCount,
                    100,
                    $"Loaded {result.Parameters.Count} vehicle parameters.").ConfigureAwait(false);
                logger.LogInformation(
                    "Preloaded {Count} parameters for {VehicleId}.",
                    result.Parameters.Count,
                    vehicleId);
            }
            else if (!cancellationToken.IsCancellationRequested)
            {
                await PublishParameterLoadStatusAsync(
                    vehicleId,
                    ParameterLoadState.Failed,
                    result.Parameters.Count,
                    result.TotalCount,
                    result.TotalCount > 0 ? result.Parameters.Count * 100 / result.TotalCount : 0,
                    result.ErrorMessage ?? "Parameter loading failed.").ConfigureAwait(false);
                logger.LogWarning(
                    "Parameter preload for {VehicleId} was incomplete: {Error}",
                    vehicleId,
                    result.ErrorMessage);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await PublishParameterLoadStatusAsync(
                vehicleId,
                ParameterLoadState.Cancelled,
                message: "Parameter loading was cancelled.").ConfigureAwait(false);
            logger.LogDebug("Parameter preload cancelled for {VehicleId}.", vehicleId);
        }
        catch (Exception exception)
        {
            await PublishParameterLoadStatusAsync(
                vehicleId,
                ParameterLoadState.Failed,
                message: $"Parameter loading failed: {exception.Message}").ConfigureAwait(false);
            logger.LogWarning(
                exception,
                "Background parameter preload failed for {VehicleId}.",
                vehicleId);
        }
    }

    private Task PublishParameterLoadStatusAsync(
        VehicleId vehicleId,
        ParameterLoadState state,
        int receivedCount = 0,
        int totalCount = 0,
        int percentComplete = 0,
        string? message = null,
        CancellationToken cancellationToken = default)
    {
        var status = new ParameterLoadStatus(
            vehicleId,
            state,
            receivedCount,
            totalCount,
            percentComplete,
            message ?? state.ToString(),
            dateTimeProvider.UtcNow);
        parameterLoadStatus.Update(status);
        return domainEventHub.PublishDomainEventAsync(
            new VehicleParameterLoadStatusChanged(status),
            cancellationToken);
    }

    private void PublishParameterLoadStatus(ParameterLoadStatus status)
    {
        parameterLoadStatus.Update(status);
        _ = domainEventHub.PublishDomainEventAsync(new VehicleParameterLoadStatusChanged(status))
            .ContinueWith(
                task => logger.LogWarning(task.Exception, "Could not publish parameter loading progress for {VehicleId}.", status.VehicleId),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
    }

    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }

    private async Task CancelParameterPreloadAsync()
    {
        var cancellation = parameterPreloadCancellation;
        var preloadTask = parameterPreloadTask;
        parameterPreloadCancellation = null;
        parameterPreloadTask = null;

        if (cancellation is null)
        {
            return;
        }

        try
        {
            await cancellation.CancelAsync().ConfigureAwait(false);
            if (preloadTask is not null)
            {
                await preloadTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected while ending the connection-owned preload.
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CancelParameterPreloadAsync().ConfigureAwait(false);

        // Disconnect the active connection (if any)
        if (activeConnection != null)
        {
            await connectionSession.DisposeAsync();
        }

        activeConnection = null;
        // Dispose the semaphore and cancellation token source
        connectionLock.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Represents an active vehicle connection.
    /// </summary>
    private record ActiveConnection(
        Guid ConnectionId,
        VehicleId VehicleId,
        IMavLinkTransport Transport,
        IMavLinkClient Client,
        string ConnectionType,
        string Endpoint);
}
