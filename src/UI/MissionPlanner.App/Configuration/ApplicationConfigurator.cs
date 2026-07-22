using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.App.AppViewModels;
using MissionPlanner.App.Presentation;
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
using MissionPlanner.App.Views.InitSetup;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Firmware;
using MissionPlanner.App.Views.Missions;
using MissionPlanner.App.Views.Simulation;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Library;
using MissionPlanner.Library.Configuration;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.MavLink.Configuration;
using MissionPlanner.Transport.Configuration;

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
        services.TryAddSingleton<ApplicationStateService>();
        services.TryAddSingleton<ParametersFileHandler>();

        //services.TryAddSingleton<Views.Vehicles.Views.ModelMapper>();
        services.TryAddSingleton<ThemeChangeViewModel>();

        services.TryAddSingleton(new CancellationTokenSource());
        services.TryAddTransient<IExtendedDialogService, ExtendedDialogService>();

        services
            .AddLibraryServices()
            .AddEventHubServices()
            .AddDomainServices(configuration)
            .AddMavLinkTransportServices(configuration)
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
        return services;
    }

    private static IServiceCollection AddViewsConfiguration(this IServiceCollection services)
    {
        services.TryAddSingleton<App>();
        services.TryAddSingleton<AppShell>();

        // Common/Shared UI Components
        services.TryAddSingleton<StatusBarViewModel>();

        services.TryAddSingleton<TopBarViewModel>();

        services.TryAddSingleton<ExitViewModel>();
        services.TryAddSingleton<ExitContentView>();
        services.TryAddTransient<ErrorViewModel>();
        services.TryAddTransient<ErrorView>();

        services.TryAddSingleton<HelpViewModel>();
        services.TryAddTransient<ConnectPopupViewModel>();
        services.TryAddTransient<ConnectPopupView>();
        services.TryAddTransient<StatisticsViewModel>();

        services.TryAddSingleton<FlightDataViewModel>();
        services.TryAddSingleton<IUserNotificationService, MauiUserNotificationService>();
        services.TryAddSingleton<IUserConfirmationService, MauiUserConfirmationService>();
        services.TryAddSingleton<ITextClipboardService, MauiTextClipboardService>();
        services.TryAddTransient<AsyncOperationRunner>();

        services.TryAddSingleton<HudViewModel>();
        services.TryAddSingleton<MissionMapViewModel>();
        services.TryAddSingleton<QuickTabViewModel>();
        services.AddSingleton<IFlightDataTabLifecycle>(serviceProvider => serviceProvider.GetRequiredService<QuickTabViewModel>());
        services.TryAddSingleton<ActionsTabViewModel>();
        services.AddSingleton<IFlightDataTabLifecycle>(serviceProvider => serviceProvider.GetRequiredService<ActionsTabViewModel>());
        services.TryAddSingleton<AuxFunctionTabViewModel>();
        services.TryAddSingleton<DataFlashLogsTabViewModel>();
        services.TryAddSingleton<GaugesTabViewModel>();
        services.TryAddSingleton<MessagesTabViewModel>();
        services.AddSingleton<IFlightDataTabLifecycle>(serviceProvider => serviceProvider.GetRequiredService<MessagesTabViewModel>());
        services.TryAddSingleton<PayloadControlTabViewModel>();
        services.TryAddSingleton<StatusTabViewModel>();
        services.TryAddSingleton<PreflightTabViewModel>();
        services.TryAddSingleton<ScriptsTabViewModel>();
        services.TryAddSingleton<ServoRelayTabViewModel>();
        services.TryAddSingleton<TelemetryLogsTabViewModel>();
        services.TryAddSingleton<TransponderTabViewModel>();
        services.TryAddSingleton<FlightPlannerViewModel>();
        services.TryAddSingleton<ISetupCompletionStore, PreferencesSetupCompletionStore>();
        services.TryAddSingleton<IFirmwarePackageCache, MauiFirmwarePackageCache>();
        services.TryAddSingleton<ISetupNavigationService, ShellSetupNavigationService>();
        services.TryAddSingleton<ISetupWorkflowViewModelFactory, SetupWorkflowViewModelFactory>();
        services.TryAddSingleton<InitSetupViewModel>();
        services.TryAddSingleton<SimulationViewModel>();
        services.TryAddSingleton<ExitViewModel>();
        services.TryAddSingleton<FullParametersListTabViewModel>();
        services.TryAddSingleton<MavFtpTabViewModel>();
        return services;
    }


    /// <summary>
    /// 
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

        var applicationOptions = serviceProvider.GetRequiredService<IOptions<ApplicationOptions>>().Value;
        ApplicationState state = new() { SelectedBaudRate = applicationOptions.BaudRate, SelectedPort = applicationOptions.Port };
        // Register shared state service as singleton for runtime state management
        var stateService = serviceProvider.GetRequiredService<ApplicationStateService>();
        stateService.Initialize(state);

        serviceProvider.UseApplicationServices();

        return serviceProvider;
    }
}
