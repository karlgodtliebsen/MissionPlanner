namespace MissionPlanner.Core.FlightData.Scripting;

/// <summary>Executes validated scripts sequentially through typed services.</summary>
public interface IVehicleScriptExecutor
{
    /// <summary>Dry-runs or executes a script, producing an ordered complete log.</summary>
    Task<IReadOnlyList<VehicleScriptStepResult>> ExecuteAsync(VehicleScriptDocument document, bool dryRun,
        CancellationToken cancellationToken);
}
