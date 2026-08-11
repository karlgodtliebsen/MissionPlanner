using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Bounded built-in KML/KMZ and common-CRS shapefile importer.</summary>
public sealed class GeospatialImportService : IGeospatialImportService
{
    /// <inheritdoc />
    public GeospatialImportResult Import(GeospatialSource source)
    {
        if (source.Content.Length > MissionPlanningLimits.MaximumImportedFileBytes) return Failure("File exceeds the 16 MiB input limit.");
        try
        {
            var extension = Path.GetExtension(source.FileName).ToLowerInvariant();
            var features = extension switch
            {
                ".kml" => ReadKml(source.Content),
                ".kmz" => ReadKmz(source.Content),
                ".shp" => ReadShapefile(source),
                _ => throw new InvalidDataException("Select a .kml, .kmz, or .shp file.")
            };
            if (features.Count > MissionPlanningLimits.MaximumGeospatialFeatures || features.Sum(feature => feature.Positions.Count) > MissionPlanningLimits.MaximumGeospatialVertices)
                return Failure("Imported content exceeds feature or vertex limits.");
            var preview = new GeospatialImportPreview(features.Count(x => x.Kind == GeospatialGeometryKind.Point),
                features.Count(x => x.Kind == GeospatialGeometryKind.LineString), features.Count(x => x.Kind == GeospatialGeometryKind.Polygon),
                features.Where(x => x.Kind is GeospatialGeometryKind.Point or GeospatialGeometryKind.LineString).Sum(x => x.Positions.Count),
                features.Count(x => x.Kind == GeospatialGeometryKind.Unsupported));
            return new(true, $"Imported {features.Count} features.", features, preview);
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or IOException)
        {
            return Failure(exception.Message);
        }
    }

    private static List<GeospatialFeature> ReadKml(ReadOnlyMemory<byte> content)
    {
        using var stream = new MemoryStream(content.ToArray(), false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MissionPlanningLimits.MaximumExpandedGeospatialBytes });
        var document = XDocument.Load(reader, LoadOptions.None);
        XNamespace ns = "http://www.opengis.net/kml/2.2";
        var features = new List<GeospatialFeature>();
        foreach (var placemark in document.Descendants(ns + "Placemark"))
        {
            var name = (string?)placemark.Element(ns + "name") ?? "KML feature";
            var description = ((string?)placemark.Element(ns + "description"))?[..Math.Min(((string?)placemark.Element(ns + "description"))?.Length ?? 0, 4096)];
            foreach (var geometry in placemark.Descendants().Where(node => node.Name == ns + "Point" || node.Name == ns + "LineString" || node.Name == ns + "Polygon"))
            {
                var kind = geometry.Name.LocalName switch { "Point" => GeospatialGeometryKind.Point, "LineString" => GeospatialGeometryKind.LineString, _ => GeospatialGeometryKind.Polygon };
                var coordinates = geometry.Descendants(ns + "coordinates").SelectMany(node => ParseCoordinates(node.Value)).ToArray();
                if (coordinates.Length > 0) features.Add(new(name, kind, coordinates, description));
            }
        }
        return features;
    }

    private static List<GeospatialFeature> ReadKmz(ReadOnlyMemory<byte> content)
    {
        using var archive = new ZipArchive(new MemoryStream(content.ToArray(), false), ZipArchiveMode.Read);
        var entries = archive.Entries.Where(entry => entry.FullName.EndsWith(".kml", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (entries.Length != 1 || entries[0].Length > MissionPlanningLimits.MaximumExpandedGeospatialBytes || entries[0].FullName.Contains("..", StringComparison.Ordinal))
            throw new InvalidDataException("KMZ must contain one bounded KML document without path traversal.");
        using var stream = entries[0].Open(); using var memory = new MemoryStream(); stream.CopyTo(memory);
        return ReadKml(memory.ToArray());
    }

    private static IEnumerable<GeoPosition> ParseCoordinates(string text)
    {
        foreach (var tuple in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var values = tuple.Split(',');
            if (values.Length >= 2 && double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
                && double.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude))
            {
                var position = new GeoPosition(latitude, longitude);
                if (position.IsValid) yield return position;
            }
        }
    }

    private static List<GeospatialFeature> ReadShapefile(GeospatialSource source)
    {
        if (source.Companions?.TryGetValue(".prj", out var prj) != true)
            throw new InvalidDataException("Shapefile CRS is missing; provide a .prj companion file.");
        var projection = System.Text.Encoding.UTF8.GetString(prj.Span);
        var transform = CreateCoordinateTransform(projection);
        using var reader = new BinaryReader(new MemoryStream(source.Content.ToArray(), false));
        if (ReadBigEndianInt32(reader) != 9994 || reader.BaseStream.Length < 100) throw new InvalidDataException("Invalid shapefile header.");
        reader.BaseStream.Position = 100;
        var features = new List<GeospatialFeature>();
        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
        {
            _ = ReadBigEndianInt32(reader); var bytes = checked(ReadBigEndianInt32(reader) * 2);
            var end = reader.BaseStream.Position + bytes; if (end > reader.BaseStream.Length) throw new InvalidDataException("Truncated shapefile record.");
            var type = reader.ReadInt32();
            if (type == 1)
            {
                var x = reader.ReadDouble(); var y = reader.ReadDouble();
                features.Add(new("SHP point", GeospatialGeometryKind.Point, [transform(x, y)]));
            }
            else if (type is 3 or 5)
            {
                reader.BaseStream.Position += 32; var partCount = reader.ReadInt32(); var pointCount = reader.ReadInt32();
                var parts = Enumerable.Range(0, partCount).Select(_ => reader.ReadInt32()).Append(pointCount).ToArray();
                var points = Enumerable.Range(0, pointCount).Select(_ => { var x = reader.ReadDouble(); var y = reader.ReadDouble(); return transform(x, y); }).ToArray();
                for (var i = 0; i < partCount; i++) features.Add(new("SHP feature", type == 5 ? GeospatialGeometryKind.Polygon : GeospatialGeometryKind.LineString, points[parts[i]..parts[i + 1]]));
            }
            reader.BaseStream.Position = end;
        }
        return features;
    }

    private static Func<double, double, GeoPosition> CreateCoordinateTransform(string projection)
    {
        if (projection.Contains("WGS_1984", StringComparison.OrdinalIgnoreCase) || projection.Contains("WGS 84", StringComparison.OrdinalIgnoreCase))
        {
            if (!projection.Contains("PROJCS", StringComparison.OrdinalIgnoreCase))
                return static (x, y) => CheckedPosition(y, x);
            if (projection.Contains("Mercator", StringComparison.OrdinalIgnoreCase))
                return static (x, y) => WebMercatorToWgs84(x, y);
            var zoneMatch = Regex.Match(projection, @"UTM[^0-9]*(?<zone>[0-9]{1,2})(?<hemisphere>[NS])?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (zoneMatch.Success && int.TryParse(zoneMatch.Groups["zone"].Value, out var zone) && zone is >= 1 and <= 60)
            {
                var north = !string.Equals(zoneMatch.Groups["hemisphere"].Value, "S", StringComparison.OrdinalIgnoreCase)
                    && !projection.Contains("South", StringComparison.OrdinalIgnoreCase);
                return (x, y) => UtmToWgs84(x, y, zone, north);
            }
        }
        throw new InvalidDataException("The shapefile CRS is unknown. Export as WGS84, Web Mercator, or WGS84 UTM and include its .prj file.");
    }

    private static GeoPosition WebMercatorToWgs84(double x, double y)
    {
        const double radius = 6378137d;
        var longitude = x / radius * 180d / Math.PI;
        var latitude = (2d * Math.Atan(Math.Exp(y / radius)) - Math.PI / 2d) * 180d / Math.PI;
        return CheckedPosition(latitude, longitude);
    }

    private static GeoPosition UtmToWgs84(double easting, double northing, int zone, bool north)
    {
        const double a = 6378137d, eccentricity = 0.0818191908426215d, scale = 0.9996d;
        var x = easting - 500000d;
        var y = north ? northing : northing - 10000000d;
        var e1 = (1d - Math.Sqrt(1d - eccentricity * eccentricity)) / (1d + Math.Sqrt(1d - eccentricity * eccentricity));
        var mu = y / (a * scale * (1d - eccentricity * eccentricity / 4d - 3d * Math.Pow(eccentricity, 4) / 64d - 5d * Math.Pow(eccentricity, 6) / 256d));
        var phi1 = mu + (3d * e1 / 2d - 27d * Math.Pow(e1, 3) / 32d) * Math.Sin(2d * mu)
            + (21d * e1 * e1 / 16d - 55d * Math.Pow(e1, 4) / 32d) * Math.Sin(4d * mu)
            + 151d * Math.Pow(e1, 3) / 96d * Math.Sin(6d * mu);
        var ePrimeSquared = eccentricity * eccentricity / (1d - eccentricity * eccentricity);
        var n1 = a / Math.Sqrt(1d - eccentricity * eccentricity * Math.Sin(phi1) * Math.Sin(phi1));
        var t1 = Math.Tan(phi1) * Math.Tan(phi1); var c1 = ePrimeSquared * Math.Cos(phi1) * Math.Cos(phi1);
        var r1 = a * (1d - eccentricity * eccentricity) / Math.Pow(1d - eccentricity * eccentricity * Math.Sin(phi1) * Math.Sin(phi1), 1.5d);
        var d = x / (n1 * scale);
        var latitude = phi1 - n1 * Math.Tan(phi1) / r1 * (d * d / 2d - (5d + 3d * t1 + 10d * c1 - 4d * c1 * c1 - 9d * ePrimeSquared) * Math.Pow(d, 4) / 24d
            + (61d + 90d * t1 + 298d * c1 + 45d * t1 * t1 - 252d * ePrimeSquared - 3d * c1 * c1) * Math.Pow(d, 6) / 720d);
        var longitudeOrigin = (zone - 1d) * 6d - 180d + 3d;
        var longitude = (d - (1d + 2d * t1 + c1) * Math.Pow(d, 3) / 6d
            + (5d - 2d * c1 + 28d * t1 - 3d * c1 * c1 + 8d * ePrimeSquared + 24d * t1 * t1) * Math.Pow(d, 5) / 120d) / Math.Cos(phi1);
        return CheckedPosition(latitude * 180d / Math.PI, longitudeOrigin + longitude * 180d / Math.PI);
    }

    private static GeoPosition CheckedPosition(double latitude, double longitude)
    {
        var position = new GeoPosition(latitude, longitude);
        return position.IsValid ? position : throw new InvalidDataException("A transformed shapefile coordinate is outside WGS84 bounds.");
    }

    private static int ReadBigEndianInt32(BinaryReader reader) => System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(reader.ReadBytes(4));
    private static GeospatialImportResult Failure(string message) => new(false, message, [], new(0, 0, 0, 0, 0));
}
