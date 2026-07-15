namespace MissionPlanner.Core.Vehicles.Models;

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
    public static VehicleNavigationState Empty { get; } = new(null, null, null, null, null, null, null, null, null, null, null, null, null);
}
