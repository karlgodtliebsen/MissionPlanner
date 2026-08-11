using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.Core.Tests;

public sealed class GeospatialImportServiceTests
{
    private readonly GeospatialImportService service = new();

    [Fact]
    public void Import_Kml_DistinguishesSupportedGeometry()
    {
        const string kml = """
            <kml xmlns="http://www.opengis.net/kml/2.2"><Document>
              <Placemark><name>P</name><Point><coordinates>10,56,12</coordinates></Point></Placemark>
              <Placemark><name>L</name><LineString><coordinates>10,56 10.1,56.1</coordinates></LineString></Placemark>
              <Placemark><name>A</name><Polygon><outerBoundaryIs><LinearRing><coordinates>10,56 10.1,56 10.1,56.1 10,56</coordinates></LinearRing></outerBoundaryIs></Polygon></Placemark>
            </Document></kml>
            """;

        var result = service.Import(new("sample.kml", Encoding.UTF8.GetBytes(kml)));

        result.Succeeded.Should().BeTrue();
        result.Preview.Should().Be(new GeospatialImportPreview(1, 1, 1, 3, 0));
        result.Features[0].Positions[0].LatitudeDegrees.Should().Be(56);
        result.Features[0].Positions[0].LongitudeDegrees.Should().Be(10);
    }

    [Fact]
    public void Import_Kml_RejectsDocumentTypes()
    {
        var bytes = Encoding.UTF8.GetBytes("<!DOCTYPE kml [<!ENTITY x SYSTEM 'file:///secret'>]><kml xmlns='http://www.opengis.net/kml/2.2'><Placemark><name>&x;</name></Placemark></kml>");
        service.Import(new("unsafe.kml", bytes)).Succeeded.Should().BeFalse();
    }

    [Fact]
    public void Import_Kmz_ReadsOneBoundedKmlEntry()
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
        using (var writer = new StreamWriter(archive.CreateEntry("doc.kml").Open()))
            writer.Write("<kml xmlns='http://www.opengis.net/kml/2.2'><Placemark><Point><coordinates>10,56</coordinates></Point></Placemark></kml>");

        service.Import(new("sample.kmz", memory.ToArray())).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Import_Shapefile_RequiresProjectionAndPreservesCoordinateOrder()
    {
        var shp = CreatePointShapefile(10, 56);
        service.Import(new("point.shp", shp)).Message.Should().Contain(".prj");
        var result = service.Import(new("point.shp", shp, new Dictionary<string, ReadOnlyMemory<byte>>
        {
            [".prj"] = Encoding.UTF8.GetBytes("GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\"]]")
        }));
        result.Succeeded.Should().BeTrue();
        result.Features.Single().Positions.Single().Should().Be(new global::MissionPlanner.Core.Missions.Models.GeoPosition(56, 10));
    }

    [Fact]
    public void Import_Shapefile_TransformsWebMercator()
    {
        var result = service.Import(new("point.shp", CreatePointShapefile(1113194.9079, 7558415.6561),
            new Dictionary<string, ReadOnlyMemory<byte>> { [".prj"] = Encoding.UTF8.GetBytes("PROJCS[\"WGS_1984_Web_Mercator\",GEOGCS[\"WGS 84\"],PROJECTION[\"Mercator\"]]") }));
        result.Succeeded.Should().BeTrue();
        result.Features.Single().Positions.Single().LatitudeDegrees.Should().BeApproximately(56, .001);
        result.Features.Single().Positions.Single().LongitudeDegrees.Should().BeApproximately(10, .001);
    }

    private static byte[] CreatePointShapefile(double x, double y)
    {
        var bytes = new byte[128];
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(0, 4), 9994);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(24, 4), bytes.Length / 2);
        BitConverter.GetBytes(1000).CopyTo(bytes, 28);
        BitConverter.GetBytes(1).CopyTo(bytes, 32);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(100, 4), 1);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(104, 4), 10);
        BitConverter.GetBytes(1).CopyTo(bytes, 108);
        BitConverter.GetBytes(x).CopyTo(bytes, 112);
        BitConverter.GetBytes(y).CopyTo(bytes, 120);
        return bytes;
    }
}
