using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Owns validated planning-polygon state and geometry operations.</summary>
public interface IPlanningPolygonService
{
    /// <summary>Raised when stable polygon state changes.</summary>
    event EventHandler? Changed;
    /// <summary>Gets current polygon state.</summary>
    PlanningPolygonSnapshot Snapshot { get; }
    /// <summary>Validates and replaces the current polygon.</summary>
    PlanningPolygonOperationResult Set(string name, IEnumerable<GeoPosition> vertices);
    /// <summary>Clears the current polygon.</summary>
    void Clear();
    /// <summary>Builds a polygon from positioned mission items.</summary>
    PlanningPolygonOperationResult FromMission(Mission mission);
    /// <summary>Creates, but does not apply, a signed metre offset preview.</summary>
    PlanningPolygonOperationResult PreviewOffset(double distanceMeters);
    /// <summary>Applies a previously generated preview.</summary>
    PlanningPolygonOperationResult ApplyPreview(PlanningPolygon preview);
    /// <summary>Calculates current polygon area.</summary>
    PlanningPolygonArea? CalculateArea();
    /// <summary>Serializes a polygon to the bounded versioned format.</summary>
    string Serialize(DateTimeOffset createdAt);
    /// <summary>Loads and validates serialized polygon content.</summary>
    PlanningPolygonOperationResult Deserialize(string content);
}
