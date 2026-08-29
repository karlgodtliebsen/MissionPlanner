using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Missions.Models;

/// <summary>Describes the strongest protocol confirmation obtained for a mission intervention.</summary>
public enum MissionInterventionStatus
{
    TelemetryConfirmed,
    AcceptedButNotTelemetryConfirmed,
    FallbackTelemetryConfirmed,
    Denied,
    Unsupported,
    Failed,
    Busy,
    Timeout
}

/// <summary>Typed result returned by mission intervention operations.</summary>
public sealed record MissionInterventionResult(VehicleId VehicleId, MissionInterventionStatus Status, string Message)
{
    public bool IsAccepted => Status is MissionInterventionStatus.TelemetryConfirmed or MissionInterventionStatus.AcceptedButNotTelemetryConfirmed or MissionInterventionStatus.FallbackTelemetryConfirmed;
}
