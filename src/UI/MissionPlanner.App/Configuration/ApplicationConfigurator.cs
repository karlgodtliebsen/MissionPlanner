using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.App.AppViewModels;
using MissionPlanner.App.Views.Common;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.App.Views.ConfigTuning.Tabs;
using MissionPlanner.App.Views.Connect;
using MissionPlanner.App.Views.Exit;
using MissionPlanner.App.Views.FlightData;
using MissionPlanner.App.Views.FlightData.Hud;
using MissionPlanner.App.Views.FlightData.Map;
using MissionPlanner.App.Views.FlightData.Tabs;
using MissionPlanner.App.Views.FlightPlanner;
using MissionPlanner.App.Views.Help;
using MissionPlanner.App.Views.InitSetup;
using MissionPlanner.App.Views.Simulation;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Library;
using MissionPlanner.Library.Configuration;
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

        ApplicationState state = new() { SelectedBaudRate = applicationOptions.BaudRate, /* SelectedConnectionType = applicationOptions.ConnectionType,*/ SelectedPort = applicationOptions.Port };
        // Register shared state service as singleton for runtime state management
        ApplicationStateService stateService = new();
        stateService.Initialize(state);
        services.TryAddSingleton(stateService);

        services.TryAddSingleton<ParametersFileHandler>();

        //services.TryAddSingleton<Views.Vehicles.Views.ModelMapper>();
        services.TryAddSingleton<ThemeChangeViewModel>();

        services.TryAddSingleton(new CancellationTokenSource());

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

    private static IServiceCollection AddViewsConfiguration(this IServiceCollection services)
    {
        services.TryAddSingleton<App>();
        services.TryAddSingleton<AppShell>();

        // Common/Shared UI Components
        services.TryAddSingleton<StatusBarViewModel>();

        //services.TryAddSingleton<DashboardPageViewModel>();
        //services.TryAddSingleton<DashboardPage>();

        services.TryAddSingleton<TopBarViewModel>();

        services.TryAddSingleton<ExitViewModel>();
        services.TryAddSingleton<ExitContentView>();

        services.TryAddSingleton<HelpViewModel>();
        services.TryAddTransient<ConnectPopupViewModel>();
        services.TryAddTransient<ConnectPopupView>();

        services.TryAddSingleton<FlightDataViewModel>();
        services.TryAddSingleton<HudViewModel>();
        services.TryAddSingleton<FlightDataMapViewModel>();
        services.TryAddSingleton<QuickTabViewModel>();
        services.TryAddSingleton<ActionsTabViewModel>();
        services.TryAddSingleton<AuxFunctionTabViewModel>();
        services.TryAddSingleton<DataFlashLogsTabViewModel>();
        services.TryAddSingleton<GaugesTabViewModel>();
        services.TryAddSingleton<MessagesTabViewModel>();
        services.TryAddSingleton<PayloadControlTabViewModel>();
        services.TryAddSingleton<StatusTabViewModel>();
        services.TryAddSingleton<PreflightTabViewModel>();
        services.TryAddSingleton<ScriptsTabViewModel>();
        services.TryAddSingleton<ServoRelayTabViewModel>();
        services.TryAddSingleton<TelemetryLogsTabViewModel>();
        services.TryAddSingleton<TransponderTabViewModel>();
        services.TryAddSingleton<FlightPlannerViewModel>();
        services.TryAddSingleton<InitSetupViewModel>();
        services.TryAddSingleton<SimulationViewModel>();
        services.TryAddSingleton<ExitViewModel>();
        services.TryAddSingleton<FullParametersListTabViewModel>();
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

        return serviceProvider;
    }
}
