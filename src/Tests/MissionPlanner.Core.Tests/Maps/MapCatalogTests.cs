using FluentAssertions;
using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Core.Tests.Maps;

public sealed class MapCatalogTests
{
    [Fact]
    public void BuiltInCatalog_IsValidAndContainsCurrentAndFutureSources()
    {
        var catalog = BuiltInMapCatalog.Load();
        MapCatalogValidator.Validate(catalog).Should().BeEmpty();
        catalog.Sources.Should().Contain(source => source.Id == "osm-standard" && source.IsEnabledByDefault);
        catalog.Sources.Should().Contain(source => source.Id == "esri-world-topo" && source.IsEnabledByDefault);
        catalog.Sources.Should().Contain(source => source.Id == "no-map" && source.IsEnabledByDefault);
        catalog.Sources.Should().Contain(source => source.Id == "raster-mbtiles-template" && source.IsFutureCandidate && !source.IsEnabledByDefault);
        catalog.Sources.Should().Contain(source => source.Id == "protomaps-pmtiles" && source.IsFutureCandidate && !source.IsEnabledByDefault);
    }

    [Fact]
    public void Serialize_IsDeterministicAndRoundTrips()
    {
        var first = MapCatalogSerializer.Serialize(BuiltInMapCatalog.Load());
        var second = MapCatalogSerializer.Serialize(MapCatalogSerializer.Deserialize(first));
        second.Should().Be(first);
    }

    [Fact]
    public void Validate_ReportsDuplicateIdentifiers()
    {
        var catalog = BuiltInMapCatalog.Load();
        var invalid = catalog with { Providers = [catalog.Providers[0], catalog.Providers[0]] };
        MapCatalogValidator.Validate(invalid).Should().Contain(issue => issue.Path.Contains("providers/", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReportsBrokenCrossReferences()
    {
        var catalog = BuiltInMapCatalog.Load();
        var invalidSource = catalog.Sources[0] with { ProductId = "missing", PolicyId = "missing", AttributionIds = ["missing"] };
        var issues = MapCatalogValidator.Validate(catalog with { Sources = [invalidSource] });
        issues.Should().Contain(issue => issue.Path.EndsWith("productId", StringComparison.Ordinal));
        issues.Should().Contain(issue => issue.Path.EndsWith("policyId", StringComparison.Ordinal));
        issues.Should().Contain(issue => issue.Path.EndsWith("attributionIds", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ReportsImpossibleArchiveCombination()
    {
        var catalog = BuiltInMapCatalog.Load();
        var invalidSource = catalog.Sources.Single(source => source.Id == "osm-standard") with { ArchiveFormat = MapArchiveFormat.MbTiles };
        MapCatalogValidator.Validate(catalog with { Sources = [invalidSource] })
            .Should().Contain(issue => issue.Path.EndsWith("archiveFormat", StringComparison.Ordinal));
    }
}
