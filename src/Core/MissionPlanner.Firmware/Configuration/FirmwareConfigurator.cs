using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Compatibility;
using MissionPlanner.Firmware.Connected;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Downloads;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Images;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;
using MissionPlanner.Firmware.Preparation;
using MissionPlanner.Firmware.Presentation;
using MissionPlanner.Firmware.Protocol;
using MissionPlanner.Firmware.Recovery;
using MissionPlanner.Library.Factory.Domain.Abstractions;

namespace MissionPlanner.Firmware.Configuration;

/// <summary>
/// 
/// </summary>
public static class FirmwareConfigurator
{
    /// <summary>
    /// Adds Firmware services to the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to which domain services will be added.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="options">An optional firmware-options configuration callback.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddFirmwareServices(this IServiceCollection services, IConfiguration configuration, Action<FirmwareOptions>? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var firmwareOptions = services.AddOptions<FirmwareOptions>(); //as long as we are not using appsettings.json, we can use this to configure the options directly in code.
        services.AddOptions<DfuOptions>()
            .Validate(value => value.OfficialFirmwareHosts is { Length: > 0 } && value.OfficialFirmwareHosts.All(host => Uri.CheckHostName(host) is not UriHostNameType.Unknown), "At least one valid official firmware host is required.")
            .Validate(value => value.TargetPolicies is not null && value.TargetPolicies.All(policy =>
                    !string.IsNullOrWhiteSpace(policy.Platform) && policy.CompatibleMcuDeviceIds is not null &&
                    policy.CompatibleMcuDeviceIds.All(id => !string.IsNullOrWhiteSpace(id)) &&
                    policy.MinimumInternalFlashBytes is null or > 0 && policy.MaximumInternalFlashBytes is null or > 0 &&
                    (policy.MinimumInternalFlashBytes is null || policy.MaximumInternalFlashBytes is null || policy.MinimumInternalFlashBytes <= policy.MaximumInternalFlashBytes)),
                "DFU target policies must contain valid platform, MCU, and flash constraints.")
            .Validate(value => value.MinimumCubeProgrammerVersion.Major >= 0, "MinimumCubeProgrammerVersion is required.")
            .Validate(value => value.CubeProgrammerProbeTimeout > TimeSpan.Zero, "CubeProgrammerProbeTimeout must be positive.")
            .Validate(value => value.MaximumProviderOutputLines > 0, "MaximumProviderOutputLines must be positive.")
            .Validate(value => value.MaximumProviderOutputCharacters > 0, "MaximumProviderOutputCharacters must be positive.")
            .Validate(value => value.ProviderStartupTimeout > TimeSpan.Zero, "ProviderStartupTimeout must be positive.")
            .Validate(value => value.ProviderProgrammingTimeout > TimeSpan.Zero, "ProviderProgrammingTimeout must be positive.")
            .Validate(value => value.DefaultUsbVendorId > 0, "DefaultUsbVendorId must be positive.")
            .Validate(value => value.DefaultUsbProductId > 0, "DefaultUsbProductId must be positive.")
            .Validate(value => value.AcceptedWindowsDriverServices is { Length: > 0 } && value.AcceptedWindowsDriverServices.All(service => !string.IsNullOrWhiteSpace(service)), "At least one accepted Windows DFU driver service is required.")
            .Validate(value => value.DevicePollInterval > TimeSpan.Zero, "DevicePollInterval must be positive.")
            .Validate(value => value.DfuDisappearanceTimeout > TimeSpan.Zero, "DfuDisappearanceTimeout must be positive.")
            .Validate(value => value.DfuApplicationRediscoveryTimeout > TimeSpan.Zero, "DfuApplicationRediscoveryTimeout must be positive.")
            .Validate(value => value.MaximumIntelHexSourceBytes > 0, "MaximumIntelHexSourceBytes must be positive.")
            .Validate(value => value.MaximumIntelHexDataBytes > 0, "MaximumIntelHexDataBytes must be positive.")
            .Validate(value => value.MaximumIntelHexAddressSpan > 0, "MaximumIntelHexAddressSpan must be positive.")
            .Validate(value => value.Stm32FlashEndAddressExclusive > value.Stm32FlashStartAddress, "The STM32 flash policy range must be positive.")
            .Validate(value => value.ExpectedApplicationStartAddress > value.Stm32FlashStartAddress && value.ExpectedApplicationStartAddress < value.Stm32FlashEndAddressExclusive, "ExpectedApplicationStartAddress must lie inside the STM32 flash policy range.")
            .ValidateOnStart();
        //If we were using appsettings.json, we would use services.Configure<FirmwareOptions>(configuration.GetSection(FirmwareOptions.SectionName));
        if (options is not null)
        {
            firmwareOptions.Configure(options);
        }

        firmwareOptions
            .Validate(value => value.ManifestUri.IsAbsoluteUri && value.ManifestUri.Scheme is "http" or "https", "ManifestUri must be an absolute HTTP or HTTPS URI.")
            .Validate(value => value.CatalogCacheDuration > TimeSpan.Zero, "CatalogCacheDuration must be positive.")
            .Validate(value => value.MaximumManifestBytes > 0, "MaximumManifestBytes must be positive.")
            .Validate(value => value.MaximumManifestDownloadBytes > 0, "MaximumManifestDownloadBytes must be positive.")
            .Validate(value => value.HttpRequestTimeout > TimeSpan.Zero, "HttpRequestTimeout must be positive.")
            .Validate(value => !string.IsNullOrWhiteSpace(value.HttpUserAgent), "HttpUserAgent is required.")
            .Validate(value => value.MaximumFirmwareImageBytes > 0, "MaximumFirmwareImageBytes must be positive.")
            .Validate(value => value.BootloaderCommandTimeout > TimeSpan.Zero, "BootloaderCommandTimeout must be positive.")
            .Validate(value => value.BootloaderEraseTimeout > TimeSpan.Zero, "BootloaderEraseTimeout must be positive.")
            .Validate(value => value.BootloaderSyncAttempts > 0, "BootloaderSyncAttempts must be positive.")
            .Validate(value => value.BootloaderRetryDelay >= TimeSpan.Zero, "BootloaderRetryDelay cannot be negative.")
            .Validate(value => value.BootloaderDiscoveryTimeout > TimeSpan.Zero, "BootloaderDiscoveryTimeout must be positive.")
            .Validate(value => value.BootloaderDiscoveryPollInterval > TimeSpan.Zero, "BootloaderDiscoveryPollInterval must be positive.")
            .Validate(value => value.BootloaderPortOpenTimeout > TimeSpan.Zero, "BootloaderPortOpenTimeout must be positive.")
            .Validate(value => value.BootloaderSynchronizationTimeout > TimeSpan.Zero, "BootloaderSynchronizationTimeout must be positive.")
            .Validate(value => value.BootloaderBaudRate > 0, "BootloaderBaudRate must be positive.")
            .Validate(value => value.TemporaryMavLinkHeartbeatTimeout > TimeSpan.Zero, "TemporaryMavLinkHeartbeatTimeout must be positive.")
            .Validate(value => value.BootloaderInitialProbeTimeout > TimeSpan.Zero, "BootloaderInitialProbeTimeout must be positive.")
            .Validate(value => value.TemporaryMavLinkCommandAckTimeout > TimeSpan.Zero, "TemporaryMavLinkCommandAckTimeout must be positive.")
            .Validate(value => value.MaximumArtifactBytes > 0, "MaximumArtifactBytes must be positive.")
            .Validate(value => value.ArtifactCacheQuotaBytes > 0, "ArtifactCacheQuotaBytes must be positive.")
            .Validate(value => value.ArtifactCacheMaximumAge > TimeSpan.Zero, "ArtifactCacheMaximumAge must be positive.")
            .ValidateOnStart();

        services.TryAddSingleton<IFirmwareOperationCoordinator, FirmwareOperationCoordinator>();
        services.TryAddSingleton<IIntelHexInspector, IntelHexInspector>();
        services.TryAddSingleton<IWindowsDfuPnPSnapshotSource>(serviceProvider =>
            OperatingSystem.IsWindows() ? new WindowsRegistryDfuPnPSnapshotSource() : new EmptyWindowsDfuPnPSnapshotSource());
        services.TryAddSingleton<IDfuToolDiscoverySource>(serviceProvider =>
            OperatingSystem.IsWindows()
                ? new WindowsCubeProgrammerDiscoverySource(serviceProvider.GetRequiredService<IOptions<DfuOptions>>())
                : new EmptyDfuToolDiscoverySource());
        services.TryAddSingleton<IDfuChildProcessFactory, SystemDfuChildProcessFactory>();
        services.TryAddSingleton<IDfuProcessRunner, DfuProcessRunner>();
        services.TryAddSingleton<Stm32CubeProgrammerCommandBuilder>();
        services.TryAddSingleton<IDfuToolLocator>(serviceProvider =>
            OperatingSystem.IsWindows()
                ? new Stm32CubeProgrammerToolLocator(
                    serviceProvider.GetRequiredService<IDfuToolDiscoverySource>(),
                    serviceProvider.GetService<IDfuProcessRunner>() ?? new UnavailableDfuProcessRunner(),
                    serviceProvider.GetRequiredService<IOptions<DfuOptions>>())
                : new UnsupportedDfuToolLocator());
        services.TryAddSingleton<IDfuProgrammer, Stm32CubeProgrammerCliDfuProgrammer>();
        services.TryAddSingleton<IDfuHexArtifactDownloader>(serviceProvider => new DfuHexArtifactDownloader(
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(FirmwareHttpClient.Name),
            serviceProvider.GetRequiredService<IFirmwareArtifactStore>(),
            serviceProvider.GetRequiredService<IIntelHexInspector>(),
            serviceProvider.GetRequiredService<IOptions<DfuOptions>>(),
            serviceProvider.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<IDfuArtifactResolver, DfuArtifactResolver>();
        services.TryAddSingleton<IDfuTargetSafetyService, DfuTargetSafetyService>();
        services.TryAddSingleton<IDfuInstallationService, DfuInstallationService>();
        services.TryAddSingleton<IWindowsUsbDeviceChangeNotifier>(serviceProvider =>
            OperatingSystem.IsWindows() ? new WindowsUsbRegistryChangeNotifier() : new PollingDfuDeviceChangeNotifier());
        services.TryAddSingleton<IDfuDeviceCatalog>(serviceProvider =>
            OperatingSystem.IsWindows()
                ? new WindowsDfuDeviceCatalog(
                    serviceProvider.GetRequiredService<IWindowsDfuPnPSnapshotSource>(),
                    serviceProvider.GetRequiredService<IOptions<DfuOptions>>(),
                    serviceProvider.GetRequiredService<TimeProvider>())
                : new EmptyDfuDeviceCatalog());
        services.TryAddSingleton<IDfuDeviceMonitor, WindowsDfuDeviceMonitor>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(FirmwareHttpClient.Name, (serviceProvider, client) =>
            {
                var configured = serviceProvider.GetRequiredService<IOptions<FirmwareOptions>>().Value;
                client.Timeout = configured.HttpRequestTimeout;
                client.DefaultRequestHeaders.UserAgent.ParseAdd(configured.HttpUserAgent);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, ConnectTimeout = TimeSpan.FromSeconds(15) });
        services.TryAddSingleton<IFirmwareManifestClient>(serviceProvider => new HttpFirmwareManifestClient(
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(FirmwareHttpClient.Name),
            serviceProvider.GetRequiredService<IOptions<FirmwareOptions>>()));
        services.TryAddSingleton<IFirmwareManifestParser, ArduPilotFirmwareManifestParser>();
        services.TryAddSingleton<IFirmwareCachePathProvider, DefaultFirmwareCachePathProvider>();
        services.TryAddSingleton<IFirmwareCatalogCache, PersistentFirmwareCatalogCache>();
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
        services.TryAddSingleton<IFirmwareArtifactDownloader>(serviceProvider => new FirmwareArtifactDownloader(
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(FirmwareHttpClient.Name),
            serviceProvider.GetRequiredService<IFirmwareArtifactStore>(),
            serviceProvider.GetRequiredService<IFirmwarePackageReader>(),
            serviceProvider.GetRequiredService<IOptions<FirmwareOptions>>(),
            serviceProvider.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<IFirmwarePreparationService, FirmwarePreparationService>();
        services.TryAddSingleton<IFirmwareInstallationService, FirmwareInstallationService>();
        services.TryAddSingleton<IEmbeddedBootloaderUpdateService, EmbeddedBootloaderUpdateService>();
        services.TryAddSingleton<IFirmwarePageModeResolver, FirmwarePageModeResolver>();
        services.TryAddSingleton<IFirmwareApplicationDiscoveryService, FirmwareApplicationDiscoveryService>();

        services.TryAddSingleton<IFirmwareManifestSelector, FirmwareManifestSelector>();

        return services;
    }

    /// <summary>
    /// Configures serviceProvider that are being instantiated through the IDomainFactory. These typical requires constructor arguments, that are not registered in the DI container.
    /// This method registers the domain serviceProvider with the domain factory, allowing them to be created as needed.
    /// 
    /// </summary>
    /// <param name="serviceProvider">The service provider from which IDomainFactory will be resolved.</param>
    /// <returns>The updated service provider.</returns>
    public static IServiceProvider UseFirmwareServices(this IServiceProvider serviceProvider)
    {
        var domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();
        //domainFactory.Add<IVehicleFileSystemService, VehicleFileSystemService>();


        return serviceProvider;
    }
}
