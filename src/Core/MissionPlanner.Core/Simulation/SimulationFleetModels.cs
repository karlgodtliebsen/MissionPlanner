using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Describes a relative launch offset without introducing autonomous formation control.</summary>
/// <param name="NorthMeters">North offset from the base home location.</param>
/// <param name="EastMeters">East offset from the base home location.</param>
/// <param name="AltitudeMeters">Altitude offset from the base home location.</param>
/// <param name="HeadingDegrees">Heading offset from the base home heading.</param>
public sealed record SimulationFormationOffset(
    double NorthMeters,
    double EastMeters,
    double AltitudeMeters = 0,
    double HeadingDegrees = 0);

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

/// <summary>Requests deterministic allocation and launch of multiple SITL instances.</summary>
/// <param name="BaseProfile">Base simulator profile.</param>
/// <param name="Count">Number of instances.</param>
/// <param name="Formation">Ordered launch offsets.</param>
/// <param name="PortStride">Port increment per instance.</param>
/// <param name="MaximumConcurrency">Maximum concurrent start or stop operations.</param>
public sealed record SimulationFleetLaunchRequest(
    SimulatorProfile BaseProfile,
    int Count,
    SimulationFormationProfile Formation,
    int PortStride = 10,
    int MaximumConcurrency = 3);

/// <summary>Contains all resources deterministically allocated to one fleet member.</summary>
/// <param name="FleetSessionId">Stable identity derived from the base profile and member index.</param>
/// <param name="Index">Zero-based fleet member index.</param>
/// <param name="Profile">Fully allocated launch profile.</param>
/// <param name="Offset">Applied launch offset.</param>
/// <param name="Artifacts">Isolated artifact paths.</param>
public sealed record SimulationInstanceAllocation(
    Guid FleetSessionId,
    int Index,
    SimulatorProfile Profile,
    SimulationFormationOffset Offset,
    SimulationInstanceArtifacts Artifacts);

/// <summary>Describes one observable member of the simulation fleet.</summary>
/// <param name="Allocation">Deterministic member allocation.</param>
/// <param name="Session">Current runtime session state.</param>
/// <param name="IsSelected">Whether this member is the active workspace selection.</param>
public sealed record SimulationFleetSessionSnapshot(
    SimulationInstanceAllocation Allocation,
    SimulationSessionSnapshot Session,
    bool IsSelected)
{
    /// <summary>Gets the exact connected vehicle target when the member is ready.</summary>
    public VehicleId? VehicleId => Session.VehicleId;
}

/// <summary>Contains one member's start or stop result.</summary>
/// <param name="FleetSessionId">Exact fleet member identity.</param>
/// <param name="Succeeded">Whether the requested terminal state was reached.</param>
/// <param name="Session">Resulting session snapshot.</param>
/// <param name="Error">Per-session failure detail.</param>
public sealed record SimulationFleetOperationResult(
    Guid FleetSessionId,
    bool Succeeded,
    SimulationSessionSnapshot Session,
    string? Error);

/// <summary>Contains all per-session results from a bounded fleet operation.</summary>
/// <param name="Results">Ordered operation results.</param>
public sealed record SimulationFleetOperationReport(IReadOnlyList<SimulationFleetOperationResult> Results)
{
    /// <summary>Gets whether every member operation succeeded.</summary>
    public bool Succeeded => Results.All(result => result.Succeeded);
}

/// <summary>Signals a deterministic resource-allocation conflict.</summary>
public sealed class SimulationAllocationException : Exception
{
    /// <summary>Initializes an allocation conflict.</summary>
    /// <param name="message">Actionable conflict detail.</param>
    public SimulationAllocationException(string message)
        : base(message)
    {
    }
}

/// <summary>Provides fleet state-change data.</summary>
/// <param name="session">The member that changed.</param>
public sealed class SimulationFleetChangedEventArgs(SimulationFleetSessionSnapshot session) : EventArgs
{
    /// <summary>Gets the changed fleet member snapshot.</summary>
    public SimulationFleetSessionSnapshot Session { get; } = session;
}
