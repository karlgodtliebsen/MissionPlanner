using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Configuration.Fences;

/// <summary>Identifies a supported fence geometry primitive.</summary>
public enum FenceAreaKind
{
    /// <summary>The vehicle must remain inside the polygon.</summary>
    PolygonInclusion,
    /// <summary>The vehicle must remain outside the polygon.</summary>
    PolygonExclusion,
    /// <summary>The vehicle must remain inside the circle.</summary>
    CircleInclusion,
    /// <summary>The vehicle must remain outside the circle.</summary>
    CircleExclusion
}

/// <summary>Represents one independently editable fence area.</summary>
/// <param name="Id">The stable local area identifier.</param>
/// <param name="Kind">The geometry and inclusion kind.</param>
/// <param name="Vertices">The polygon vertices; closure is implicit and the first point is not repeated.</param>
/// <param name="Center">The circle center.</param>
/// <param name="RadiusMeters">The circle radius in meters.</param>
/// <param name="IsClosed">Whether polygon editing has been explicitly completed.</param>
public sealed record FenceArea(
    Guid Id,
    FenceAreaKind Kind,
    IReadOnlyList<GeoPosition> Vertices,
    GeoPosition? Center,
    double RadiusMeters,
    bool IsClosed)
{
    /// <summary>Creates an editable polygon area.</summary>
    /// <param name="kind">The inclusion or exclusion polygon kind.</param>
    /// <param name="vertices">The initial vertices.</param>
    /// <param name="isClosed">Whether polygon editing is complete.</param>
    /// <returns>The polygon area.</returns>
    public static FenceArea Polygon(FenceAreaKind kind, IReadOnlyList<GeoPosition> vertices, bool isClosed = false) =>
        new(Guid.NewGuid(), kind, vertices.ToArray(), null, 0, isClosed);

    /// <summary>Creates a circular area.</summary>
    /// <param name="kind">The inclusion or exclusion circle kind.</param>
    /// <param name="center">The circle center.</param>
    /// <param name="radiusMeters">The radius in meters.</param>
    /// <returns>The circular area.</returns>
    public static FenceArea Circle(FenceAreaKind kind, GeoPosition center, double radiusMeters) =>
        new(Guid.NewGuid(), kind, [], center, radiusMeters, true);
}

/// <summary>Represents a complete local or vehicle fence plan.</summary>
/// <param name="ReturnPoint">The optional legacy fence return point.</param>
/// <param name="Areas">The polygon and circle areas.</param>
public sealed record FencePlan(GeoPosition? ReturnPoint, IReadOnlyList<FenceArea> Areas)
{
    /// <summary>Gets an empty fence plan.</summary>
    public static FencePlan Empty { get; } = new(null, []);
}

/// <summary>Describes one fence validation problem.</summary>
/// <param name="Code">The stable problem code.</param>
/// <param name="Message">The user-facing explanation.</param>
/// <param name="AreaId">The affected area, when applicable.</param>
public sealed record FenceValidationIssue(string Code, string Message, Guid? AreaId = null);

/// <summary>Reports fence geometry and cross-parameter validation.</summary>
/// <param name="Issues">The validation problems.</param>
public sealed record FenceValidationResult(IReadOnlyList<FenceValidationIssue> Issues)
{
    /// <summary>Gets whether no validation problems were found.</summary>
    public bool IsValid => Issues.Count == 0;

    /// <summary>Gets a successful validation result.</summary>
    public static FenceValidationResult Valid { get; } = new([]);
}

/// <summary>Reports conversion from MAVLink fence mission items.</summary>
/// <param name="Plan">The converted plan.</param>
/// <param name="Errors">Protocol-shape errors encountered during conversion.</param>
public sealed record FenceProtocolParseResult(FencePlan Plan, IReadOnlyList<string> Errors)
{
    /// <summary>Gets whether all protocol items were converted.</summary>
    public bool Success => Errors.Count == 0;
}

/// <summary>Projects local, synchronized, and recoverable fence revisions.</summary>
/// <param name="VehicleId">The workspace vehicle.</param>
/// <param name="LocalPlan">The editable local plan.</param>
/// <param name="VehiclePlan">The last plan confirmed from or to the vehicle.</param>
/// <param name="BackupPlan">The plan saved before the latest replace or clear.</param>
/// <param name="LocalRevision">The monotonic local revision.</param>
/// <param name="VehicleRevision">The monotonic synchronized revision.</param>
/// <param name="IsDirty">Whether local geometry differs from the synchronized revision.</param>
public sealed record FenceConfigurationSnapshot(
    VehicleId VehicleId,
    FencePlan LocalPlan,
    FencePlan? VehiclePlan,
    FencePlan? BackupPlan,
    long LocalRevision,
    long? VehicleRevision,
    bool IsDirty);

/// <summary>Reports fence transfer progress.</summary>
/// <param name="Stage">The transfer stage.</param>
/// <param name="Completed">The completed item count.</param>
/// <param name="Total">The total item count.</param>
public sealed record FenceTransferProgress(string Stage, int Completed, int Total);

/// <summary>Reports a fence download, upload, or clear operation.</summary>
/// <param name="Success">Whether the operation completed and was confirmed.</param>
/// <param name="Message">The user-facing result.</param>
/// <param name="Snapshot">The resulting workspace snapshot.</param>
/// <param name="Validation">The validation result.</param>
/// <param name="ParameterReport">The grouped parameter result, when parameters were applied.</param>
public sealed record FenceOperationReport(
    bool Success,
    string Message,
    FenceConfigurationSnapshot Snapshot,
    FenceValidationResult Validation,
    ParameterApplyReport? ParameterReport = null);
