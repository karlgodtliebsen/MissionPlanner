using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.Maps.Attribution;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Custom;
using MissionPlanner.Maps.Esri;
using MissionPlanner.Maps.Hosted;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Offline;
using MissionPlanner.Maps.Policy;
using MissionPlanner.Maps.Prefetch;
using MissionPlanner.Maps.Sources;
using MissionPlanner.Maps.Terrain;

namespace MissionPlanner.Maps.Configuration;

/// <summary>
/// Provides extension methods for configuring map-related services in the application.
/// </summary>
public static class MapsConfigurator
{
    /// <summary>
    /// Adds Map services to the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to which domain services will be added.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddMapServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IMapCatalog, BuiltInMapCatalogService>();
        services.TryAddSingleton(provider => provider.GetRequiredService<IMapCatalog>().Current);
        services.TryAddSingleton<IMapPolicyEvaluator, MapPolicyEvaluator>();
        services.TryAddSingleton<IMapTilePrefetchService, MapTilePrefetchService>();
        services.TryAddSingleton<HttpMessageHandler>(_ => new SocketsHttpHandler());
        services.TryAddSingleton<IMapHttpClientFactory, MapHttpClientFactory>();
        services.TryAddSingleton<CustomMapSourceService>();
        services.TryAddSingleton<IOfflineMapPackRepository>(provider => provider.GetRequiredService<FileOfflineMapPackRepository>());
        services.TryAddSingleton<IOfflineMapPackValidator, MbTilesOfflineMapPackValidator>();
        services.TryAddSingleton<IOfflineMapPackInstaller, OfflineMapPackInstaller>();
        services.TryAddSingleton<IOfflineMapPackManager, OfflineMapPackManager>();
        services.TryAddSingleton<HostedMapSourceService>();
        services.TryAddSingleton<IMapSourceResolver, MapSourceResolver>();
        services.TryAddSingleton<IMapHttpResourceFetcher, MapHttpResourceFetcher>();
        services.TryAddSingleton<IMapAttributionService, MapAttributionService>();
        services.TryAddSingleton<ICustomMapSourceStore>(_ => new JsonCustomMapSourceStore(Path.Combine(FileSystem.AppDataDirectory, "Maps", "custom-sources.json")));
        services.TryAddSingleton(_ => new FileOfflineMapPackRepository(FileSystem.AppDataDirectory));
        services.TryAddSingleton(_ => new MapHttpDiskCache(Path.Combine(FileSystem.CacheDirectory, "Maps", "Http"), 256L * 1_048_576));
        services.TryAddSingleton<ITerrainElevationService>(provider => new SrtmTerrainElevationService(
            provider.GetRequiredService<IMapHttpClientFactory>(),
            Path.Combine(FileSystem.CacheDirectory, "Maps", "Terrain", "Srtm")));

        services.TryAddTransient<IMapDynamicAttributionResolver, EsriAttributionResolver>();
        services.TryAddTransient<IMapAttributionCoordinator, MapAttributionCoordinator>();

        return services;
    }

    /// <summary>
    /// Configures serviceProvider that are being instantiated through the IDomainFactory. These typical requires constructor arguments, that are not registered in the DI container.
    /// This method registers the domain serviceProvider with the domain factory, allowing them to be created as needed.
    /// 
    /// </summary>
    /// <param name="serviceProvider">The service provider from which IDomainFactory will be resolved.</param>
    /// <returns>The updated service provider.</returns>
    public static IServiceProvider UseMapServices(this IServiceProvider serviceProvider)
    {
        var domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();

        return serviceProvider;
    }
}
