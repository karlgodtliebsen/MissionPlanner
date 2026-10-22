using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.App.Services;
using MissionPlanner.App.Maps;
using MissionPlanner.Maps.Offline;
using MissionPlanner.Library.Windows.Maps;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.Library.Windows.Configuration;

/// <summary>Registers the services that are specific to the Windows app.</summary>
public static class WindowsAppConfigurator
{

    /// <summary>Registers the Windows implementations of the platform services.</summary>
    public static IServiceCollection AddWindowsOnlyServices(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<MissionPlanner.Firmware.Betaflight.IUsbTopologyProvider, Firmware.WindowsUsbTopologyProvider>());
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
