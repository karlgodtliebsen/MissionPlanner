namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Readiness reported by the flight controller for the current session.</summary>
public enum VehicleArmingState
{
    /// <summary>Readiness has not yet been reported.</summary>
    Unknown,
    /// <summary>The disarmed vehicle reports an arming blocker.</summary>
    DisarmedNotReady,
    /// <summary>The disarmed vehicle reports successful pre-arm checks.</summary>
    DisarmedReady,
    /// <summary>The heartbeat reports the vehicle is armed.</summary>
    Armed
}

/// <summary>Retained arming feedback, independent of the bounded message history.</summary>
/// <param name="State">Current arming/readiness state.</param>
/// <param name="PreArmReason">Latest meaningful pre-arm blocker.</param>
/// <param name="LastArmFailure">Latest rejected arming attempt, retained until session reset.</param>
/// <param name="UpdatedAt">Reception time of the latest relevant observation.</param>
public sealed record VehicleArmingStatus(
    VehicleArmingState State,
    string? PreArmReason,
    string? LastArmFailure,
    DateTimeOffset? UpdatedAt)
{
    /// <summary>Gets an empty status for a new or disconnected session.</summary>
    public static VehicleArmingStatus Empty { get; } = new(VehicleArmingState.Unknown, null, null, null);
}
