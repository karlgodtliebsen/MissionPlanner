using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Terrain availability for an elevation profile sample.</summary>
public enum TerrainProfileStatus { /// <summary>Terrain is available.</summary>
    Available, /// <summary>Terrain is unavailable and must be rendered as a gap.</summary>
    Unavailable }
/// <summary>Request for bounded route elevation sampling.</summary>
public sealed record MissionElevationProfileRequest(Mission Mission, GeoPosition? HomePosition, double? HomeAltitudeMslMeters,
    double SampleIntervalMeters = 50, int MaximumSamples = 2000);
/// <summary>One route/terrain profile sample.</summary>
public sealed record MissionElevationSample(double DistanceMeters, GeoPosition Position, double? TerrainElevationMeters,
    double PlannedAltitudeMeters, MissionAltitudeReference AltitudeReference, double? PlannedMslMeters, double? ClearanceMeters,
    ushort MissionSequence, int LegIndex, TerrainProfileStatus TerrainStatus);
/// <summary>One sampled mission leg.</summary>
public sealed record MissionElevationLeg(int Index, ushort StartSequence, ushort EndSequence, double StartDistanceMeters, double EndDistanceMeters);
/// <summary>Completed elevation profile.</summary>
public sealed record MissionElevationProfile(IReadOnlyList<MissionElevationSample> Samples, IReadOnlyList<MissionElevationLeg> Legs,
    double TotalDistanceMeters, int UnavailableSamples);
/// <summary>Samples mission navigation legs through the existing terrain subsystem.</summary>
public interface IMissionElevationProfileService
{
    /// <summary>Generates an immutable profile.</summary>
    Task<MissionElevationProfile> GenerateAsync(MissionElevationProfileRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Application boundary for the existing terrain subsystem.</summary>
public interface IMissionTerrainElevationProvider
{
    /// <summary>Gets MSL elevation, or <see langword="null"/> when terrain is unavailable.</summary>
    ValueTask<double?> GetElevationAsync(GeoPosition position, CancellationToken cancellationToken = default);
}

/// <summary>Default bounded mission elevation sampler.</summary>
public sealed class MissionElevationProfileService(IMissionTerrainElevationProvider terrain) : IMissionElevationProfileService
{
    /// <inheritdoc />
    public async Task<MissionElevationProfile> GenerateAsync(MissionElevationProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SampleIntervalMeters <= 0 || request.MaximumSamples is < 2 or > 10000) throw new ArgumentOutOfRangeException(nameof(request));
        var positioned = request.Mission.Items.Select(item => (Item: item, Position: Position(item), Altitude: Altitude(item))).Where(x => x.Position is not null && x.Altitude is not null).ToArray();
        if (positioned.Length < 2) return new([], [], 0, 0);
        var samples = new List<MissionElevationSample>(); var legs = new List<MissionElevationLeg>(); var cumulative = 0d;
        for (var index = 0; index + 1 < positioned.Length && samples.Count < request.MaximumSamples; index++)
        {
            var start = positioned[index]; var end = positioned[index + 1]; var distance = Distance(start.Position!.Value, end.Position!.Value);
            var count = Math.Max(1, (int)Math.Ceiling(distance / request.SampleIntervalMeters)); var legStart = cumulative;
            for (var step = index == 0 ? 0 : 1; step <= count && samples.Count < request.MaximumSamples; step++)
            {
                cancellationToken.ThrowIfCancellationRequested(); var fraction = step / (double)count;
                var position = Interpolate(start.Position.Value, end.Position.Value, fraction);
                var planned = Interpolate(start.Altitude!.Value.Meters, end.Altitude!.Value.Meters, fraction);
                var reference = end.Altitude.Value.Reference; var terrainElevation = await terrain.GetElevationAsync(position, cancellationToken);
                double? plannedMsl = reference switch { MissionAltitudeReference.MeanSeaLevel => planned, MissionAltitudeReference.Home when request.HomeAltitudeMslMeters is { } home => home + planned,
                    MissionAltitudeReference.Terrain when terrainElevation is { } ground => ground + planned, _ => null };
                samples.Add(new(legStart + distance * fraction, position, terrainElevation, planned, reference, plannedMsl,
                    plannedMsl is { } msl && terrainElevation is { } elevation ? msl - elevation : null, end.Item.Sequence, index,
                    terrainElevation is null ? TerrainProfileStatus.Unavailable : TerrainProfileStatus.Available));
            }
            cumulative += distance; legs.Add(new(index, start.Item.Sequence, end.Item.Sequence, legStart, cumulative));
        }
        return new(samples, legs, cumulative, samples.Count(sample => sample.TerrainStatus == TerrainProfileStatus.Unavailable));
    }
    private static GeoPosition? Position(MissionItem item) => item switch { WaypointMissionItem x => x.Position, SplineWaypointMissionItem x => x.Position, TakeoffMissionItem x => x.Position, LandMissionItem x => x.Position, LoiterMissionItem x => x.Position, _ => null };
    private static MissionAltitude? Altitude(MissionItem item) => item switch { WaypointMissionItem x => x.Altitude, SplineWaypointMissionItem x => x.Altitude, TakeoffMissionItem x => x.Altitude, LandMissionItem x => x.Altitude, LoiterMissionItem x => x.Altitude, _ => null };
    private static GeoPosition Interpolate(GeoPosition a, GeoPosition b, double value) => new(Interpolate(a.LatitudeDegrees, b.LatitudeDegrees, value), Interpolate(a.LongitudeDegrees, b.LongitudeDegrees, value));
    private static double Interpolate(double a, double b, double value) => a + (b - a) * value;
    private static double Distance(GeoPosition a, GeoPosition b) { var lat = (a.LatitudeDegrees + b.LatitudeDegrees) / 2 * Math.PI / 180; var north = (b.LatitudeDegrees-a.LatitudeDegrees)*111319.49; var east=(b.LongitudeDegrees-a.LongitudeDegrees)*111319.49*Math.Cos(lat); return Math.Sqrt(north*north+east*east); }
}
