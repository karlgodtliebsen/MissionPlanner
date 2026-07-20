namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleNavigationState.
/// </summary>
/// <param name="DesiredRollDegrees">The DesiredRollDegrees value.</param>
/// <param name="DesiredPitchDegrees">The DesiredPitchDegrees value.</param>
/// <param name="NavigationBearingDegrees">The NavigationBearingDegrees value.</param>
/// <param name="TargetBearingDegrees">The TargetBearingDegrees value.</param>
/// <param name="WaypointDistanceMeters">The WaypointDistanceMeters value.</param>
/// <param name="AltitudeErrorMeters">The AltitudeErrorMeters value.</param>
/// <param name="AirspeedErrorMetersPerSecond">The AirspeedErrorMetersPerSecond value.</param>
/// <param name="CrossTrackErrorMeters">The CrossTrackErrorMeters value.</param>
/// <param name="CurrentMissionSequence">The CurrentMissionSequence value.</param>
/// <param name="MissionItemCount">The MissionItemCount value.</param>
/// <param name="MissionState">The MissionState value.</param>
/// <param name="MissionMode">The MissionMode value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
public sealed record VehicleNavigationState(
    double? DesiredRollDegrees,
    double? DesiredPitchDegrees,
    double? NavigationBearingDegrees,
    double? TargetBearingDegrees,
    double? WaypointDistanceMeters,
    double? AltitudeErrorMeters,
    double? AirspeedErrorMetersPerSecond,
    double? CrossTrackErrorMeters,
    ushort? CurrentMissionSequence,
    ushort? MissionItemCount,
    byte? MissionState,
    byte? MissionMode,
    DateTimeOffset? ObservedAt)
{
    /// <summary>
    /// Provides the public API for Empty.
    /// </summary>
    public static VehicleNavigationState Empty { get; } = new(null, null, null, null, null, null, null, null, null, null, null, null, null);
}
