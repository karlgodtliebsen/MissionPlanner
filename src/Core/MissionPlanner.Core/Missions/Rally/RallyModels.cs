using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Missions.Rally;

/// <summary>Stable local rally-point identity.</summary>
public readonly record struct RallyPointId(Guid Value) { /// <summary>Creates an identity.</summary>
    public static RallyPointId New() => new(Guid.NewGuid()); }

/// <summary>A rally location and its altitude semantics.</summary>
public sealed record RallyPoint(RallyPointId Id, GeoPosition Position, MissionAltitude Altitude);

/// <summary>An ordered rally plan, separate from the flight mission.</summary>
public sealed record RallyPlan(IReadOnlyList<RallyPoint> Points) { /// <summary>Empty plan.</summary>
    public static RallyPlan Empty { get; } = new([]); }

/// <summary>Active-vehicle rally workspace state.</summary>
public sealed record RallyPlanSnapshot(VehicleId VehicleId, RallyPlan LocalPlan, RallyPlan? VehiclePlan,
    long LocalRevision, long? VehicleRevision, bool IsDirty, DateTimeOffset? LastDownloadedAt);

/// <summary>Result of a rally transfer operation.</summary>
public sealed record RallyOperationResult(bool Success, string Message, RallyPlanSnapshot Snapshot);

/// <summary>Maps rally domain objects to the typed MAVLink mission protocol.</summary>
public interface IRallyProtocolMapper
{
    /// <summary>Maps an ordered plan to rally mission items.</summary>
    IReadOnlyList<MissionPlanner.MavLink.Missions.MavLinkMissionItem> ToProtocol(RallyPlan plan);
    /// <summary>Parses rally mission items.</summary>
    RallyPlan FromProtocol(IReadOnlyList<MissionPlanner.MavLink.Missions.MavLinkMissionItem> items);
}

/// <summary>Owns active-vehicle-scoped local and synchronized rally plans.</summary>
public interface IRallyConfigurationService
{
    /// <summary>Raised when a workspace changes.</summary>
    event EventHandler? Changed;
    /// <summary>Gets one vehicle workspace.</summary>
    RallyPlanSnapshot GetSnapshot(VehicleId vehicleId);
    /// <summary>Replaces a local plan.</summary>
    RallyPlanSnapshot SetLocalPlan(VehicleId vehicleId, RallyPlan plan);
    /// <summary>Downloads rally points.</summary>
    Task<RallyOperationResult> DownloadAsync(VehicleId vehicleId, bool replaceLocal, CancellationToken cancellationToken = default);
    /// <summary>Uploads the local rally plan.</summary>
    Task<RallyOperationResult> UploadAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
    /// <summary>Clears acknowledged vehicle rally points.</summary>
    Task<RallyOperationResult> ClearVehicleAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
}

/// <summary>Versioned rally JSON codec.</summary>
public interface IRallyPlanFileCodec
{
    /// <summary>Serializes a rally plan.</summary>
    string Serialize(RallyPlan plan, DateTimeOffset createdAt);
    /// <summary>Parses a rally plan.</summary>
    RallyPlan Deserialize(string json);
}
