using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Commands;

namespace MissionPlanner.Core.FlightData.Preflight;

/// <summary>Executes pre-arm diagnostics through the acknowledged command workflow.</summary>
public sealed class PreflightCommandService(
    IVehicleCommandService commandService,
    IVehicleMessageStore messageStore,
    IReplaySessionManager? replay = null) : IPreflightCommandService
{
    /// <inheritdoc />
    public async Task<PreflightCommandResult> RunAsync(VehicleState state, CancellationToken cancellationToken)
    {
        if (replay?.Snapshot.State != ReplaySessionState.Unloaded)
        {
            return new PreflightCommandResult(null, [], "Pre-arm checks are unavailable during telemetry replay.");
        }

        if (state.IsArmed)
        {
            return new PreflightCommandResult(null, [], "Disarm the vehicle before running pre-arm checks.");
        }

        var start = DateTimeOffset.UtcNow;
        var response = await commandService.ExecuteExpertAsync(
            new ExpertVehicleCommand(state.VehicleId, MavLinkCommandIds.RunPrearmChecks, [0, 0, 0, 0, 0, 0, 0]), true, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        var diagnostics = messageStore.GetMessages(state.VehicleId)
            .Where(x => x.ReceivedAt >= start)
            .ToArray();
        return new PreflightCommandResult(response, diagnostics, $"Command result: {response.Result}; captured {diagnostics.Length} diagnostic message(s).");
    }
}
