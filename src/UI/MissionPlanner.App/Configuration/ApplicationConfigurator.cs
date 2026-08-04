using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Navigation;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Services;
using MissionPlanner.App.Views.Common;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.App.Views.ConfigTuning.Tabs;
using MissionPlanner.App.Views.Connect;
using MissionPlanner.App.Views.Exit;
using MissionPlanner.App.Views.FlightData;
using MissionPlanner.App.Views.FlightData.Hud;
using MissionPlanner.App.Views.FlightData.Tabs;
using MissionPlanner.App.Views.FlightPlanner;
using MissionPlanner.App.Views.Help;
using MissionPlanner.App.Views.InitSetup.Advanced;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Services;
using MissionPlanner.App.Views.InitSetup.OptionalHardware;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.App.Views.Simulation;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Connected;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Library;
using MissionPlanner.Library.Configuration;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.Transport.Configuration;
using UraniumUI.Material.Dialogs;

namespace MissionPlanner.App.Configuration;

/// <summary>
/// Configures the application services and options.
/// </summary>
public static class ApplicationConfigurator
{
    /// <summary>
    /// Adds the application configuration to the service collection.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddApplicationConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        //TODO: add app-settings/config file
        var applicationOptions = configuration.GetSection(ApplicationOptions.SectionName).Get<ApplicationOptions>();
        DomainException.ThrowIfNull(applicationOptions, ApplicationOptions.Template);

        services.AddSingleton(Options.Create(applicationOptions));

        // Register shared state service as singleton for runtime state management
        services.TryAddTransient<INavigationService, ShellNavigationService>();
        services.TryAddTransient<IModalNavigationService, ModalNavigationService>();
        services.TryAddTransient<IConfigNavigationGuard, ConfigNavigationGuard>();


        services.TryAddTransient<IPlannerSettingsStore, PreferencesPlannerSettingsStore>();
        services.TryAddTransient<IPlannerSecretStore, SecurePlannerSecretStore>();
        services.TryAddTransient<IPlannerSettingsService, PlannerSettingsService>();
        services.TryAddTransient<ISimulatorProfileStore, PreferencesSimulatorProfileStore>();
        services.TryAddTransient<ISimulatorProfileService, SimulatorProfileService>();
        services.TryAddTransient<ISimulationScenarioPresetStore, PreferencesSimulationScenarioPresetStore>();
        services.TryAddTransient<ISitlCachePathProvider, MauiSitlCachePathProvider>();
        services.TryAddTransient<ISitlPlatformService, LocalSitlPlatformService>();
        services.TryAddTransient<ISimulatorProcessHost, LocalSimulatorProcessHost>();


        services.Replace(ServiceDescriptor.Singleton<ISimulatorOwnedProcessRecovery, LocalSimulatorOwnedProcessRecovery>());
        services.Replace(ServiceDescriptor.Singleton<ISimulatorRuntime, ArduPilotSitlRuntime>());


        //services.TryAddSingleton<Views.Vehicles.Views.ModelMapper>();
        services.TryAddTransient<ApplicationStateService>();
        services.TryAddTransient<ParametersFileHandler>();
        services.TryAddTransient<PlannerSettingsRuntime>();
        services.TryAddSingleton(new CancellationTokenSource());

        services.TryAddTransient<IExtendedDialogService, ExtendedDialogService>();
        services.TryAddTransient<IUserNotificationService, UserNotificationService>();
        services.TryAddTransient<IUserConfirmationService, UserConfirmationService>();
        services.TryAddTransient<IFirmwareConnectionGateway, FirmwareConnectionGateway>();
        services.TryAddTransient<IConnectedVehicleFirmwareGateway, ConnectedVehicleFirmwareGateway>();
        services.TryAddTransient<FirmwareInteractionService>();
        services.TryAddTransient<IFirmwareFilePicker, MauiFirmwareFilePicker>();
        services.TryAddTransient<IFirmwareUserInteraction>(serviceProvider => serviceProvider.GetRequiredService<FirmwareInteractionService>());
        services.TryAddTransient<IBootloaderEntryInteraction>(serviceProvider => serviceProvider.GetRequiredService<FirmwareInteractionService>());
        services.TryAddTransient<ITemporaryMavLinkBootloaderGateway, TemporaryMavLinkBootloaderGateway>();
        services.TryAddTransient<ITextClipboardService, TextClipboardService>();
        services.TryAddTransient<ISetupCompletionStore, PreferencesSetupCompletionStore>();
        services.TryAddTransient<IFirmwarePackageCache, FirmwarePackageCache>();
        services.TryAddTransient<IParameterComparisonService, ParameterComparisonService>();
        services.TryAddTransient<IParameterValueEquivalence, ParameterValueEquivalence>();

        services
            .AddLibraryServices()
            .AddEventHubServices()
            .AddDomainServices(configuration)
            .AddMavLinkTransportServices(configuration)
            .AddFirmwareServices(configuration)
            .AddMavLinkServices(configuration)
            .AddLogging(configuration, (s, l, c) =>
                /*Customize logging*/
                services.AddSerilog(c))
            .AddViewsConfiguration();

        return services;
    }

    private static IServiceProvider UseApplicationServices(this IServiceProvider services)
    {
        var domainFactory = services.GetRequiredService<IDomainFactory>();
        domainFactory.Add<ErrorViewModel, ErrorViewModel>();
        domainFactory.Add<ErrorView, ErrorView>();
        domainFactory.Add<ParameterComparisonViewModel, ParameterComparisonViewModel>();
        domainFactory.Add<ParameterComparisonView, ParameterComparisonView>();
        return services;
    }

    private static IServiceCollection AddViewsConfiguration(this IServiceCollection services)
    {
        services.TryAddTransient<App>();
        services.TryAddTransient<AppShell>();

        // Common/Shared UI Components
        services.TryAddTransient<StatusBarViewModel>();

        services.TryAddTransient<TopBarViewModel>();

        services.TryAddTransient<ExitViewModel>();
        services.TryAddTransient<ExitContentView>();
        services.TryAddTransient<ErrorViewModel>();
        services.TryAddTransient<ErrorView>();

        services.TryAddTransient<HelpViewModel>();
        services.TryAddTransient<ConnectPopupViewModel>();
        services.TryAddTransient<ConnectPopupView>();
        services.TryAddTransient<StatisticsViewModel>();

        services.TryAddTransient<AsyncOperationRunner>();

        services.TryAddTransient<HudViewModel>();
        services.TryAddTransient<MissionMapViewModel>();

        //Tabs on FlightDataView
        services.TryAddTransient<QuickTabViewModel>();
        services.TryAddTransient<ActionsTabViewModel>();
        services.TryAddTransient<MessagesTabViewModel>();
        services.TryAddTransient<PreflightTabViewModel>();
        services.TryAddTransient<GaugesTabViewModel>();
        services.TryAddTransient<TransponderTabViewModel>();
        services.TryAddTransient<StatusTabViewModel>();
        services.TryAddTransient<ServoRelayTabViewModel>();
        services.TryAddTransient<AuxFunctionTabViewModel>();
        services.TryAddTransient<ScriptsTabViewModel>();
        services.TryAddTransient<PayloadControlTabViewModel>();
        services.TryAddTransient<TelemetryLogsTabViewModel>();
        services.TryAddTransient<DataFlashLogsTabViewModel>();


        services.AddSingleton<IFlightDataTabLifecycle>(serviceProvider => serviceProvider.GetRequiredService<ActionsTabViewModel>());
        services.AddSingleton<IFlightDataTabLifecycle>(serviceProvider => serviceProvider.GetRequiredService<QuickTabViewModel>());
        services.AddSingleton<IFlightDataTabLifecycle>(serviceProvider => serviceProvider.GetRequiredService<MessagesTabViewModel>());


        services.TryAddTransient<AdvancedViewModel>();
        services.TryAddTransient<InstallFirmwareViewModel>();
        services.TryAddTransient<OptionalHardwareViewModel>();

        services.TryAddTransient<FlightDataViewModel>();
        services.TryAddTransient<FlightPlannerViewModel>();
        services.TryAddTransient<SimulationViewModel>();
        services.TryAddTransient<ExitViewModel>();

        services.TryAddTransient<FullParametersListTabViewModel>();
        services.TryAddTransient<ParameterComparisonViewModel>();
        services.TryAddTransient<MavFtpTabViewModel>();

        services.TryAddTransient<MandatoryHardwareViewModel>();
        services.TryAddTransient<GeoFenceTabViewModel>();
        services.TryAddTransient<BasicTuningTabViewModel>();
        services.TryAddTransient<ExtendedTuningTabViewModel>();
        services.TryAddTransient<OnboardOsdTabViewModel>();
        services.TryAddTransient<PlannerTabViewModel>();
        services.TryAddTransient<CubeLan8PortSwitchTabViewModel>();


        services.TryAddTransient(serviceProvider => CreateSetupSectionViewModel<FirmwareSetupViewModel>(serviceProvider, SetupWorkflowKey.Firmware));
        services.TryAddTransient(serviceProvider => CreateSetupSectionViewModel<FrameSetupViewModel>(serviceProvider, SetupWorkflowKey.Frame));
        services.TryAddTransient(serviceProvider => CreateSetupSectionViewModel<AccelerometerSetupViewModel>(serviceProvider, SetupWorkflowKey.Accelerometer));
        services.TryAddTransient(serviceProvider => CreateSetupSectionViewModel<CompassSetupViewModel>(serviceProvider, SetupWorkflowKey.Compass));
        services.TryAddTransient(serviceProvider => CreateSetupSectionViewModel<RadioSetupViewModel>(serviceProvider, SetupWorkflowKey.Radio));
        services.TryAddTransient(serviceProvider => CreateSetupSectionViewModel<FlightModesSetupViewModel>(serviceProvider, SetupWorkflowKey.FlightModes));
        services.TryAddTransient(serviceProvider => CreateSetupSectionViewModel<BatterySetupViewModel>(serviceProvider, SetupWorkflowKey.Battery));
        services.TryAddTransient(serviceProvider => CreateSetupSectionViewModel<EscMotorSetupViewModel>(serviceProvider, SetupWorkflowKey.Esc));
        services.TryAddTransient(serviceProvider => CreateSetupSectionViewModel<ServoOutputSetupViewModel>(serviceProvider, SetupWorkflowKey.ServoOutput));
        services.TryAddTransient(serviceProvider => CreateSetupSectionViewModel<OptionalHardwareSetupViewModel>(serviceProvider, SetupWorkflowKey.OptionalHardware));
        services.TryAddTransient(serviceProvider => CreateSetupSectionViewModel<SafetySetupViewModel>(serviceProvider, SetupWorkflowKey.Safety));
        services.TryAddTransient(serviceProvider => CreateSetupSectionViewModel<SetupSummaryViewModel>(serviceProvider, SetupWorkflowKey.Summary));

        return services;
    }

    private static TViewModel CreateSetupSectionViewModel<TViewModel>(IServiceProvider serviceProvider, SetupWorkflowKey key)
        where TViewModel : SetupWorkflowDetailViewModel
    {
        var descriptor = serviceProvider.GetRequiredService<ISetupWorkflowCatalog>().Workflows.Single(workflow => workflow.Key == key);
        return ActivatorUtilities.CreateInstance<TViewModel>(serviceProvider, descriptor);
    }


    /// <summary>
    /// Post ServiceProvider Build Setup - This method is called after the ServiceProvider has been built and is used to perform any additional setup or initialization that requires access to the fully constructed service provider. 
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <returns></returns>
    public static IServiceProvider UseApplication(this IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ApplicationOptions>>();
        logger.LogInformation("UseApplication - Setting up Application Operations");
        serviceProvider
            .UseMavLinkServices()
            .UseDomainServices()
            ;
        serviceProvider.GetRequiredService<PlannerSettingsRuntime>().ApplyCurrent();
        var plannerSettingsService = serviceProvider.GetRequiredService<IPlannerSettingsService>();
        var loadResult = plannerSettingsService.InitializeAsync().AsTask().GetAwaiter().GetResult();
        var connection = loadResult.Settings.Connection;
        ApplicationState state = new() { SelectedChannel = connection.Channel, SelectedHost = connection.Host, SelectedPort = connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture), SelectedBaudRate = connection.BaudRate.ToString(System.Globalization.CultureInfo.InvariantCulture) };

        // Register shared state service as singleton for runtime state management
        var stateService = serviceProvider.GetRequiredService<ApplicationStateService>();
        stateService.Initialize(state);
        _ = serviceProvider.GetRequiredService<PlannerSettingsRuntime>();

        serviceProvider.UseApplicationServices();

        return serviceProvider;
    }
}
