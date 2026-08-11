using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Input for geodesic circle mission generation.</summary>
public sealed record CircleWaypointRequest(GeoPosition Center, double RadiusMeters, int PointCount, bool Clockwise,
    double StartAngleDegrees, MissionAltitude StartAltitude, double? EndAltitudeMeters = null, bool Spline = false, bool RoiAtCenter = false);

/// <summary>Input for deterministic cross-platform stroke-text generation.</summary>
public sealed record TextWaypointRequest(string Text, GeoPosition Origin, double HeightMeters, double RotationDegrees,
    double LetterSpacing, MissionAltitude Altitude);

/// <summary>A generated route awaiting an explicit mission merge decision.</summary>
public sealed record AutoWaypointGenerationResult(bool Succeeded, string Message, IReadOnlyList<MissionItem> Items,
    IReadOnlyList<GeoPosition> PreviewPositions);

/// <summary>Generates typed mission candidates without mutating a mission.</summary>
public interface IAutoWaypointGenerator
{
    /// <summary>Generates normal or spline points around a geodesic circle.</summary>
    AutoWaypointGenerationResult GenerateCircle(CircleWaypointRequest request);
    /// <summary>Generates a waypoint route from embedded single-line glyphs.</summary>
    AutoWaypointGenerationResult GenerateText(TextWaypointRequest request);
}

/// <summary>Deterministic geodesic and embedded stroke-font generator.</summary>
public sealed class AutoWaypointGenerator : IAutoWaypointGenerator
{
    private const double EarthRadius = 6378137d;
    private const int MaxGeneratedPoints = 1000;
    private static readonly IReadOnlyDictionary<char, (double X, double Y)[]> Glyphs = new Dictionary<char, (double, double)[]>
    {
        ['A'] = [(0,0),(0.5,1),(1,0),(0.75,0.5),(0.25,0.5)], ['B'] = [(0,0),(0,1),(0.7,1),(1,0.75),(0.7,0.5),(0,0.5),(0.7,0.5),(1,0.25),(0.7,0),(0,0)],
        ['C'] = [(1,1),(0,1),(0,0),(1,0)], ['E'] = [(1,1),(0,1),(0,0),(1,0),(0,0.5),(0.75,0.5)], ['H'] = [(0,0),(0,1),(0,0.5),(1,0.5),(1,1),(1,0)],
        ['I'] = [(0,1),(1,1),(0.5,1),(0.5,0),(0,0),(1,0)], ['L'] = [(0,1),(0,0),(1,0)], ['M'] = [(0,0),(0,1),(0.5,0.45),(1,1),(1,0)],
        ['N'] = [(0,0),(0,1),(1,0),(1,1)], ['O'] = [(0,0),(0,1),(1,1),(1,0),(0,0)], ['P'] = [(0,0),(0,1),(1,1),(1,0.5),(0,0.5)],
        ['R'] = [(0,0),(0,1),(1,1),(1,0.5),(0,0.5),(1,0)], ['S'] = [(1,1),(0,1),(0,0.5),(1,0.5),(1,0),(0,0)], ['T'] = [(0,1),(1,1),(0.5,1),(0.5,0)],
        ['U'] = [(0,1),(0,0),(1,0),(1,1)], ['V'] = [(0,1),(0.5,0),(1,1)], ['W'] = [(0,1),(0.25,0),(0.5,0.5),(0.75,0),(1,1)], ['X'] = [(0,1),(1,0),(0.5,0.5),(1,1),(0,0)],
        ['Y'] = [(0,1),(0.5,0.5),(1,1),(0.5,0.5),(0.5,0)], ['Z'] = [(0,1),(1,1),(0,0),(1,0)], ['0'] = [(0,0),(0,1),(1,1),(1,0),(0,0)],
        ['1'] = [(0.5,0),(0.5,1),(0.25,0.75)], ['2'] = [(0,0.75),(0.25,1),(1,1),(1,0.5),(0,0),(1,0)], ['3'] = [(0,1),(1,1),(0.5,0.5),(1,0),(0,0)]
    };

    /// <inheritdoc />
    public AutoWaypointGenerationResult GenerateCircle(CircleWaypointRequest request)
    {
        if (!request.Center.IsValid || !double.IsFinite(request.RadiusMeters) || request.RadiusMeters <= 0 || request.PointCount is < 3 or > MaxGeneratedPoints)
            return Failure("Circle requires a valid center, positive radius, and 3-1000 points.");
        var positions = Enumerable.Range(0, request.PointCount).Select(index => Destination(request.Center, request.RadiusMeters,
            request.StartAngleDegrees + (request.Clockwise ? 1 : -1) * index * 360d / request.PointCount)).ToArray();
        var items = new List<MissionItem>();
        for (var index = 0; index < positions.Length; index++)
        {
            var end = request.EndAltitudeMeters ?? request.StartAltitude.Meters;
            var altitude = request.StartAltitude with { Meters = request.StartAltitude.Meters + (end - request.StartAltitude.Meters) * index / Math.Max(1, positions.Length - 1) };
            items.Add(request.Spline
                ? new SplineWaypointMissionItem(MissionItemId.New(), 0, positions[index], altitude, TimeSpan.Zero)
                : new WaypointMissionItem(MissionItemId.New(), 0, positions[index], altitude, TimeSpan.Zero));
        }
        if (request.RoiAtCenter) items.Add(new RoiLocationMissionItem(MissionItemId.New(), 0, request.Center, request.StartAltitude));
        return new(true, $"Generated {items.Count} circle items.", items, positions);
    }

    /// <inheritdoc />
    public AutoWaypointGenerationResult GenerateText(TextWaypointRequest request)
    {
        if (!request.Origin.IsValid || string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 32 || request.HeightMeters is < 1 or > 10000)
            return Failure("Text requires a valid origin, 1-10000 m height, and at most 32 characters.");
        var local = new List<(double X, double Y)>(); var offset = 0d;
        foreach (var character in request.Text.ToUpperInvariant())
        {
            if (character == ' ') { offset += 1 + request.LetterSpacing; continue; }
            if (!Glyphs.TryGetValue(character, out var glyph)) return Failure($"Character '{character}' is not supported by the stroke font.");
            local.AddRange(glyph.Select(point => (offset + point.X, point.Y))); offset += 1 + request.LetterSpacing;
        }
        if (local.Count > MaxGeneratedPoints) return Failure("Generated text exceeds the 1000-point mission limit.");
        var rotation = request.RotationDegrees * Math.PI / 180d;
        var positions = local.Select(point => Offset(request.Origin,
            request.HeightMeters * (point.X * Math.Cos(rotation) - point.Y * Math.Sin(rotation)),
            request.HeightMeters * (point.X * Math.Sin(rotation) + point.Y * Math.Cos(rotation)))).ToArray();
        var items = positions.Select(position => (MissionItem)new WaypointMissionItem(MissionItemId.New(), 0, position, request.Altitude, TimeSpan.Zero)).ToArray();
        return new(true, $"Generated {items.Length} stroke-text waypoints; travel between disconnected strokes is explicit.", items, positions);
    }

    private static GeoPosition Destination(GeoPosition origin, double distance, double bearingDegrees)
    {
        var latitude = origin.LatitudeDegrees * Math.PI / 180d; var longitude = origin.LongitudeDegrees * Math.PI / 180d;
        var bearing = bearingDegrees * Math.PI / 180d; var angular = distance / EarthRadius;
        var resultLatitude = Math.Asin(Math.Sin(latitude) * Math.Cos(angular) + Math.Cos(latitude) * Math.Sin(angular) * Math.Cos(bearing));
        var resultLongitude = longitude + Math.Atan2(Math.Sin(bearing) * Math.Sin(angular) * Math.Cos(latitude), Math.Cos(angular) - Math.Sin(latitude) * Math.Sin(resultLatitude));
        return new(resultLatitude * 180d / Math.PI, ((resultLongitude * 180d / Math.PI + 540d) % 360d) - 180d);
    }
    private static GeoPosition Offset(GeoPosition origin, double east, double north) => Destination(origin, Math.Sqrt(east * east + north * north), Math.Atan2(east, north) * 180d / Math.PI);
    private static AutoWaypointGenerationResult Failure(string message) => new(false, message, [], []);
}
