using FluentAssertions;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Settings;

namespace MissionPlanner.Core.Tests.Maps;

/// <summary>Verifies settings metadata and safe source fallback.</summary>
public sealed class MapSettingsSourceCatalogTests
{
    /// <summary>Verifies sources are grouped by user purpose and expose reviewed details.</summary>
    [Fact]
    public void CreateBuildsUserFacingGroupsAndDetails()
    {
        var items = MapSettingsSourceCatalog.Create(BuiltInMapCatalog.Load());

        items.Should().Contain(value => value.Group == MapSettingsSourceGroup.OnlineProviders && value.Id == "osm-standard");
        items.Should().Contain(value => value.Group == MapSettingsSourceGroup.BlankMap);
        items.Single(value => value.Id == "osm-standard").AttributionPreview.Should().Contain("OpenStreetMap");
        items.Should().OnlyContain(value => value.PolicyReviewDate != default);
    }

    /// <summary>Verifies a deleted persisted source falls back to the ordinary online default.</summary>
    [Fact]
    public void ResolveFallsBackWhenSelectedSourceWasDeleted()
    {
        var items = MapSettingsSourceCatalog.Create(BuiltInMapCatalog.Load());

        var selected = MapSettingsSourceCatalog.Resolve(items, "deleted-source", isOnline: true);

        selected.Id.Should().Be("osm-standard");
    }

    /// <summary>Verifies offline operation falls back to a local or blank source.</summary>
    [Fact]
    public void ResolveDoesNotSelectOnlineSourceWhileOffline()
    {
        var items = MapSettingsSourceCatalog.Create(BuiltInMapCatalog.Load());

        var selected = MapSettingsSourceCatalog.Resolve(items, "osm-standard", isOnline: false);

        selected.Group.Should().BeOneOf(MapSettingsSourceGroup.OfflinePacks, MapSettingsSourceGroup.BlankMap);
    }

    /// <summary>Verifies a source with a missing credential is rejected.</summary>
    [Fact]
    public void ResolveRejectsSourceWithMissingCredential()
    {
        var items = MapSettingsSourceCatalog.Create(BuiltInMapCatalog.Load());
        var protectedSource = items.First(value => value.Source.CredentialRequirement != MapCredentialRequirement.None);

        var selected = MapSettingsSourceCatalog.Resolve(items, protectedSource.Id, isOnline: true);

        selected.Id.Should().NotBe(protectedSource.Id);
    }
}
