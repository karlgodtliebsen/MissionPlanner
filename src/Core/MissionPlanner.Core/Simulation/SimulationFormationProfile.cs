namespace MissionPlanner.Core.Simulation;

/// <summary>Contains launch-only offsets for a named multi-instance layout.</summary>
/// <param name="Name">User-facing formation profile name.</param>
/// <param name="Offsets">Ordered per-instance launch offsets.</param>
public sealed record SimulationFormationProfile(
    string Name,
    IReadOnlyList<SimulationFormationOffset> Offsets)
{
    /// <summary>Creates a north/south line of launch positions.</summary>
    /// <param name="count">Number of positions.</param>
    /// <param name="spacingMeters">Spacing between positions.</param>
    /// <returns>The launch-offset data.</returns>
    public static SimulationFormationProfile CreateLine(int count, double spacingMeters) =>
        new("Line", Enumerable.Range(0, count)
            .Select(index => new SimulationFormationOffset(index * spacingMeters, 0))
            .ToArray());

    /// <summary>Creates a square-grid set of launch positions.</summary>
    /// <param name="count">Number of positions.</param>
    /// <param name="spacingMeters">Spacing between positions.</param>
    /// <returns>The launch-offset data.</returns>
    public static SimulationFormationProfile CreateGrid(int count, double spacingMeters)
    {
        var width = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(count)));
        return new SimulationFormationProfile(
            "Grid",
            Enumerable.Range(0, count)
                .Select(index => new SimulationFormationOffset(
                    index / width * spacingMeters,
                    index % width * spacingMeters))
                .ToArray());
    }
}
