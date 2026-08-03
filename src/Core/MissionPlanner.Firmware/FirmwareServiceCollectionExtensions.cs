using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.Firmware.Operations;
using MissionPlanner.Firmware.Catalog;

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

        options.Validate(value => value.ManifestUri.IsAbsoluteUri && value.ManifestUri.Scheme is "http" or "https",
                "ManifestUri must be an absolute HTTP or HTTPS URI.")
            .Validate(value => value.CatalogCacheDuration > TimeSpan.Zero, "CatalogCacheDuration must be positive.")
            .Validate(value => value.MaximumManifestBytes > 0, "MaximumManifestBytes must be positive.")
            .ValidateOnStart();

        services.TryAddSingleton<IFirmwareOperationCoordinator, FirmwareOperationCoordinator>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<HttpClient>();
        services.TryAddSingleton<IFirmwareManifestClient, HttpFirmwareManifestClient>();
        services.TryAddSingleton<IFirmwareManifestParser, ArduPilotFirmwareManifestParser>();
        services.TryAddSingleton<IFirmwareCatalogCache, MemoryFirmwareCatalogCache>();
        services.TryAddSingleton<IFirmwareCatalogService, FirmwareCatalogService>();

        return services;
    }
}
