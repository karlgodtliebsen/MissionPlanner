using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.App.Services;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Setup.Advanced;
using MissionPlanner.Library.Desktop;
using MissionPlanner.Library.Desktop.Configuration;

namespace MissionPlanner.Library.Linux.Configuration;

/// <summary>Registers the services that are specific to the Linux app.</summary>
public static class LinuxAppConfigurator
{
    /// <summary>
    /// Registers the Linux implementations of the platform services. Secrets use the desktop keyring when
    /// <c>secret-tool</c> is installed and are otherwise kept in memory for the session only.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddLinuxOnlyServices(this IServiceCollection services)
    {
        var keyring = LinuxSecretToolStore.IsAvailable();
        services.TryAddSingleton<IAdvancedPlatformCapabilities>(
            _ => new DesktopAdvancedCapabilities("Linux", location: false, secureKeyStorage: keyring));
        services.TryAddSingleton<IPlatformLocationService, UnavailablePlatformLocationService>();
        if (keyring)
        {
            services.TryAddSingleton<IPlannerSecretStore, LinuxSecretToolStore>();
        }
        else
        {
            services.TryAddSingleton<IPlannerSecretStore, SessionPlannerSecretStore>();
        }

        services.AddDesktopServices();
        return services;
    }
}
