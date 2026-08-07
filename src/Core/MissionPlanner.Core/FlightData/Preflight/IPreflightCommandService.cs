using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Preflight;

/// <summary>Runs the typed ArduPilot pre-arm diagnostic command.</summary>
public interface IPreflightCommandService
{
    /// <summary>Runs pre-arm checks for a disarmed live vehicle.</summary>
    Task<PreflightCommandResult> RunAsync(VehicleState state, CancellationToken cancellationToken);
}
