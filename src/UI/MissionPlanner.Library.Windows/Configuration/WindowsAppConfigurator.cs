using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.App.Services;
using MissionPlanner.App.Maps;
using MissionPlanner.Maps.Offline;
using MissionPlanner.Library.Windows.Maps;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.Library.Windows.Configuration;

public static class WindowsAppConfigurator
{

    public static IServiceCollection AddWindowsOnlyServices(this IServiceCollection services)
    {
        services.TryAddSingleton<MissionPlanner.Core.Setup.Advanced.IAdvancedPlatformCapabilities, WindowsAdvancedCapabilities>();
        services.TryAddSingleton<MissionPlanner.Core.Setup.Advanced.Output.IOutputSinkFactory, WindowsOutputSinkFactory>();
        services.TryAddSingleton<MissionPlanner.Core.Setup.Advanced.Warnings.IWarningRuleStore, WindowsWarningRuleStore>();
        services.TryAddTransient<IMapsuiMbTilesSourceFactory, MapsuiMbTilesSourceFactory>();
        services.TryAddSingleton<IOfflineMapPackValidator, MbTilesOfflineMapPackValidator>();
        services.TryAddSingleton<IPlatformLocationService, WindowsPlatformLocationService>();
        services.TryAddSingleton<IPlannerSecretStore, SecurePlannerSecretStore>();

        return services;
    }

}
