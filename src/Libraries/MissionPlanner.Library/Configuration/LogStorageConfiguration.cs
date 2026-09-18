using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.Library.Logging;

namespace MissionPlanner.Library.Configuration;

/// <summary>Registers the host's logging storage without resolving desktop paths at startup.</summary>
public static class LogStorageConfiguration
{
    /// <summary>Adds browser session storage or desktop storage, preserving host overrides.</summary>
    public static IServiceCollection AddLogStorage(this IServiceCollection services, bool? browser = null)
    {
        if (browser ?? OperatingSystem.IsBrowser())
        {
            services.TryAddSingleton<ILogStorage>(_ => new BrowserLogStorage());
        }
        else
        {
            services.TryAddSingleton<ILogPathProvider, DesktopLogPathProvider>();
            services.TryAddSingleton<ILogStorage, DesktopLogStorage>();
        }

        services.TryAddTransient<LogFileOperations>();
        return services;
    }
}
