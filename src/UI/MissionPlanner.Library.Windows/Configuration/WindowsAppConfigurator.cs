using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.App.Services;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.Library.Windows.Configuration;

public static class WindowsAppConfigurator
{

    public static IServiceCollection AddWindowsOnlyServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IPlatformLocationService, WindowsPlatformLocationService>();
        services.TryAddSingleton<IPlannerSecretStore, SecurePlannerSecretStore>();

        return services;
    }

}
