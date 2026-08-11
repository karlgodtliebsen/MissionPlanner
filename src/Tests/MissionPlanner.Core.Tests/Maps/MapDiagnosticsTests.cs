using FluentAssertions;
using MissionPlanner.Maps.Attribution;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Diagnostics;

namespace MissionPlanner.Core.Tests.Maps;

/// <summary>Verifies export attribution and sanitized map diagnostics.</summary>
public sealed class MapDiagnosticsTests
{
    /// <summary>Verifies diagnostics contain operational metadata but redact secrets and signed query values.</summary>
    [Fact]
    public void SnapshotIsUsefulAndSafeToShare()
    {
        var catalog = BuiltInMapCatalog.Load();
        var source = catalog.Sources.Single(value => value.Id == "maptiler-streets");

        var snapshot = MapDiagnosticSnapshotFactory.Create(
            catalog,
            source,
            credentialConfigured: true,
            isOnline: true,
            cacheSizeBytes: 42,
            activePack: null,
            mapsuiVersion: "5.0.0",
            platform: "Windows",
            lastSourceError: "https://example.test/tile?key=secret-value token-value",
            knownSecret: "token-value");
        var json = snapshot.ToJson();

        json.Should().Contain("maptiler-streets").And.Contain("maptiler").And.Contain("42");
        json.Should().NotContain("secret-value").And.NotContain("token-value");
        json.Should().Contain("[REDACTED]");
    }

    /// <summary>Verifies only export-required, deduplicated attribution enters a future export footer.</summary>
    [Fact]
    public void ExportFooterUsesRequiredAggregateAttribution()
    {
        var duplicate = new MapAttributionEntry("a", "© Provider", null, true, true);
        var snapshot = new MapAttributionSnapshot([
            duplicate,
            duplicate with { Id = "b" },
            new("screen-only", "Screen only", null, true, false)
        ]);

        var footer = MapExportAttribution.CreateFooter(snapshot);

        footer.Should().Be("© Provider");
    }
}
