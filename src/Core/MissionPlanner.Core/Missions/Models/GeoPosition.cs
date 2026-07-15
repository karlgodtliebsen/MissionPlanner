namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Represents a geographic position with latitude and longitude in degrees.  
/// </summary>
/// <param name="LatitudeDegrees"></param>
/// <param name="LongitudeDegrees"></param>
public readonly record struct GeoPosition(double LatitudeDegrees, double LongitudeDegrees)
{
    /// <summary>
    /// Indicates whether the geographic position is valid. A valid position has a latitude between -90 and 90 degrees and a longitude between -180 and 180 degrees.
    /// </summary>
    public bool IsValid => LatitudeDegrees is >= -90 and <= 90 && LongitudeDegrees is >= -180 and <= 180;

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Lat: {LatitudeDegrees}, Lon: {LongitudeDegrees}";
    }
}
