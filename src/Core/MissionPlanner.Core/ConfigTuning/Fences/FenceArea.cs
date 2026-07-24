using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.ConfigTuning.Fences;

/// <summary>Represents one independently editable fence area.</summary>
/// <param name="Id">The stable local area identifier.</param>
/// <param name="Kind">The geometry and inclusion kind.</param>
/// <param name="Vertices">The polygon vertices; closure is implicit and the first point is not repeated.</param>
/// <param name="Center">The circle center.</param>
/// <param name="RadiusMeters">The circle radius in meters.</param>
/// <param name="IsClosed">Whether polygon editing has been explicitly completed.</param>
public sealed record FenceArea(
    Guid Id,
    FenceAreaKind Kind,
    IReadOnlyList<GeoPosition> Vertices,
    GeoPosition? Center,
    double RadiusMeters,
    bool IsClosed)
{
    /// <summary>Creates an editable polygon area.</summary>
    /// <param name="kind">The inclusion or exclusion polygon kind.</param>
    /// <param name="vertices">The initial vertices.</param>
    /// <param name="isClosed">Whether polygon editing is complete.</param>
    /// <returns>The polygon area.</returns>
    public static FenceArea Polygon(FenceAreaKind kind, IReadOnlyList<GeoPosition> vertices, bool isClosed = false)
    {
        return new FenceArea(Guid.NewGuid(), kind, vertices.ToArray(), null, 0, isClosed);
    }

    /// <summary>Creates a circular area.</summary>
    /// <param name="kind">The inclusion or exclusion circle kind.</param>
    /// <param name="center">The circle center.</param>
    /// <param name="radiusMeters">The radius in meters.</param>
    /// <returns>The circular area.</returns>
    public static FenceArea Circle(FenceAreaKind kind, GeoPosition center, double radiusMeters)
    {
        return new FenceArea(Guid.NewGuid(), kind, [], center, radiusMeters, true);
    }
}
