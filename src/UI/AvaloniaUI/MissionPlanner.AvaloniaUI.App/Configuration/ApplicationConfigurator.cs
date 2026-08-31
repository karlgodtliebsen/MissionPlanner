using Avalonia.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.AvaloniaUI.App.Maps;
using MissionPlanner.AvaloniaUI.App.Services;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;
using MissionPlanner.AvaloniaUI.App.Views.Common;
using MissionPlanner.AvaloniaUI.App.Views.Connect;
using MissionPlanner.AvaloniaUI.App.Views.Exit;
using MissionPlanner.AvaloniaUI.App.Views.Main;
using MissionPlanner.AvaloniaUI.App.Views.Navigation;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Library;
using MissionPlanner.Library.Configuration;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.Maps.Configuration;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Offline;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.Simulation;
using MissionPlanner.Simulation.Abstractions;
using MissionPlanner.Simulation.ArduPilot;
using MissionPlanner.Simulation.Configuration;
using MissionPlanner.Transport.Configuration;

namespace MissionPlanner.AvaloniaUI.App.Configuration;

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
        services.TryAddSingleton(new CancellationTokenSource());

        //services.AddSingleton<IFileSaver>(FileSaver.Default);
        //services.AddSingleton<AppShellContentViewModel>();

        //// Register shared state service as singleton for runtime state management

        services.TryAddSingleton<Dispatcher>(sp => Dispatcher.UIThread);
        services.TryAddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();

        services.TryAddSingleton<IPlannerSettingsService, PlannerSettingsService>();
        services.TryAddSingleton<IPlannerSettingsStore, PreferencesPlannerSettingsStore>();
        services.TryAddSingleton<IPlannerSecretStore, SecurePlannerSecretStore>();
        services.TryAddTransient<IMapSecretStore, PlannerMapSecretStoreAdapter>();

        services.TryAddTransient<IActiveMapSourceStore, PlannerActiveMapSourceStore>();
        services.TryAddTransient<MapsuiHostedBasemapFactory>();
        services.TryAddTransient<MapsuiMbTilesSourceFactory>();
        services.TryAddTransient<IMapsuiBasemapFactory, CompositeMapsuiBasemapFactory>();
        services.TryAddTransient<IMapHttpRuntimeSettings, PlannerMapHttpRuntimeSettings>();
        services.TryAddSingleton<MapHttpOptionsProvider>();
        services.TryAddSingleton(sp => sp.GetRequiredService<MapHttpOptionsProvider>().GetOptions());

        services.TryAddTransient<ISimulatorProfileStore, PreferencesSimulatorProfileStore>();
        services.TryAddTransient<ISimulatorProfileService, SimulatorProfileService>();
        services.TryAddTransient<ISimulationScenarioPresetStore, PreferencesSimulationScenarioPresetStore>();
        services.TryAddTransient<ISitlCachePathProvider, MauiSitlCachePathProvider>();
        services.TryAddTransient<ISitlPlatformService, LocalSitlPlatformService>();
        services.TryAddTransient<ISimulatorProcessHost, LocalSimulatorProcessHost>();

        services.TryAddSingleton<ISimulatorOwnedProcessRecovery, LocalSimulatorOwnedProcessRecovery>();
        services.TryAddSingleton<ISimulatorRuntime, ArduPilotSitlRuntime>();

        services.TryAddSingleton<ApplicationStateService>();
        //services.TryAddTransient<ParametersFileHandler>();
        //services.TryAddSingleton<PlannerSettingsRuntime>();
        //services.TryAddTransient<MissionItemListViewPage>();
        //services.TryAddTransient<MissionItemListDockViewModel>();
        //services.TryAddTransient<MissionMapPresenter>();

        //services.TryAddTransient<IUserNotificationService, UserNotificationService>();
        //services.TryAddTransient<IUserConfirmationService, UserConfirmationService>();
        //services.TryAddTransient<IMissionMapInteractionService, MissionMapInteractionService>();

        services.TryAddSingleton<IJsonPoiFilePathProvider, JsonPoiFilePathProvider>();
        services.TryAddSingleton<IPoiRepository, JsonPoiRepository>();
        services.TryAddSingleton<IPoiService, PoiService>();

        //services.TryAddTransient<IUserPromptService, MauiMissionPlanningDialogService>();
        //services.TryAddTransient<IMissionTerrainElevationProvider, MissionTerrainElevationProvider>();
        //services.TryAddTransient<IUserChoiceService, MauiMissionPlanningDialogService>();
        //services.TryAddTransient<IFileOpenService, MauiMissionPlanningFileService>();
        //services.TryAddTransient<IFileSaveService, MauiMissionPlanningFileService>();
        //services.TryAddTransient<IFirmwareConnectionGateway, FirmwareConnectionGateway>();
        //services.TryAddTransient<IConnectedVehicleFirmwareGateway, ConnectedVehicleFirmwareGateway>();
        //services.TryAddTransient<IFirmwareFilePicker, MauiFirmwareFilePicker>();

        //services.TryAddTransient<IFirmwareUserInteraction, FirmwareInteractionService>();
        //services.TryAddTransient<IBootloaderEntryInteraction, FirmwareInteractionService>();
        //services.TryAddTransient<IDfuUserInteraction, FirmwareInteractionService>();

        //services.TryAddTransient<ITemporaryMavLinkBootloaderGateway, TemporaryMavLinkBootloaderGateway>();
        //services.TryAddTransient<ITextClipboardService, TextClipboardService>();
        //services.TryAddTransient<ISetupCompletionStore, PreferencesSetupCompletionStore>();
        //services.TryAddTransient<IFirmwarePackageCache, FirmwarePackageCache>();
        //services.TryAddTransient<IParameterComparisonService, ParameterComparisonService>();
        //services.TryAddTransient<IParameterValueEquivalence, ParameterValueEquivalence>();

        //services.TryAddSingleton<IFirmwareSupportLinkProvider, FirmwareSupportLinkProvider>();
        //services.TryAddSingleton<IExternalLinkLauncher, ExternalLinkLauncher>();
        //services.TryAddSingleton<IDeviceManagerLauncher, DeviceManagerLauncher>();
        //services.AddSingleton<IIntroductionContentLoader, IntroductionContentLoader>();

        services
            .AddLibraryServices()
            .AddEventHubServices()
            .AddDomainServices(configuration)
            .AddMavLinkTransportServices(configuration)
            .AddFirmwareServices(configuration)
            .AddMapServices(configuration)
            .AddSimulationServices(configuration)
            .AddMavLinkServices(configuration)
            .AddLogging(configuration, (s, l, c) =>
                /*Customize logging*/
                services.AddSerilog(c))
            .AddViewsModelsConfiguration();

        return services;
    }


    private static IServiceCollection AddViewsModelsConfiguration(this IServiceCollection services)
    {
        services.TryAddTransient<App>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();


        // Common/Shared UI Components
        services.TryAddSingleton<StatusBarViewModel>();
        services.TryAddSingleton<TopBarViewModel>();
        services.TryAddSingleton<NotificationViewModel>();

        services.TryAddTransient<ViewDialogViewModel>();

        services.TryAddSingleton<IWindowProvider, WindowProvider>();
        services.TryAddSingleton<IDialogService, AvaloniaDialogService>();

        services.TryAddTransient<ConnectPopupViewModel>();
        services.TryAddTransient<ConnectPopupView>();

        services.TryAddTransient<ExitViewModel>();
        services.TryAddTransient<ExitUserControlView>();

        services.TryAddSingleton<INavigationPageFactory, NavigationPageFactory>();
        services.TryAddSingleton<AvaloniaNavigationService>();
        services.TryAddSingleton<INavigationService>(sp => sp.GetRequiredService<AvaloniaNavigationService>());
        //services.TryAddTransient<NavigationViewModel>();

        services.TryAddTransient<ErrorViewModel>();
        services.TryAddTransient<ErrorView>();

        //services.TryAddTransient<HelpViewModel>();
        //services.TryAddTransient<ConnectPopupViewModel>();
        //services.TryAddTransient<ConnectPopupView>();
        //services.TryAddTransient<StatisticsViewModel>();
        //services.TryAddTransient<LandingPageViewModel>();
        //services.TryAddTransient<IntroductionViewModel>();
        //services.TryAddTransient<ParametersEditorView>();
        //services.TryAddTransient<ParametersEditorViewModel>();

        //services.TryAddTransient<AsyncOperationRunner>();
        //services.TryAddTransient<FlightDataMissionMapView>();
        //services.TryAddTransient<FlightPlannerMissionMapView>();
        //services.TryAddTransient<FlightPlannerMissionMapViewModel>();
        //services.TryAddTransient<FlightDataMissionMapViewModel>();


        //services.TryAddTransient<HudViewModel>();

        //// Tabs on FlightData Page
        //services.TryAddTransient<QuickTabViewModel>();
        //services.TryAddTransient<ActionsTabViewModel>();
        //services.TryAddTransient<MessagesTabViewModel>();
        //services.TryAddTransient<PreflightTabViewModel>();
        //services.TryAddTransient<GaugesTabViewModel>();
        //services.TryAddTransient<TransponderTabViewModel>();
        //services.TryAddTransient<StatusTabViewModel>();
        //services.TryAddTransient<ServoRelayTabViewModel>();
        //services.TryAddTransient<AuxFunctionTabViewModel>();
        //services.TryAddTransient<ScriptsTabViewModel>();
        //services.TryAddTransient<PayloadControlTabViewModel>();
        //services.TryAddTransient<TelemetryLogsTabViewModel>();
        //services.TryAddTransient<DataFlashLogsTabViewModel>();

        //services.TryAddTransient<AdvancedViewModel>();
        //services.TryAddTransient<InstallFirmwareViewModel>();
        //services.TryAddTransient<OptionalHardwareViewModel>();
        //services.TryAddSingleton<OptionalHardwareTabCatalog>();

        //services.TryAddTransient<FlightDataViewModel>();
        //services.TryAddTransient<FlightPlannerViewModel>();
        //services.TryAddTransient<SimulationViewModel>();
        //services.TryAddTransient<ExitViewModel>();

        //// Tabs on Config View
        //services.TryAddTransient<FullParametersListTabViewModel>();
        //services.TryAddTransient<ParameterComparisonViewModel>();
        //services.TryAddTransient<MavFtpTabViewModel>();

        //services.TryAddTransient<MandatoryHardwareViewModel>();
        //services.TryAddTransient<GeoFenceTabViewModel>();
        //services.TryAddTransient<BasicTuningTabViewModel>();
        //services.TryAddTransient<ExtendedTuningTabViewModel>();
        //services.TryAddTransient<OnboardOsdTabViewModel>();
        //services.TryAddTransient<PreferencesViewModel>();
        //services.TryAddTransient<CubeLan8PortSwitchTabViewModel>();

        ////remove
        //services.TryAddTransient<FirmwareSetupViewModel>();

        //// Workflow Tabs on Setup Mandatory Hardware View
        //services.TryAddTransient<FrameSetupViewModel>();
        //services.TryAddTransient<AccelerometerSetupViewModel>();
        //services.TryAddTransient<CompassSetupViewModel>();
        //services.TryAddTransient<RadioSetupViewModel>();
        //services.TryAddTransient<ServoOutputSetupViewModel>();
        ////services.TryAddTransient<SerialPortsViewModel>();
        //services.TryAddTransient<EscMotorSetupViewModel>();
        //services.TryAddTransient<FlightModesSetupViewModel>();
        //services.TryAddTransient<FailSafeViewModel>();
        //services.TryAddTransient<InitTuneParametersViewModel>();
        //services.TryAddTransient<HwIdViewModel>();
        //services.TryAddTransient<AdsbViewModel>();
        //services.TryAddTransient<SafetySetupViewModel>();
        //services.TryAddTransient<SetupSummaryViewModel>();

        //// Tabs on Setup Optional Hardware View
        //services.TryAddTransient<RtkGpsInjectViewModel>();
        //services.TryAddTransient<CubeIdUpdateViewModel>();
        //services.TryAddTransient<CanGpsOrderViewModel>();
        //services.TryAddTransient<BatterySetupViewModel>();
        //services.TryAddTransient<DroneCanUavCanViewModel>();
        //services.TryAddTransient<JoystickViewModel>();
        //services.TryAddTransient<CompassMotorCalibrationViewModel>();
        //services.TryAddTransient<RangefinderViewModel>();
        //services.TryAddTransient<AirspeedViewModel>();
        //services.TryAddTransient<OpticalFlowViewModel>();
        //services.TryAddTransient<OnboardOsdBridgeViewModel>();
        //services.TryAddTransient<CameraGimbalViewModel>();
        //services.TryAddTransient<SikRadioViewModel>();
        //services.TryAddTransient<BluetoothSetupViewModel>();
        //services.TryAddTransient<MotorTestViewModel>();
        //services.TryAddSingleton<MotorLayoutResolver>();
        //services.TryAddTransient<ParachuteViewModel>();
        //services.TryAddTransient<Esp8266SetupViewModel>();
        //services.TryAddTransient<AntennaTrackerViewModel>();
        //services.TryAddTransient<FftSetupViewModel>();

        return services;
    }

    private static IServiceProvider UseApplicationServices(this IServiceProvider services)
    {
        var domainFactory = services.GetRequiredService<IDomainFactory>();
        domainFactory.Add<ErrorViewModel>();
        domainFactory.Add<ErrorView>();
        domainFactory.Add<ConnectPopupView>();
        domainFactory.Add<IDialogService, AvaloniaDialogService>();

        //domainFactory.Add<ParameterComparisonViewModel>();
        //domainFactory.Add<ParameterComparisonView>();
        //domainFactory.Add<MissionItemListViewPage>();
        //domainFactory.Add<MissionMapPresenter>();
        //domainFactory.Add<FlightPlannerMissionMapViewModel>();
        //domainFactory.Add<FlightPlannerMissionMapView>();
        //domainFactory.Add<ParametersEditorViewModel>();
        //domainFactory.Add<ParametersEditorView>();
        return services;
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
            .UseSimulationServices();

        //TODO: create a replacement for MAUI PlannerSettingsService to load settings from config file or other source
        //var plannerSettingsService = serviceProvider.GetRequiredService<IPlannerSettingsService>();

        //var loadResult = plannerSettingsService.InitializeAsync().AsTask().GetAwaiter().GetResult();
        //var connection = loadResult.Settings.Connection;
        var connection = serviceProvider.GetRequiredService<IOptions<ApplicationOptions>>().Value;
        ApplicationState state = new()
        {
            SelectedChannel = connection.Channel,
            SelectedHost = connection.Host,
            SelectedPort = connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SelectedBaudRate = connection.BaudRate.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        // Register shared state service as singleton for runtime state management
        var stateService = serviceProvider.GetRequiredService<ApplicationStateService>();
        stateService.Initialize(state);
        serviceProvider.UseApplicationServices();
        return serviceProvider;
    }
}
