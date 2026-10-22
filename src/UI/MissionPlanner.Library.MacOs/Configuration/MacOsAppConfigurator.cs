using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.App.Services;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Setup.Advanced;
using MissionPlanner.Library.Desktop;
using MissionPlanner.Library.Desktop.Configuration;

namespace MissionPlanner.Library.MacOs.Configuration;

/// <summary>Registers the services that are specific to the macOS app.</summary>
public static class MacOsAppConfigurator
{
    /// <summary>Registers the macOS implementations of the platform services; secrets use the login keychain.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddMacOsOnlyServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IAdvancedPlatformCapabilities>(
            _ => new DesktopAdvancedCapabilities("macOS", location: false, secureKeyStorage: true));
        services.TryAddSingleton<IPlatformLocationService, UnavailablePlatformLocationService>();
        services.TryAddSingleton<IPlannerSecretStore, MacOsKeychainSecretStore>();
        services.AddDesktopServices();
        return services;
    }
}
