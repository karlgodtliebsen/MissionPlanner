using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.App.Services;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Maps.Custom;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Offline;
using MissionPlanner.Maps.Terrain;
using MissionPlanner.App.Configuration;
using MissionPlanner.Library.Browser.Transport;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Shared.Models.Services.Abstractions;

namespace MissionPlanner.Library.Browser.Configuration;

public static class BrowserAppConfigurator
{
    public static IServiceCollection AddBrowserOnlyServices(this IServiceCollection services)
    {
        services.TryAddTransient<IMavLinkConnectionSessionFactory, BrowserConnectionSessionFactory>();
        services.TryAddTransient<ISerialPortDiscoveryService, BrowserSerialPortDiscovery>();
        services.PostConfigure<ApplicationOptions>(options =>
        {
            options.Channels.Clear();
            options.Channels.Add("UDP");
            options.Channel = "UDP";
        });
        services.TryAddSingleton<IPlatformLocationService, BrowserPlatformLocationService>();
        services.TryAddSingleton<IPlannerSecretStore, BrowserPlannerSecretStore>();
        services.TryAddSingleton<IPlannerSettingsStore, BrowserPlannerSettingsStore>();
        // WebAssembly has no OS application-data folders. These paths belong to
        // its virtual filesystem and last only for this page's runtime instance.
        const string dataRoot = "/missionplanner";
        services.TryAddSingleton(_ => new FileOfflineMapPackRepository(dataRoot));
        services.TryAddSingleton<ICustomMapSourceStore>(_ => new JsonCustomMapSourceStore(
            Path.Combine(dataRoot, "Maps", "custom-sources.json")));
        services.TryAddSingleton(_ => new MapHttpDiskCache(
            Path.Combine(dataRoot, "Maps", "Http"), 256L * 1_048_576));
        services.TryAddSingleton<ITerrainElevationService>(provider => new SrtmTerrainElevationService(
            provider.GetRequiredService<IMapHttpClientFactory>(),
            Path.Combine(dataRoot, "Maps", "Terrain", "Srtm")));
        services.TryAddSingleton<HttpMessageHandler>(_ => new HttpClientHandler());
        return services;
    }
}
