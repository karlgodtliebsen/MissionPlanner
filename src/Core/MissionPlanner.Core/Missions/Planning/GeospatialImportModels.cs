using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Supported imported geometry kinds.</summary>
public enum GeospatialGeometryKind
{
    /// <summary>A single geographic position.</summary>
    Point,
    /// <summary>An ordered open path.</summary>
    LineString,
    /// <summary>A closed boundary.</summary>
    Polygon,
    /// <summary>A source feature containing multiple child geometries.</summary>
    MultiGeometry,
    /// <summary>A geometry the importer intentionally does not convert.</summary>
    Unsupported
}

/// <summary>A bounded geospatial source and optional same-base-name companion files.</summary>
/// <param name="FileName">Safe source file name.</param>
/// <param name="Content">Source bytes.</param>
/// <param name="Companions">Companion bytes keyed by lower-case extension.</param>
public sealed record GeospatialSource(string FileName, ReadOnlyMemory<byte> Content, IReadOnlyDictionary<string, ReadOnlyMemory<byte>>? Companions = null);

/// <summary>One UI-neutral imported feature.</summary>
/// <param name="Name">Feature name.</param>
/// <param name="Kind">Geometry kind.</param>
/// <param name="Positions">WGS84 geometry positions.</param>
/// <param name="Description">Optional description.</param>
/// <param name="AltitudeMeters">Optional per-feature altitude.</param>
public sealed record GeospatialFeature(string Name, GeospatialGeometryKind Kind, IReadOnlyList<GeoPosition> Positions,
    string? Description = null, double? AltitudeMeters = null);

/// <summary>Preview counts for deliberate overlay/mission/polygon import choices.</summary>
public sealed record GeospatialImportPreview(int Points, int LineStrings, int Polygons, int MissionCandidates, int Unsupported);

/// <summary>Result of bounded geospatial parsing.</summary>
public sealed record GeospatialImportResult(bool Succeeded, string Message, IReadOnlyList<GeospatialFeature> Features,
    GeospatialImportPreview Preview);

/// <summary>Parses supported geospatial files without renderer or UI dependencies.</summary>
public interface IGeospatialImportService
{
    /// <summary>Parses KML/KMZ or shapefile content into WGS84 features.</summary>
    GeospatialImportResult Import(GeospatialSource source);
}
