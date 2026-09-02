using Avalonia.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.AvaloniaUI.App.Maps;
using MissionPlanner.AvaloniaUI.App.Presentation;
using MissionPlanner.AvaloniaUI.App.Services;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;
using MissionPlanner.AvaloniaUI.App.Views.Common;
using MissionPlanner.AvaloniaUI.App.Views.Config;
using MissionPlanner.AvaloniaUI.App.Views.ConfigTuning.Tabs;
using MissionPlanner.AvaloniaUI.App.Views.Connect;
using MissionPlanner.AvaloniaUI.App.Views.DialogTest;
using MissionPlanner.AvaloniaUI.App.Views.Exit;
using MissionPlanner.AvaloniaUI.App.Views.FlightData;
using MissionPlanner.AvaloniaUI.App.Views.FlightData.Hud;
using MissionPlanner.AvaloniaUI.App.Views.FlightData.Tabs;
using MissionPlanner.AvaloniaUI.App.Views.FlightPlanner;
using MissionPlanner.AvaloniaUI.App.Views.Help;
using MissionPlanner.AvaloniaUI.App.Views.InitSetup.Advanced;
using MissionPlanner.AvaloniaUI.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware;
using MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware.Services;
using MissionPlanner.AvaloniaUI.App.Views.InitSetup.OptionalHardware;
using MissionPlanner.AvaloniaUI.App.Views.InitSetup.OptionalHardware.Sections;
using MissionPlanner.AvaloniaUI.App.Views.Introduction;
using MissionPlanner.AvaloniaUI.App.Views.Landing;
using MissionPlanner.AvaloniaUI.App.Views.Main;
using MissionPlanner.AvaloniaUI.App.Views.Missions;
using MissionPlanner.AvaloniaUI.App.Views.Missions.DockView;
using MissionPlanner.AvaloniaUI.App.Views.Navigation;
using MissionPlanner.AvaloniaUI.App.Views.Preferences;
using MissionPlanner.AvaloniaUI.App.Views.Simulation;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Connected;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Installation;
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
using DialogDemoViewModel = MissionPlanner.AvaloniaUI.App.Views.DialogTest.DialogDemoViewModel;
using ErrorView = MissionPlanner.AvaloniaUI.App.Utilities.Dialogs.SubViews.ErrorView;
using ErrorViewModel = MissionPlanner.AvaloniaUI.App.Utilities.Dialogs.SubViews.ErrorViewModel;

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
        services.TryAddSingleton<IPlatformLocationService, WindowsPlatformLocationService>();

        services.TryAddSingleton<IPlannerSettingsService, PlannerSettingsService>();
        services.TryAddSingleton<IPlannerSettingsStore, JsonPlannerSettingsStore>();
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
        services.TryAddTransient<ParametersFileHandler>();
        //services.TryAddSingleton<PlannerSettingsRuntime>();
        services.TryAddTransient<MissionItemListViewPage>();
        services.TryAddTransient<MissionItemListDockViewModel>();
        services.TryAddTransient<MissionMapPresenter>();

        services.TryAddTransient<IUserNotificationService, UserNotificationService>();
        services.TryAddTransient<IUserConfirmationService, UserConfirmationService>();
        services.TryAddTransient<IMissionMapInteractionService, MissionMapInteractionService>();

        services.TryAddSingleton<IJsonPoiFilePathProvider, JsonPoiFilePathProvider>();
        services.TryAddSingleton<IPoiRepository, JsonPoiRepository>();
        services.TryAddSingleton<IPoiService, PoiService>();

        services.TryAddTransient<IMissionTerrainElevationProvider, MissionTerrainElevationProvider>();
        services.TryAddTransient<AvaloniaMissionPlanningFileService>();
        services.TryAddTransient<IFileOpenService>(sp => sp.GetRequiredService<AvaloniaMissionPlanningFileService>());
        services.TryAddTransient<IFileSaveService>(sp => sp.GetRequiredService<AvaloniaMissionPlanningFileService>());
        services.TryAddTransient<IFirmwareConnectionGateway, FirmwareConnectionGateway>();
        services.TryAddTransient<IConnectedVehicleFirmwareGateway, ConnectedVehicleFirmwareGateway>();
        services.TryAddTransient<IFirmwareUserInteraction, FirmwareInteractionService>();
        services.TryAddTransient<IBootloaderEntryInteraction, FirmwareInteractionService>();
        services.TryAddTransient<IDfuUserInteraction, FirmwareInteractionService>();

        services.TryAddTransient<ITemporaryMavLinkBootloaderGateway, TemporaryMavLinkBootloaderGateway>();
        services.TryAddTransient<ITextClipboardService, TextClipboardService>();
        services.TryAddSingleton<ISetupCompletionStore, JsonSetupCompletionStore>();

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
        services.TryAddTransient<OverlayViewDialogViewModel>();

        services.TryAddSingleton<IWindowProvider, WindowProvider>();
        services.TryAddSingleton<IDialogService, AvaloniaDialogService>();

        services.TryAddTransient<ConnectPopupViewModel>();
        services.TryAddTransient<ConnectPopupView>();

        services.TryAddTransient<DialogDemoViewModel>();
        services.TryAddTransient<DialogDemoPage>();


        services.TryAddTransient<ExitViewModel>();
        services.TryAddTransient<ExitUserControlView>();

        services.TryAddSingleton<INavigationPageFactory, NavigationPageFactory>();

        //This is to satisfy the DI container for INavigationService, but we will use AvaloniaNavigationService as the implementation
        services.TryAddSingleton<INavigationService, AvaloniaNavigationService>();
        //services.TryAddSingleton<INavigationService>(sp => sp.GetRequiredService<AvaloniaNavigationService>());

        services.TryAddSingleton<MainShellViewModel>();

        services.TryAddTransient<FlightDataMissionMapViewModel>();
        services.TryAddTransient<FlightDataViewModel>();
        services.TryAddTransient<FlightDataPage>();
        services.TryAddTransient<FlightPlannerMissionMapViewModel>();
        services.TryAddTransient<FlightPlannerViewModel>();
        services.TryAddTransient<FlightPlannerPage>();
        services.TryAddTransient<ConfigPage>();
        services.TryAddTransient<PreferencesViewModel>();
        services.TryAddTransient<PreferencesPage>();
        services.TryAddTransient<SimulationPage>();
        services.TryAddTransient<IntroductionPage>();
        services.TryAddTransient<HelpPage>();

        services.TryAddTransient<ErrorViewModel>();
        services.TryAddTransient<ErrorView>();

        services.TryAddTransient<HelpViewModel>();
        services.TryAddTransient<StatisticsViewModel>();
        services.TryAddTransient<LandingPageViewModel>();
        services.TryAddTransient<IntroductionViewModel>();

        //services.TryAddTransient<ParametersEditorView>();
        services.TryAddTransient<ParametersEditorViewModel>();

        services.TryAddTransient<AsyncOperationRunner>();

        //services.TryAddTransient<FlightDataMissionMapView>();
        //services.TryAddTransient<FlightPlannerMissionMapView>();
        services.TryAddTransient<FlightPlannerMissionMapViewModel>();
        services.TryAddTransient<FlightDataMissionMapViewModel>();


        services.TryAddTransient<HudViewModel>();

        // Tabs on FlightData Page
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

        services.TryAddTransient<AdvancedViewModel>();
        services.TryAddTransient<InstallFirmwareViewModel>();
        services.TryAddTransient<OptionalHardwareViewModel>();
        services.TryAddSingleton<OptionalHardwareTabCatalog>();

        services.TryAddTransient<FlightDataViewModel>();
        services.TryAddTransient<FlightPlannerViewModel>();
        services.TryAddTransient<DialogDemoViewModel>();
        services.TryAddTransient<ExitViewModel>();

        // Tabs on Config View
        services.TryAddTransient<FullParametersListTabViewModel>();
        services.TryAddTransient<ParameterComparisonViewModel>();
        services.TryAddTransient<MavFtpTabViewModel>();

        services.TryAddTransient<MandatoryHardwareViewModel>();
        services.TryAddTransient<GeoFenceTabViewModel>();
        services.TryAddTransient<BasicTuningTabViewModel>();
        services.TryAddTransient<ExtendedTuningTabViewModel>();
        services.TryAddTransient<OnboardOsdTabViewModel>();
        services.TryAddTransient<PreferencesViewModel>();
        services.TryAddTransient<CubeLan8PortSwitchTabViewModel>();

        ////remove
        services.TryAddTransient<FirmwareSetupViewModel>();

        services.TryAddTransient<InstallFirmwarePage>();
        services.TryAddTransient<InstallFirmwareViewModel>();

        services.TryAddTransient<MandatoryHardwarePage>();
        services.TryAddTransient<MandatoryHardwareViewModel>();


        //// Workflow Tabs on Setup Mandatory Hardware View
        services.TryAddTransient<FrameSetupViewModel>();
        services.TryAddTransient<AccelerometerSetupViewModel>();
        services.TryAddTransient<CompassSetupViewModel>();
        services.TryAddTransient<RadioSetupViewModel>();
        services.TryAddTransient<ServoOutputSetupViewModel>();
        ////services.TryAddTransient<SerialPortsViewModel>();
        services.TryAddTransient<EscMotorSetupViewModel>();
        services.TryAddTransient<FlightModesSetupViewModel>();
        services.TryAddTransient<FailSafeViewModel>();
        services.TryAddTransient<InitTuneParametersViewModel>();
        services.TryAddTransient<HwIdViewModel>();
        services.TryAddTransient<AdsbViewModel>();
        services.TryAddTransient<SafetySetupViewModel>();
        services.TryAddTransient<SetupSummaryViewModel>();

        //// Tabs on Setup Optional Hardware View
        services.TryAddTransient<RtkGpsInjectViewModel>();
        services.TryAddTransient<CubeIdUpdateViewModel>();
        services.TryAddTransient<CanGpsOrderViewModel>();
        services.TryAddTransient<BatterySetupViewModel>();
        services.TryAddTransient<DroneCanUavCanViewModel>();
        services.TryAddTransient<JoystickViewModel>();
        services.TryAddTransient<CompassMotorCalibrationViewModel>();
        services.TryAddTransient<RangefinderViewModel>();
        services.TryAddTransient<AirspeedViewModel>();
        services.TryAddTransient<OpticalFlowViewModel>();
        services.TryAddTransient<OnboardOsdBridgeViewModel>();
        services.TryAddTransient<CameraGimbalViewModel>();
        services.TryAddTransient<SikRadioViewModel>();
        services.TryAddTransient<BluetoothSetupViewModel>();
        services.TryAddTransient<MotorTestViewModel>();
        services.TryAddSingleton<MotorLayoutResolver>();
        services.TryAddTransient<ParachuteViewModel>();
        services.TryAddTransient<Esp8266SetupViewModel>();
        services.TryAddTransient<AntennaTrackerViewModel>();
        services.TryAddTransient<FftSetupViewModel>();

        return services;
    }

    private static IServiceProvider UseApplicationServices(this IServiceProvider services)
    {
        var domainFactory = services.GetRequiredService<IDomainFactory>();
        domainFactory.Add<ErrorViewModel>();
        domainFactory.Add<ErrorView>();
        domainFactory.Add<ConnectPopupView>();
        domainFactory.Add<IDialogService, AvaloniaDialogService>();

        domainFactory.Add<ParameterComparisonViewModel>();
        //domainFactory.Add<ParameterComparisonView>();
        domainFactory.Add<MissionItemListViewPage>();
        domainFactory.Add<MissionMapPresenter>();
        //domainFactory.Add<FlightPlannerMissionMapViewModel>();
        //domainFactory.Add<FlightPlannerMissionMapView>();
        domainFactory.Add<ParametersEditorViewModel>();
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

        var plannerSettingsService = serviceProvider.GetRequiredService<IPlannerSettingsService>();
        var loadResult = plannerSettingsService.InitializeAsync().AsTask().GetAwaiter().GetResult();
        if (!string.IsNullOrWhiteSpace(loadResult.Message))
        {
            logger.LogInformation("Planner settings initialization: {Message}", loadResult.Message);
        }

        var connection = loadResult.Settings.Connection;
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
