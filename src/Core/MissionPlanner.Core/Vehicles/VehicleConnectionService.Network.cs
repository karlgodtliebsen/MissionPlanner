using Microsoft.Extensions.Logging;
using MissionPlanner.Core.DomainEvents;

namespace MissionPlanner.Core.Vehicles;

public partial class VehicleConnectionService
{
    /// <inheritdoc />
    public Task<VehicleConnectionResult> ConnectNetworkAsync(NetworkConnectionSettings settings,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        => ConnectNetworkCoreAsync(settings, true, progress, cancellationToken);

    private async Task<VehicleConnectionResult> ConnectNetworkCoreAsync(NetworkConnectionSettings settings,
        bool replaceExisting, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        if (settings.Validate() is { } error)
        {
            return new(false, null, null, error);
        }
        await connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (activeConnection is not null)
            {
                if (!replaceExisting)
                {
                    return new(false, null, null, "Another connection is active; it was left unchanged.");
                }
                await DisconnectInternalAsync(cancellationToken).ConfigureAwait(false);
            }
            progress?.Report($"Opening {settings.Description}…");
            var started = dateTimeProvider.UtcNow;
            var lifetime = await connectionSession.CreateNetworkConnection(settings,
                plannerSettings.Current.Legacy.GcsSystemId, cancellationToken).ConfigureAwait(false);
            progress?.Report("Waiting for vehicle heartbeat…");
            var vehicleId = await WaitForVehicleHeartbeatAsync(lifetime.Token).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (vehicleId is null)
            {
                var failure = connectionSession.Client.ReceiveFailure?.Message
                    ?? "Timeout waiting for vehicle heartbeat. Check the endpoint and remote MAVLink sender.";
                await CleanupFailedConnectionAsync().ConfigureAwait(false);
                await PublishConnectionFailed(settings.Channel, settings.Description, failure).ConfigureAwait(false);
                return new(false, null, null, failure);
            }
            await RequestFirmwareIdentityAsync(vehicleId.Value, lifetime.Token).ConfigureAwait(false);
            await RequestTelemetryStreamsAsync(vehicleId.Value, lifetime.Token).ConfigureAwait(false);
            var id = Guid.NewGuid();
            activeConnection = new ActiveConnection(id, vehicleId.Value, connectionSession.Transport,
                connectionSession.Client, settings.Channel, settings.Description)
            {
                ReconnectTarget = new(id, settings.Channel, null, settings.LocalPort, 0) { Network = settings }
            };
            await domainEventHub.PublishDomainEventAsync(new VehicleConnected(vehicleId.Value, settings.Channel,
                settings.Description, dateTimeProvider.UtcNow), lifetime.Token).ConfigureAwait(false);
            monitorLease = connectionMonitor?.Track(vehicleId.Value, id, connectionSession,
                reason => DisconnectLostConnectionAsync(id, reason));
            StartParameterPreload(vehicleId.Value);
            _ = firmwareUpdates?.Start(vehicleId.Value, started);
            progress?.Report("Receiving vehicle telemetry.");
            return new(true, vehicleId.Value, connectionSession, ConnectionId: id);
        }
        catch (OperationCanceledException)
        {
            await CleanupFailedConnectionAsync().ConfigureAwait(false);
            return new(false, null, null, cancellationToken.IsCancellationRequested
                ? "Connection cancelled." : "Network connection timed out while opening the endpoint.");
        }
        catch (Exception ex)
        {
            await CleanupFailedConnectionAsync().ConfigureAwait(false);
            // Transport errors are sanitized; avoid logging a request URL or server response.
            var failure = settings.Channel is "WS" or "WSS"
                ? "WebSocket connection failed. Check the URL, server availability, and TLS certificate trust."
                : $"UDP connection failed: {ex.Message}";
            logger.LogWarning("Network connection failed: {Failure}", failure);
            await PublishConnectionFailed(settings.Channel, settings.Description, failure).ConfigureAwait(false);
            return new(false, null, null, failure);
        }
        finally
        {
            connectionLock.Release();
        }
    }
}
