namespace MissionPlanner.Core.Vehicles;

public partial class VehicleConnectionService
{
    /// <inheritdoc />
    public VehicleReconnectTarget? CaptureReconnectTarget() => activeConnection?.ReconnectTarget;

    /// <inheritdoc />
    public async Task<VehicleConnectionResult> ReconnectAsync(VehicleReconnectTarget target,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            progress?.Report($"Waiting for {target.Description} to disconnect completely…");
            // The same lock serializes monitor-triggered teardown. Never close a replacement connection.
            await DisconnectOwnedAsync(target.ConnectionId, deadline.Token).ConfigureAwait(false);
            if (IsConnected)
            {
                return new(false, null, null, "Another connection is active; reconnect was stopped.");
            }
            await Task.Delay(TimeSpan.FromSeconds(2), deadline.Token).ConfigureAwait(false);
            VehicleConnectionResult result = new(false, null, null, "Reconnect did not complete.");
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                deadline.Token.ThrowIfCancellationRequested();
                progress?.Report($"Reconnecting to {target.Description} (attempt {attempt} of 3). Waiting for a vehicle heartbeat…");
                result = target.ConnectionType switch
                {
                    "Serial" => await ConnectSerialExclusiveAsync(target.Address!, target.BaudRate, deadline.Token).ConfigureAwait(false),
                    "TCP" => await ConnectTcpCoreAsync(target.Address!, target.Port, false, deadline.Token).ConfigureAwait(false),
                    "UDP" => await ConnectUdpExclusiveAsync(target.Port, target.Address, target.RemotePort, deadline.Token).ConfigureAwait(false),
                    _ => new(false, null, null, "This connection type cannot be reconnected automatically.")
                };
                deadline.Token.ThrowIfCancellationRequested();
                if (result.Success || IsConnected)
                {
                    return result;
                }
                if (attempt < 3)
                {
                    progress?.Report($"No connection yet. Waiting before retrying {target.Description}…");
                    await Task.Delay(TimeSpan.FromSeconds(2), deadline.Token).ConfigureAwait(false);
                }
            }
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, null, null, "Reconnect timed out after 30 seconds. Check the vehicle connection and try again.");
        }
    }
}
