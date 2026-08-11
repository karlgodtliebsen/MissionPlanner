using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Core.Missions.Rally;
using MissionPlanner.Maps.Configuration;
using MissionPlanner.Maps.Coordinates;
using MissionPlanner.Maps.Prefetch;

namespace MissionPlanner.Core.Tests.MissionMapMenu;

/// <summary>Verifies final mission-map safety limits and production composition.</summary>
public sealed class MissionMapCompositionTests
{
    [Fact]
    public void PlanningLimits_ArePositiveAndInternallyConsistent()
    {
        Assert.InRange(MissionPlanningLimits.MaximumImportedFileBytes, 1, MissionPlanningLimits.MaximumExpandedGeospatialBytes);
        Assert.InRange(MissionPlanningLimits.MaximumGeneratedMissionItems, 1, MissionPlanningLimits.MaximumSurveyPoints);
        Assert.InRange(MissionPlanningLimits.MaximumTextGeneratorPoints, 1, MissionPlanningLimits.MaximumGeneratedMissionItems);
        Assert.InRange(MapPrefetchLimits.MaximumTiles, 1, 100_000);
    }

    [Fact]
    public void ProductionConfigurators_RegisterMissionMapServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddDomainServices(configuration);
        services.AddMapServices(configuration);

        AssertRegistered<IPlanningPolygonService>(services);
        AssertRegistered<IGeospatialImportService>(services);
        AssertRegistered<IAutoWaypointGenerator>(services);
        AssertRegistered<ISurveyMissionGenerator>(services);
        AssertRegistered<IMissionElevationProfileService>(services);
        AssertRegistered<ITrackerHomeService>(services);
        AssertRegistered<IRallyConfigurationService>(services);
        AssertRegistered<IMapTilePrefetchService>(services);
        AssertRegistered<IGeodeticCoordinateConverter>(services);
    }

    private static void AssertRegistered<TService>(IServiceCollection services) =>
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TService));
}
