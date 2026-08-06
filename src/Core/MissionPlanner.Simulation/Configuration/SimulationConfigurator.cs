using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.Shared.Models.Services;
using MissionPlanner.Shared.Models.Services.Abstractions;
using MissionPlanner.Simulation.Abstractions;
using MissionPlanner.Simulation.ArduPilot;

namespace MissionPlanner.Simulation.Configuration;

/// <summary>
/// 
/// </summary>
public static class SimulationConfigurator
{
    /// <summary>
    /// Adds Firmware services to the specified service collection.
    /// </summary>
    /// <param name="services">The service collection to which domain services will be added.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="options">An optional firmware-options configuration callback.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSimulationServices(this IServiceCollection services, IConfiguration configuration, Action<FirmwareOptions>? options = null)
    {
        services.TryAddSingleton<ISimulatorHostEnvironment, LocalSimulatorHostEnvironment>();
        services.TryAddSingleton<ISimulatorProfileValidator, SimulatorProfileValidator>();
        services.TryAddSingleton<IArduPilotFrameCatalog, ArduPilotFrameCatalog>();
        services.TryAddSingleton<IArduPilotLaunchPlanBuilder, ArduPilotLaunchPlanBuilder>();
        services.TryAddSingleton<ISimulationPortAllocator, SimulationPortAllocator>();
        services.TryAddSingleton<ISimulatorOwnedProcessRecovery, UnavailableSimulatorOwnedProcessRecovery>();
        services.TryAddSingleton<ISimulationOwnershipStore, SimulationOwnershipStore>();
        services.TryAddSingleton<ISimulationSessionManager, SimulationSessionManager>();
        services.TryAddSingleton<ISimulationSessionManagerFactory, SimulationSessionManagerFactory>();
        services.TryAddSingleton<ISimulationFleetManager, SimulationFleetManager>();
        services.TryAddSingleton<ISimulationControlCatalog, SimulationControlCatalog>();
        services.TryAddSingleton<ISimulationScenarioPresetService, SimulationScenarioPresetService>();
        services.TryAddSingleton<ISimulationScenarioParser, SimulationScenarioParser>();
        services.TryAddSingleton<ISimulationScenarioDelay, SimulationScenarioDelay>();
        services.TryAddSingleton<ISimulationScenarioReportExporter, SimulationScenarioReportExporter>();
        services.TryAddSingleton<ISitlManifestProvider, JsonSitlManifestProvider>();
        services.TryAddSingleton<ISitlReleaseSelector, SitlReleaseSelector>();
        services.TryAddSingleton<ISitlPackageManager, SitlPackageManager>();
        services.TryAddSingleton<ISitlInstallationService, SitlInstallationService>();
        services.TryAddTransient<ISerialPortDiscoveryService, SerialPortDiscoveryService>();

        return services;
    }

    /// <summary>
    /// Configures serviceProvider that are being instantiated through the IDomainFactory. These typical requires constructor arguments, that are not registered in the DI container.
    /// This method registers the domain serviceProvider with the domain factory, allowing them to be created as needed.
    /// 
    /// </summary>
    /// <param name="serviceProvider">The service provider from which IDomainFactory will be resolved.</param>
    /// <returns>The updated service provider.</returns>
    public static IServiceProvider UseSimulationServices(this IServiceProvider serviceProvider)
    {
        var domainFactory = serviceProvider.GetRequiredService<IDomainFactory>();


        return serviceProvider;
    }
}
