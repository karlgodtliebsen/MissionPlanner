using FluentAssertions;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Custom;
using MissionPlanner.Maps.Offline;
using MissionPlanner.Maps.Policy;
using MissionPlanner.Maps.Sources;
using NSubstitute;

namespace MissionPlanner.Core.Tests.Maps;

/// <summary>Verifies stable map-source resolution across every supported namespace.</summary>
public sealed class MapSourceResolverTests
{
    /// <summary>Verifies each production built-in resolves with provider and policy metadata.</summary>
    [Theory]
    [InlineData("osm-standard")]
    [InlineData("esri-world-topo")]
    [InlineData("esri-world-physical")]
    [InlineData("esri-world-shaded-relief")]
    [InlineData("esri-world-dark-gray")]
    [InlineData("no-map")]
    public async Task ResolveBuiltInSource(string sourceId)
    {
        var result = await CreateResolver().ResolveAsync(sourceId, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Source!.Id.Should().Be(sourceId);
        result.Source.Origin.Should().Be(MapSourceOrigin.Catalog);
        result.Source.EffectivePolicy.Id.Should().Be(result.Source.Definition.PolicyId);
    }

    /// <summary>Verifies pack IDs resolve a concrete installed archive.</summary>
    [Fact]
    public async Task ResolveInstalledPack()
    {
        var archive = Path.GetTempFileName();
        try
        {
            var repository = Substitute.For<IOfflineMapPackRepository>();
            repository.FindAsync("denmark", "1", Arg.Any<CancellationToken>()).Returns(Pack(archive));
            var result = await CreateResolver(repository: repository).ResolveAsync("pack:denmark:1", TestContext.Current.CancellationToken);

            result.IsSuccess.Should().BeTrue();
            result.Source!.Origin.Should().Be(MapSourceOrigin.InstalledPack);
            result.Source.Location.Should().Be(archive);
        }
        finally
        {
            File.Delete(archive);
        }
    }

    /// <summary>Verifies missing pack and custom IDs produce distinct typed outcomes.</summary>
    [Theory]
    [InlineData("pack:missing:1", MapSourceResolutionStatus.PackMissing)]
    [InlineData("custom:missing", MapSourceResolutionStatus.CustomSourceMissing)]
    public async Task ResolveMissingNamespacedSource(string sourceId, MapSourceResolutionStatus expected)
    {
        var result = await CreateResolver().ResolveAsync(sourceId, TestContext.Current.CancellationToken);
        result.Status.Should().Be(expected);
        result.Source.Should().BeNull();
    }

    /// <summary>Verifies a valid custom XYZ source is projected into renderer-neutral metadata.</summary>
    [Fact]
    public async Task ResolveCustomSource()
    {
        var custom = new CustomMapSourceSettings("local", "Local", MapAccessKind.HttpXyz, "https://maps.local/{z}/{x}/{y}.png", 0, 18, null, null, null, MapCredentialRequirement.None, "© Local", true);
        var store = Substitute.For<ICustomMapSourceStore>();
        store.LoadAsync(Arg.Any<CancellationToken>()).Returns(new[] { custom });

        var result = await CreateResolver(customSources: store).ResolveAsync("custom:local", TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Source!.Origin.Should().Be(MapSourceOrigin.Custom);
        result.Source.Definition.AccessKind.Should().Be(MapAccessKind.HttpXyz);
    }

    /// <summary>Verifies credentials, deferred formats, unknown IDs, and cancellation are typed.</summary>
    [Theory]
    [InlineData("stadia-outdoors", MapSourceResolutionStatus.CredentialMissing)]
    [InlineData("protomaps-pmtiles", MapSourceResolutionStatus.Deferred)]
    [InlineData("unknown", MapSourceResolutionStatus.UnknownSource)]
    public async Task ResolveExpectedConfigurationState(string sourceId, MapSourceResolutionStatus expected)
    {
        var result = await CreateResolver().ResolveAsync(sourceId, TestContext.Current.CancellationToken);
        result.Status.Should().Be(expected);
    }

    /// <summary>Verifies cancellation is returned without an ordinary configuration exception.</summary>
    [Fact]
    public async Task ResolveCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var result = await CreateResolver().ResolveAsync("osm-standard", cancellation.Token);
        result.Status.Should().Be(MapSourceResolutionStatus.Cancelled);
    }

    private static MapSourceResolver CreateResolver(
        IOfflineMapPackRepository? repository = null,
        ICustomMapSourceStore? customSources = null)
    {
        repository ??= Substitute.For<IOfflineMapPackRepository>();
        if (customSources is null)
        {
            customSources = Substitute.For<ICustomMapSourceStore>();
            customSources.LoadAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<CustomMapSourceSettings>());
        }
        return new(
            new BuiltInMapCatalogService(),
            new MapPolicyEvaluator(),
            Substitute.For<IMapSecretStore>(),
            repository,
            customSources);
    }

    private static InstalledOfflineMapPack Pack(string archive) => new(
        new OfflineMapPackManifest("denmark", "1", "Denmark", Path.GetFileName(archive), new string('0', 64), 0, new(-1, -1, 1, 1), 0, 18, "EPSG:3857", "png", "© Test", "Test"),
        Path.GetDirectoryName(archive)!,
        archive);
}
