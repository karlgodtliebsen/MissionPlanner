using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Grid survey input over a validated planning polygon.</summary>
public sealed record GridSurveyRequest(PlanningPolygon Area, double AngleDegrees, double LineSpacingMeters,
    double OvershootMeters, MissionAltitude Altitude, bool CrossGrid = false);
/// <summary>Concentric circle survey input.</summary>
public sealed record CircleSurveyRequest(GeoPosition Center, double InnerRadiusMeters, double OuterRadiusMeters,
    double RadialSpacingMeters, int PointsPerRing, bool Clockwise, MissionAltitude Altitude);
/// <summary>One ordered survey flight leg.</summary>
public sealed record SurveyLeg(GeoPosition Start, GeoPosition End);
/// <summary>Survey preview statistics.</summary>
public sealed record SurveyStatistics(double DistanceMeters, int PointCount, int LineCount, double AreaSquareMeters);
/// <summary>Generated survey candidates and preview.</summary>
public sealed record SurveyMissionResult(bool Succeeded, string Message, IReadOnlyList<MissionItem> Items,
    IReadOnlyList<GeoPosition> Preview, IReadOnlyList<SurveyLeg> Legs, SurveyStatistics? Statistics);
/// <summary>Generates platform-neutral survey routes.</summary>
public interface ISurveyMissionGenerator
{
    /// <summary>Creates clipped, deterministically ordered grid legs.</summary>
    SurveyMissionResult GenerateGrid(GridSurveyRequest request);
    /// <summary>Creates concentric survey rings.</summary>
    SurveyMissionResult GenerateCircle(CircleSurveyRequest request);
}

/// <summary>Local-tangent-plane grid and concentric survey generator.</summary>
public sealed class SurveyMissionGenerator(IAutoWaypointGenerator circles) : ISurveyMissionGenerator
{
    /// <inheritdoc />
    public SurveyMissionResult GenerateGrid(GridSurveyRequest request)
    {
        if (request.LineSpacingMeters < 1 || request.OvershootMeters < 0 || request.Area.Vertices.Count < 3)
            return Failure("Grid requires a valid polygon, line spacing of at least 1 m, and non-negative overshoot.");
        var routes = GenerateGridPass(request, request.AngleDegrees).ToList();
        if (request.CrossGrid) routes.AddRange(GenerateGridPass(request, request.AngleDegrees + 90));
        var preview = routes.SelectMany((leg, index) => index % 2 == 0 ? new[] { leg.Start, leg.End } : new[] { leg.End, leg.Start }).ToArray();
        if (preview.Length is 0 or > MissionPlanningLimits.MaximumSurveyPoints) return Failure("Survey is empty or exceeds the 4000-point limit.");
        var items = preview.Select(position => (MissionItem)new WaypointMissionItem(MissionItemId.New(), 0, position, request.Altitude, TimeSpan.Zero)).ToArray();
        var distance = preview.Zip(preview.Skip(1), Distance).Sum();
        var area = PolygonArea(request.Area.Vertices);
        return new(true, $"Generated {routes.Count} grid legs and {items.Length} waypoints.", items, preview, routes,
            new(distance, items.Length, routes.Count, area));
    }
    /// <inheritdoc />
    public SurveyMissionResult GenerateCircle(CircleSurveyRequest request)
    {
        if (request.InnerRadiusMeters <= 0 || request.OuterRadiusMeters < request.InnerRadiusMeters || request.RadialSpacingMeters <= 0 || request.PointsPerRing < 3)
            return Failure("Circle survey radii, spacing, and point count are invalid.");
        var all = new List<MissionItem>(); var preview = new List<GeoPosition>(); var legs = new List<SurveyLeg>();
        for (var radius = request.InnerRadiusMeters; radius <= request.OuterRadiusMeters + .001; radius += request.RadialSpacingMeters)
        {
            var ring = circles.GenerateCircle(new(request.Center, radius, request.PointsPerRing, request.Clockwise, 0, request.Altitude));
            if (!ring.Succeeded) return Failure(ring.Message);
            all.AddRange(ring.Items); preview.AddRange(ring.PreviewPositions);
            legs.AddRange(ring.PreviewPositions.Zip(ring.PreviewPositions.Skip(1).Append(ring.PreviewPositions[0]), (a, b) => new SurveyLeg(a, b)));
        }
        if (all.Count > MissionPlanningLimits.MaximumSurveyPoints) return Failure("Circle survey exceeds the 4000-point limit.");
        var distance = legs.Sum(leg => Distance(leg.Start, leg.End));
        return new(true, $"Generated {legs.Count / request.PointsPerRing} survey rings.", all, preview, legs,
            new(distance, all.Count, legs.Count, Math.PI * request.OuterRadiusMeters * request.OuterRadiusMeters));
    }

    private static IEnumerable<SurveyLeg> GenerateGridPass(GridSurveyRequest request, double angleDegrees)
    {
        var origin = request.Area.Vertices[0]; var angle = angleDegrees * Math.PI / 180d;
        var polygon = request.Area.Vertices.Select(point => ToLocal(origin, point, angle)).ToArray();
        var minY = polygon.Min(point => point.Y); var maxY = polygon.Max(point => point.Y);
        for (var y = minY; y <= maxY + 1e-6; y += request.LineSpacingMeters)
        {
            var intersections = new List<double>();
            for (var index = 0; index < polygon.Length; index++)
            {
                var a = polygon[index]; var b = polygon[(index + 1) % polygon.Length];
                if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y)) intersections.Add(a.X + (y - a.Y) * (b.X - a.X) / (b.Y - a.Y));
            }
            intersections.Sort();
            for (var index = 0; index + 1 < intersections.Count; index += 2)
            {
                var left = intersections[index] - request.OvershootMeters; var right = intersections[index + 1] + request.OvershootMeters;
                if (right - left > .01) yield return new(FromLocal(origin, left, y, angle), FromLocal(origin, right, y, angle));
            }
        }
    }
    private static (double X, double Y) ToLocal(GeoPosition origin, GeoPosition point, double angle)
    {
        const double radius = 6378137d; var east = (point.LongitudeDegrees - origin.LongitudeDegrees) * Math.PI / 180d * radius * Math.Cos(origin.LatitudeDegrees * Math.PI / 180d);
        var north = (point.LatitudeDegrees - origin.LatitudeDegrees) * Math.PI / 180d * radius;
        return (east * Math.Cos(angle) + north * Math.Sin(angle), -east * Math.Sin(angle) + north * Math.Cos(angle));
    }
    private static GeoPosition FromLocal(GeoPosition origin, double x, double y, double angle)
    {
        const double radius = 6378137d; var east = x * Math.Cos(angle) - y * Math.Sin(angle); var north = x * Math.Sin(angle) + y * Math.Cos(angle);
        return new(origin.LatitudeDegrees + north / radius * 180d / Math.PI, origin.LongitudeDegrees + east / (radius * Math.Cos(origin.LatitudeDegrees * Math.PI / 180d)) * 180d / Math.PI);
    }
    private static double Distance(GeoPosition a, GeoPosition b)
    {
        var lat = (a.LatitudeDegrees + b.LatitudeDegrees) / 2d * Math.PI / 180d; var north = (b.LatitudeDegrees - a.LatitudeDegrees) * 111319.49;
        var east = (b.LongitudeDegrees - a.LongitudeDegrees) * 111319.49 * Math.Cos(lat); return Math.Sqrt(north * north + east * east);
    }
    private static double PolygonArea(IReadOnlyList<GeoPosition> points)
    { var origin = points[0]; var local = points.Select(point => ToLocal(origin, point, 0)).ToArray(); return Math.Abs(local.Select((point, i) => point.X * local[(i + 1) % local.Length].Y - local[(i + 1) % local.Length].X * point.Y).Sum()) / 2d; }
    private static SurveyMissionResult Failure(string message) => new(false, message, [], [], [], null);
}
