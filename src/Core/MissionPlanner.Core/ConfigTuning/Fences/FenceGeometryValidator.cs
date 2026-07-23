using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.ConfigTuning.Fences;

/// <summary>Performs deterministic geometry, altitude, radius, and protocol-limit validation.</summary>
public sealed class FenceGeometryValidator : IFenceGeometryValidator
{
    /// <inheritdoc />
    public FenceValidationResult Validate(FencePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var issues = new List<FenceValidationIssue>();
        if (plan.ReturnPoint is { IsValid: false })
        {
            issues.Add(new FenceValidationIssue("return-position", "The fence return position is invalid."));
        }

        var duplicateIds = plan.Areas.GroupBy(area => area.Id).Where(group => group.Count() > 1).Select(group => group.Key);
        issues.AddRange(duplicateIds.Select(id => new FenceValidationIssue("duplicate-area", "Fence area identifiers must be unique.", id)));

        long protocolItems = plan.ReturnPoint is null ? 0 : 1;
        foreach (var area in plan.Areas)
        {
            switch (area.Kind)
            {
                case FenceAreaKind.PolygonInclusion:
                case FenceAreaKind.PolygonExclusion:
                    ValidatePolygon(area, issues);
                    protocolItems += area.Vertices.Count;
                    break;
                case FenceAreaKind.CircleInclusion:
                case FenceAreaKind.CircleExclusion:
                    ValidateCircle(area, issues);
                    protocolItems++;
                    break;
                default:
                    issues.Add(new FenceValidationIssue("area-kind", "The fence area kind is not supported.", area.Id));
                    break;
            }
        }

        if (protocolItems > ushort.MaxValue)
        {
            issues.Add(new FenceValidationIssue("protocol-limit", $"The fence requires {protocolItems} items; MAVLink permits at most {ushort.MaxValue}."));
        }

        return new FenceValidationResult(issues);
    }

    /// <inheritdoc />
    public FenceValidationResult ValidateParameters(IParameterEditSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var issues = new List<FenceValidationIssue>();
        AddFieldErrors(session, issues);

        var minimum = Pending(session, "FENCE_ALT_MIN");
        var maximum = Pending(session, "FENCE_ALT_MAX");
        var returnAltitude = Pending(session, "FENCE_RET_ALT");
        if (minimum is { } min && maximum is { } max && min > max)
        {
            issues.Add(new FenceValidationIssue("altitude-order", "FENCE_ALT_MIN cannot be greater than FENCE_ALT_MAX."));
        }

        if (returnAltitude is { } rtl && minimum is { } lower && rtl < lower)
        {
            issues.Add(new FenceValidationIssue("return-altitude", "FENCE_RET_ALT cannot be below FENCE_ALT_MIN."));
        }

        if (returnAltitude is { } returnValue && maximum is { } upper && returnValue > upper)
        {
            issues.Add(new FenceValidationIssue("return-altitude", "FENCE_RET_ALT cannot be above FENCE_ALT_MAX."));
        }

        if (Pending(session, "FENCE_RADIUS") is { } radius && radius <= 0)
        {
            issues.Add(new FenceValidationIssue("radius", "FENCE_RADIUS must be greater than zero."));
        }

        if (Pending(session, "FENCE_MARGIN") is { } margin && margin < 0)
        {
            issues.Add(new FenceValidationIssue("margin", "FENCE_MARGIN cannot be negative."));
        }

        return new FenceValidationResult(issues);
    }

    private static void ValidatePolygon(FenceArea area, ICollection<FenceValidationIssue> issues)
    {
        if (!area.IsClosed)
        {
            issues.Add(new FenceValidationIssue("polygon-open", "Finish the polygon before uploading it.", area.Id));
        }

        if (area.Vertices.Count < 3)
        {
            issues.Add(new FenceValidationIssue("polygon-vertices", "A fence polygon requires at least three vertices.", area.Id));
            return;
        }

        if (area.Vertices.Any(vertex => !vertex.IsValid))
        {
            issues.Add(new FenceValidationIssue("polygon-position", "A polygon contains an invalid latitude or longitude.", area.Id));
        }

        for (var index = 0; index < area.Vertices.Count; index++)
        {
            var next = (index + 1) % area.Vertices.Count;
            if (area.Vertices[index] == area.Vertices[next])
            {
                issues.Add(new FenceValidationIssue("polygon-duplicate", "Adjacent polygon vertices must be different.", area.Id));
                break;
            }
        }

        if (SelfIntersects(area.Vertices))
        {
            issues.Add(new FenceValidationIssue("polygon-intersection", "A fence polygon cannot intersect itself.", area.Id));
        }
    }

    private static void ValidateCircle(FenceArea area, ICollection<FenceValidationIssue> issues)
    {
        if (area.Center is not { IsValid: true })
        {
            issues.Add(new FenceValidationIssue("circle-position", "A circle requires a valid center position.", area.Id));
        }

        if (!double.IsFinite(area.RadiusMeters) || area.RadiusMeters <= 0 || area.RadiusMeters > float.MaxValue)
        {
            issues.Add(new FenceValidationIssue("circle-radius", "A circle radius must be a finite positive MAVLink value.", area.Id));
        }
    }

    private static bool SelfIntersects(IReadOnlyList<GeoPosition> vertices)
    {
        for (var first = 0; first < vertices.Count; first++)
        {
            var firstNext = (first + 1) % vertices.Count;
            for (var second = first + 1; second < vertices.Count; second++)
            {
                var secondNext = (second + 1) % vertices.Count;
                if (first == second || firstNext == second || secondNext == first)
                {
                    continue;
                }

                if (SegmentsIntersect(vertices[first], vertices[firstNext], vertices[second], vertices[secondNext]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(GeoPosition a, GeoPosition b, GeoPosition c, GeoPosition d)
    {
        const double epsilon = 1e-12;
        var o1 = Orientation(a, b, c);
        var o2 = Orientation(a, b, d);
        var o3 = Orientation(c, d, a);
        var o4 = Orientation(c, d, b);
        if (o1 * o2 < 0 && o3 * o4 < 0)
        {
            return true;
        }

        return Math.Abs(o1) <= epsilon && OnSegment(a, c, b) ||
            Math.Abs(o2) <= epsilon && OnSegment(a, d, b) ||
            Math.Abs(o3) <= epsilon && OnSegment(c, a, d) ||
            Math.Abs(o4) <= epsilon && OnSegment(c, b, d);
    }

    private static double Orientation(GeoPosition a, GeoPosition b, GeoPosition c) =>
        ((b.LongitudeDegrees - a.LongitudeDegrees) * (c.LatitudeDegrees - a.LatitudeDegrees)) -
        ((b.LatitudeDegrees - a.LatitudeDegrees) * (c.LongitudeDegrees - a.LongitudeDegrees));

    private static bool OnSegment(GeoPosition start, GeoPosition point, GeoPosition end) =>
        point.LatitudeDegrees >= Math.Min(start.LatitudeDegrees, end.LatitudeDegrees) &&
        point.LatitudeDegrees <= Math.Max(start.LatitudeDegrees, end.LatitudeDegrees) &&
        point.LongitudeDegrees >= Math.Min(start.LongitudeDegrees, end.LongitudeDegrees) &&
        point.LongitudeDegrees <= Math.Max(start.LongitudeDegrees, end.LongitudeDegrees);

    private static void AddFieldErrors(IParameterEditSession session, ICollection<FenceValidationIssue> issues)
    {
        foreach (var field in session.Fields.Where(field => field.Name.StartsWith("FENCE_", StringComparison.Ordinal) && !field.IsValid))
        {
            issues.Add(new FenceValidationIssue("parameter", $"{field.Name}: {field.ValidationError}"));
        }
    }

    private static double? Pending(IParameterEditSession session, string name) => session.GetField(name)?.PendingValue;
}
