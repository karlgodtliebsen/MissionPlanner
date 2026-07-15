namespace MissionPlanner.Core.Missions;

public readonly record struct GeoPosition(double LatitudeDegrees, double LongitudeDegrees)
{
    public bool IsValid => LatitudeDegrees is >= -90 and <= 90 && LongitudeDegrees is >= -180 and <= 180;
}
