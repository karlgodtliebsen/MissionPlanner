using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Missions.Models;

/// <summary>Describes the strongest protocol confirmation obtained for a mission intervention.</summary>
public enum MissionInterventionStatus
{
    /// <summary>Telemetry shows the vehicle applied the change.</summary>
    TelemetryConfirmed,
    /// <summary>The vehicle accepted the command but telemetry has not confirmed it.</summary>
    AcceptedButNotTelemetryConfirmed,
    /// <summary>The primary command was not supported and a fallback command was confirmed by telemetry.</summary>
    FallbackTelemetryConfirmed,
    /// <summary>The vehicle or the safety policy denied the request.</summary>
    Denied,
    /// <summary>The vehicle does not support the request.</summary>
    Unsupported,
    /// <summary>The request failed.</summary>
    Failed,
    /// <summary>The vehicle is busy with another operation.</summary>
    Busy,
    /// <summary>The vehicle did not answer in time.</summary>
    Timeout
}

/// <summary>Typed result returned by mission intervention operations.</summary>
public sealed record MissionInterventionResult(VehicleId VehicleId, MissionInterventionStatus Status, string Message)
{
    /// <summary>Gets whether the vehicle accepted the intervention.</summary>
    public bool IsAccepted => Status is MissionInterventionStatus.TelemetryConfirmed or MissionInterventionStatus.AcceptedButNotTelemetryConfirmed or MissionInterventionStatus.FallbackTelemetryConfirmed;
}
