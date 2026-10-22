using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.App.Services;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Library.Desktop.Configuration;

namespace MissionPlanner.Library.Windows.Configuration;

/// <summary>Registers the services that are specific to the Windows app.</summary>
public static class WindowsAppConfigurator
{

    /// <summary>Registers the Windows implementations of the platform services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddWindowsOnlyServices(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<MissionPlanner.Firmware.Betaflight.IUsbTopologyProvider, Firmware.WindowsUsbTopologyProvider>());
        services.TryAddSingleton<MissionPlanner.Core.Setup.Advanced.IAdvancedPlatformCapabilities, WindowsAdvancedCapabilities>();
        services.TryAddSingleton<IPlatformLocationService, WindowsPlatformLocationService>();
        services.TryAddSingleton<IPlannerSecretStore, SecurePlannerSecretStore>();
        services.AddDesktopServices();

        return services;
    }

}
