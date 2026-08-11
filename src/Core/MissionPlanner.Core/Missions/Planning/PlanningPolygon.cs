using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>An immutable planning polygon independent from missions and fences.</summary>
/// <param name="Name">Polygon name.</param>
/// <param name="Vertices">Ordered boundary vertices without a repeated closing vertex.</param>
public sealed record PlanningPolygon(string Name, IReadOnlyList<GeoPosition> Vertices);

/// <summary>Immutable polygon workspace state.</summary>
/// <param name="Polygon">Current polygon, or <see langword="null"/>.</param>
/// <param name="Revision">Monotonic workspace revision.</param>
public sealed record PlanningPolygonSnapshot(PlanningPolygon? Polygon, int Revision)
{
    /// <summary>Gets the empty workspace state.</summary>
    public static PlanningPolygonSnapshot Empty { get; } = new(null, 0);
}

/// <summary>Result of a polygon workspace operation.</summary>
/// <param name="Succeeded">Whether the operation succeeded.</param>
/// <param name="Message">User-facing result.</param>
/// <param name="Preview">Optional preview polygon not yet applied.</param>
public sealed record PlanningPolygonOperationResult(bool Succeeded, string Message, PlanningPolygon? Preview = null);

/// <summary>Area measurements for a planning polygon.</summary>
/// <param name="SquareMeters">Area in square metres.</param>
public sealed record PlanningPolygonArea(double SquareMeters)
{
    /// <summary>Area in hectares.</summary>
    public double Hectares => SquareMeters / 10_000;
    /// <summary>Area in square kilometres.</summary>
    public double SquareKilometers => SquareMeters / 1_000_000;
    /// <summary>Area in acres.</summary>
    public double Acres => SquareMeters / 4046.8564224;
    /// <summary>Area in square feet.</summary>
    public double SquareFeet => SquareMeters * 10.7639104167;
}
