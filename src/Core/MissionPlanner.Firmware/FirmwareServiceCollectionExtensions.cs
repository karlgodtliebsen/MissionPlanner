using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.Firmware.Operations;

namespace MissionPlanner.Firmware;

/// <summary>Registers the Mission Planner firmware subsystem.</summary>
public static class FirmwareServiceCollectionExtensions
{
    /// <summary>Adds firmware services and optional host configuration.</summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configure">An optional firmware-options configuration callback.</param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddMissionPlannerFirmware(
        this IServiceCollection services,
        Action<FirmwareOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<FirmwareOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton<IFirmwareOperationCoordinator, FirmwareOperationCoordinator>();

        return services;
    }
}
