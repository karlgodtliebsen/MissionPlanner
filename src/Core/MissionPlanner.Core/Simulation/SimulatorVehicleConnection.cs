using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Simulation;

namespace MissionPlanner.Core.Simulation;

/// <summary>Connects SITL through the existing vehicle connection service and verifies its identity.</summary>
public sealed class SimulatorVehicleConnection(
    IVehicleConnectionService connectionService,
    IVehicleRegistry vehicleRegistry,
    ILogger<SimulatorVehicleConnection> logger) : ISimulatorVehicleConnection
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Guid? ownedConnectionId;

    /// <inheritdoc />
    public async Task<VehicleId> ConnectAsync(
        SimulatorProfile profile,
        SimulationEndpoint endpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(endpoint);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ownedConnectionId is not null)
            {
                throw new SimulationConnectionException("This simulator runtime already owns a vehicle connection.");
            }

            if (connectionService.IsConnected)
            {
                throw new SimulationConnectionException(
                    "MissionPlanner is already connected to a vehicle. Disconnect it before connecting this simulator.");
            }

            using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(timeout);
            VehicleConnectionResult result;
            try
            {
                result = await connectionService.ConnectUdpExclusiveAsync(
                    endpoint.Port,
                    endpoint.Host,
                    endpoint.Port,
                    timeoutCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new SimulationConnectionException(
                    $"The simulator process started, but no MAVLink heartbeat was received within {FormatTimeout(timeout)}.");
            }

            if (!result.Success || result.VehicleId is null || result.ConnectionId is null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (timeoutCancellation.IsCancellationRequested)
                {
                    throw new SimulationConnectionException(
                        $"The simulator process started, but no MAVLink heartbeat was received within {FormatTimeout(timeout)}.");
                }

                throw new SimulationConnectionException(
                    $"The simulator process started, but MAVLink connection failed: {result.ErrorMessage ?? "no heartbeat was received"}.");
            }

            ownedConnectionId = result.ConnectionId;
            var vehicleId = result.VehicleId.Value;
            var expectedSystemId = profile.EffectiveLaunchSettings.SystemId;
            var state = vehicleRegistry.GetRequired(vehicleId)?.State;
            if (vehicleId.SystemId != expectedSystemId || state?.Identity.Firmware.Family != profile.FirmwareFamily)
            {
                await DisconnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
                throw new SimulationConnectionException(
                    $"Received heartbeat {vehicleId} for {state?.Identity.Firmware.Family.ToString() ?? "unknown firmware"}; " +
                    $"expected system {expectedSystemId} and {profile.FirmwareFamily}.");
            }

            logger.LogInformation(
                "Connected simulator profile {ProfileId} to verified vehicle {VehicleId} using connection {ConnectionId}.",
                profile.Id,
                vehicleId,
                ownedConnectionId);
            return vehicleId;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisconnectCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        if (ownedConnectionId is not { } connectionId)
        {
            return;
        }

        await connectionService.DisconnectOwnedAsync(connectionId, cancellationToken).ConfigureAwait(false);
        ownedConnectionId = null;
    }

    private static string FormatTimeout(TimeSpan timeout)
    {
        return timeout.TotalSeconds < 1
            ? $"{timeout.TotalMilliseconds:0} milliseconds"
            : $"{timeout.TotalSeconds:0.#} seconds";
    }
}
