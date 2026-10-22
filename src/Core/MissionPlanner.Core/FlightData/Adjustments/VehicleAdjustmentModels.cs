using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.FlightData.Adjustments;

/// <summary>Which speed a speed target controls.</summary>
public enum VehicleSpeedTargetType
{
    /// <summary>Speed over the ground.</summary>
    GroundSpeed,
    /// <summary>Speed through the air.</summary>
    Airspeed,
}

/// <summary>The confirmed outcome of a vehicle adjustment.</summary>
public enum VehicleAdjustmentStatus
{
    /// <summary>The vehicle accepted the command.</summary>
    CommandAccepted,
    /// <summary>The target was sent, but telemetry has not confirmed it.</summary>
    TargetSentButNotTelemetryConfirmed,
    /// <summary>Telemetry shows the vehicle applied the target.</summary>
    TelemetryConfirmed,
    /// <summary>The vehicle parameter shows the new value.</summary>
    ParameterConfirmed,
    /// <summary>The vehicle or the safety policy denied the adjustment.</summary>
    Denied,
    /// <summary>The vehicle does not support the adjustment.</summary>
    Unsupported,
    /// <summary>The adjustment failed.</summary>
    Failed,
    /// <summary>The vehicle is busy with another operation.</summary>
    Busy,
    /// <summary>The vehicle did not answer in time.</summary>
    Timeout
}

/// <summary>The result of a vehicle adjustment.</summary>
/// <param name="VehicleId">The vehicle the adjustment targeted.</param>
/// <param name="Status">The confirmed outcome.</param>
/// <param name="Message">A message the operator can read.</param>
/// <param name="PersistedValue">The value stored on the vehicle, when known.</param>
public sealed record VehicleAdjustmentResult(VehicleId VehicleId, VehicleAdjustmentStatus Status, string Message, float? PersistedValue = null);
