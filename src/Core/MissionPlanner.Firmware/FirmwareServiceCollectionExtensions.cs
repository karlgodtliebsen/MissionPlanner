using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.Firmware.Operations;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Images;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Protocol;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Compatibility;
using MissionPlanner.Firmware.Downloads;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Connected;
using MissionPlanner.Firmware.Presentation;

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
            .Validate(value => value.MaximumFirmwareImageBytes > 0, "MaximumFirmwareImageBytes must be positive.")
            .Validate(value => value.BootloaderCommandTimeout > TimeSpan.Zero, "BootloaderCommandTimeout must be positive.")
            .Validate(value => value.BootloaderEraseTimeout > TimeSpan.Zero, "BootloaderEraseTimeout must be positive.")
            .Validate(value => value.BootloaderSyncAttempts > 0, "BootloaderSyncAttempts must be positive.")
            .Validate(value => value.BootloaderRetryDelay >= TimeSpan.Zero, "BootloaderRetryDelay cannot be negative.")
            .Validate(value => value.BootloaderDiscoveryTimeout > TimeSpan.Zero, "BootloaderDiscoveryTimeout must be positive.")
            .Validate(value => value.BootloaderPortOpenTimeout > TimeSpan.Zero, "BootloaderPortOpenTimeout must be positive.")
            .Validate(value => value.BootloaderBaudRate > 0, "BootloaderBaudRate must be positive.")
            .Validate(value => value.MaximumArtifactBytes > 0, "MaximumArtifactBytes must be positive.")
            .ValidateOnStart();

        services.TryAddSingleton<IFirmwareOperationCoordinator, FirmwareOperationCoordinator>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<HttpClient>();
        services.TryAddSingleton<IFirmwareManifestClient, HttpFirmwareManifestClient>();
        services.TryAddSingleton<IFirmwareManifestParser, ArduPilotFirmwareManifestParser>();
        services.TryAddSingleton<IFirmwareCatalogCache, MemoryFirmwareCatalogCache>();
        services.TryAddSingleton<IFirmwareCatalogService, FirmwareCatalogService>();
        services.TryAddSingleton<IFirmwarePackageReader, ApjFirmwarePackageReader>();
        services.TryAddSingleton<IFirmwareSerialDeviceCatalog>(serviceProvider =>
            OperatingSystem.IsWindows()
                ? new WindowsSerialDeviceCatalog(serviceProvider.GetRequiredService<TimeProvider>())
                : new SystemSerialDeviceCatalog(serviceProvider.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<IFirmwareDeviceMonitor, PollingFirmwareDeviceMonitor>();
        services.TryAddSingleton<IFirmwareSerialPortFactory, SystemFirmwareSerialPortFactory>();
        services.TryAddSingleton<IArduPilotBootloaderClientFactory, ArduPilotBootloaderClientFactory>();
        services.TryAddSingleton<IBootloaderDiscoveryService, BootloaderDiscoveryService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBootloaderEntryStrategy, AlreadyInBootloaderEntryStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBootloaderEntryStrategy, TemporaryMavLinkRebootEntryStrategy>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBootloaderEntryStrategy, ManualReconnectBootloaderEntryStrategy>());
        services.TryAddSingleton<IBootloaderEntryService, BootloaderEntryService>();
        services.TryAddSingleton<IFirmwareCompatibilityService, FirmwareCompatibilityService>();
        services.TryAddSingleton<IFirmwareArtifactStore, FileSystemFirmwareArtifactStore>();
        services.TryAddSingleton<IFirmwareArtifactDownloader, FirmwareArtifactDownloader>();
        services.TryAddSingleton<IFirmwareInstallationService, FirmwareInstallationService>();
        services.TryAddSingleton<IEmbeddedBootloaderUpdateService, EmbeddedBootloaderUpdateService>();
        services.TryAddSingleton<IFirmwarePageModeResolver, FirmwarePageModeResolver>();

        return services;
    }
}
