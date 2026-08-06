namespace MissionPlanner.Simulation;

/// <summary>Contains all per-session results from a bounded fleet operation.</summary>
/// <param name="Results">Ordered operation results.</param>
public sealed record SimulationFleetOperationReport(IReadOnlyList<SimulationFleetOperationResult> Results)
{
    /// <summary>Gets whether every member operation succeeded.</summary>
    public bool Succeeded => Results.All(result => result.Succeeded);
}
