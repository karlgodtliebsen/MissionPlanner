using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.App.Maps;
using MissionPlanner.Core.Setup.Advanced.Output;
using MissionPlanner.Core.Setup.Advanced.Warnings;
using MissionPlanner.Library.Desktop.Maps;
using MissionPlanner.Maps.Offline;

namespace MissionPlanner.Library.Desktop.Configuration;

/// <summary>Registers the services shared by every desktop platform (Windows, Linux and macOS).</summary>
public static class DesktopAppConfigurator
{
    /// <summary>Registers serial/network output, warning storage and offline MBTiles map support.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<MissionPlanner.Core.Notifications.ISpeechService, DesktopSpeechService>());
        services.Replace(ServiceDescriptor.Singleton<MissionPlanner.Core.Setup.OptionalHardware.IJoystickProvider, SdlJoystickProvider>());
        services.TryAddSingleton<IOutputSinkFactory, DesktopOutputSinkFactory>();
        services.TryAddSingleton<IWarningRuleStore, DesktopWarningRuleStore>();
        services.TryAddTransient<IMapsuiMbTilesSourceFactory, MapsuiMbTilesSourceFactory>();
        services.TryAddSingleton<IOfflineMapPackValidator, MbTilesOfflineMapPackValidator>();
        return services;
    }
}
