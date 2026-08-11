using System.Text.Json;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>In-memory planning polygon workspace using local metre geometry.</summary>
public sealed class PlanningPolygonService : IPlanningPolygonService
{
    private const int MaximumVertices = 20_000;
    private const int MaximumDocumentLength = 4 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    /// <inheritdoc />
    public event EventHandler? Changed;
    /// <inheritdoc />
    public PlanningPolygonSnapshot Snapshot { get; private set; } = PlanningPolygonSnapshot.Empty;

    /// <inheritdoc />
    public PlanningPolygonOperationResult Set(string name, IEnumerable<GeoPosition> vertices)
    {
        var supplied = vertices.ToArray();
        var normalized = supplied.Length > 1 && supplied[0] == supplied[^1] ? supplied[..^1] : supplied;
        if (normalized.Distinct().Count() != normalized.Length)
            return new(false, "Polygon contains duplicate vertices.");
        var error = Validate(normalized);
        if (error is not null)
            return new(false, error);
        Snapshot = new(new(string.IsNullOrWhiteSpace(name) ? "Planning polygon" : name.Trim(), normalized), Snapshot.Revision + 1);
        Changed?.Invoke(this, EventArgs.Empty);
        return new(true, $"Polygon contains {normalized.Length} vertices.", Snapshot.Polygon);
    }

    /// <inheritdoc />
    public void Clear()
    {
        Snapshot = new(null, Snapshot.Revision + 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public PlanningPolygonOperationResult FromMission(Mission mission) =>
        Set("Mission boundary", mission.Items.Select(PositionOf).Where(position => position is not null).Select(position => position!.Value));

    /// <inheritdoc />
    public PlanningPolygonOperationResult PreviewOffset(double distanceMeters)
    {
        if (Snapshot.Polygon is not { } polygon)
            return new(false, "Create a polygon first.");
        if (!double.IsFinite(distanceMeters) || Math.Abs(distanceMeters) < 0.01)
            return new(false, "Enter a non-zero finite offset distance.");

        var origin = Centroid(polygon.Vertices);
        var points = polygon.Vertices.Select(point => Project(point, origin)).ToArray();
        if (distanceMeters < 0)
        {
            var maximumInward = Enumerable.Range(0, points.Length)
                .Min(index => DistanceToLine(default, points[index], points[(index + 1) % points.Length]));
            if (Math.Abs(distanceMeters) >= maximumInward)
                return new(false, "Offset distance collapses the polygon.");
        }
        var orientation = SignedArea(points) >= 0 ? 1d : -1d;
        var result = new Point2[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            var previous = points[(i + points.Length - 1) % points.Length];
            var current = points[i];
            var next = points[(i + 1) % points.Length];
            var first = OffsetLine(previous, current, distanceMeters * orientation);
            var second = OffsetLine(current, next, distanceMeters * orientation);
            if (!TryIntersect(first, second, out result[i]))
                return new(false, "Offset collapsed at a parallel polygon corner.");
        }
        var originalArea = Math.Abs(SignedArea(points));
        var resultArea = Math.Abs(SignedArea(result));
        if (Math.Sign(SignedArea(result)) != Math.Sign(SignedArea(points)) ||
            (distanceMeters < 0 && resultArea >= originalArea) ||
            (distanceMeters > 0 && resultArea <= originalArea))
            return new(false, "Offset distance collapses the polygon.");
        var preview = new PlanningPolygon($"{polygon.Name} offset {distanceMeters:F1} m", result.Select(point => Unproject(point, origin)).ToArray());
        var error = Validate(preview.Vertices);
        return error is null ? new(true, "Offset preview is valid.", preview) : new(false, error);
    }

    /// <inheritdoc />
    public PlanningPolygonOperationResult ApplyPreview(PlanningPolygon preview) => Set(preview.Name, preview.Vertices);

    /// <inheritdoc />
    public PlanningPolygonArea? CalculateArea()
    {
        if (Snapshot.Polygon is not { } polygon)
            return null;
        var origin = Centroid(polygon.Vertices);
        return new(Math.Abs(SignedArea(polygon.Vertices.Select(point => Project(point, origin)).ToArray())));
    }

    /// <inheritdoc />
    public string Serialize(DateTimeOffset createdAt)
    {
        var polygon = Snapshot.Polygon ?? throw new InvalidOperationException("No polygon is available.");
        return JsonSerializer.Serialize(new PolygonDocument(1, polygon.Name, createdAt,
            polygon.Vertices.Select(point => new CoordinateDocument(point.LatitudeDegrees, point.LongitudeDegrees)).ToArray()), JsonOptions);
    }

    /// <inheritdoc />
    public PlanningPolygonOperationResult Deserialize(string content)
    {
        if (content.Length > MaximumDocumentLength)
            return new(false, "Polygon file exceeds the 4 MiB limit.");
        try
        {
            var document = JsonSerializer.Deserialize<PolygonDocument>(content, JsonOptions);
            if (document is null || document.SchemaVersion != 1 || document.Coordinates is null)
                return new(false, "Unsupported polygon document.");
            return Set(document.Name ?? "Planning polygon", document.Coordinates.Select(point => new GeoPosition(point.Latitude, point.Longitude)));
        }
        catch (JsonException exception)
        {
            return new(false, $"Invalid polygon JSON: {exception.Message}");
        }
    }

    private static string? Validate(IReadOnlyList<GeoPosition> points)
    {
        if (points.Count < 3) return "A polygon requires at least three unique vertices.";
        if (points.Count > MaximumVertices) return $"A polygon cannot exceed {MaximumVertices} vertices.";
        if (points.Any(point => !point.IsValid)) return "Polygon coordinates must be finite and valid.";
        var origin = Centroid(points);
        var projected = points.Select(point => Project(point, origin)).ToArray();
        if (Math.Abs(SignedArea(projected)) < 0.01) return "Polygon area is degenerate.";
        for (var i = 0; i < projected.Length; i++)
        for (var j = i + 1; j < projected.Length; j++)
        {
            if (j == i || j == i + 1 || (i == 0 && j == projected.Length - 1)) continue;
            if (SegmentsIntersect(projected[i], projected[(i + 1) % projected.Length], projected[j], projected[(j + 1) % projected.Length]))
                return "Polygon boundary self-intersects.";
        }
        return null;
    }

    private static GeoPosition? PositionOf(MissionItem item) => item switch
    {
        WaypointMissionItem x => x.Position, SplineWaypointMissionItem x => x.Position,
        LandMissionItem x => x.Position, LoiterMissionItem x => x.Position,
        TakeoffMissionItem x => x.Position, RoiLocationMissionItem x => x.Position, _ => null
    };

    private static GeoPosition Centroid(IReadOnlyList<GeoPosition> points)
    {
        var latitude = points.Average(point => point.LatitudeDegrees);
        var sin = points.Average(point => Math.Sin(point.LongitudeDegrees * Math.PI / 180));
        var cos = points.Average(point => Math.Cos(point.LongitudeDegrees * Math.PI / 180));
        return new(latitude, Math.Atan2(sin, cos) * 180 / Math.PI);
    }

    private static Point2 Project(GeoPosition point, GeoPosition origin)
    {
        const double radius = 6371008.8;
        var dLon = (point.LongitudeDegrees - origin.LongitudeDegrees) * Math.PI / 180;
        if (dLon > Math.PI) dLon -= 2 * Math.PI;
        if (dLon < -Math.PI) dLon += 2 * Math.PI;
        return new(radius * dLon * Math.Cos(origin.LatitudeDegrees * Math.PI / 180),
            radius * (point.LatitudeDegrees - origin.LatitudeDegrees) * Math.PI / 180);
    }

    private static GeoPosition Unproject(Point2 point, GeoPosition origin)
    {
        const double radius = 6371008.8;
        return new(origin.LatitudeDegrees + point.Y / radius * 180 / Math.PI,
            origin.LongitudeDegrees + point.X / (radius * Math.Cos(origin.LatitudeDegrees * Math.PI / 180)) * 180 / Math.PI);
    }

    private static double SignedArea(IReadOnlyList<Point2> points)
    {
        var twiceArea = 0d;
        for (var i = 0; i < points.Count; i++) twiceArea += Cross(points[i], points[(i + 1) % points.Count]);
        return twiceArea / 2;
    }

    private static Line OffsetLine(Point2 a, Point2 b, double distance)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y; var length = Math.Sqrt(dx * dx + dy * dy);
        var normal = new Point2(dy / length * distance, -dx / length * distance);
        return new(new(a.X + normal.X, a.Y + normal.Y), new(b.X + normal.X, b.Y + normal.Y));
    }

    private static bool TryIntersect(Line a, Line b, out Point2 point)
    {
        var r = new Point2(a.B.X - a.A.X, a.B.Y - a.A.Y); var s = new Point2(b.B.X - b.A.X, b.B.Y - b.A.Y);
        var denominator = Cross(r, s);
        if (Math.Abs(denominator) < 1e-9) { point = default; return false; }
        var q = new Point2(b.A.X - a.A.X, b.A.Y - a.A.Y);
        var t = Cross(q, s) / denominator;
        point = new(a.A.X + t * r.X, a.A.Y + t * r.Y); return true;
    }

    private static bool SegmentsIntersect(Point2 a, Point2 b, Point2 c, Point2 d)
    {
        static double Direction(Point2 p, Point2 q, Point2 r) => Cross(new(q.X - p.X, q.Y - p.Y), new(r.X - p.X, r.Y - p.Y));
        var d1 = Direction(a, b, c); var d2 = Direction(a, b, d); var d3 = Direction(c, d, a); var d4 = Direction(c, d, b);
        return d1 * d2 < 0 && d3 * d4 < 0;
    }

    private static double DistanceToLine(Point2 point, Point2 a, Point2 b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        return Math.Abs(dy * point.X - dx * point.Y + b.X * a.Y - b.Y * a.X) / Math.Sqrt(dx * dx + dy * dy);
    }

    private static double Cross(Point2 a, Point2 b) => a.X * b.Y - a.Y * b.X;
    private readonly record struct Point2(double X, double Y);
    private readonly record struct Line(Point2 A, Point2 B);
    private sealed record PolygonDocument(int SchemaVersion, string? Name, DateTimeOffset CreatedAt, CoordinateDocument[]? Coordinates);
    private sealed record CoordinateDocument(double Latitude, double Longitude);
}
