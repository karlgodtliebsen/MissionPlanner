using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Adjustments;

public enum VehicleSpeedTargetType { GroundSpeed, Airspeed }

public enum VehicleAdjustmentStatus
{
    CommandAccepted,
    TargetSentButNotTelemetryConfirmed,
    TelemetryConfirmed,
    ParameterConfirmed,
    Denied,
    Unsupported,
    Failed,
    Busy,
    Timeout
}

public sealed record VehicleAdjustmentResult(VehicleId VehicleId, VehicleAdjustmentStatus Status, string Message, float? PersistedValue = null);
