using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.App.AppViewModels;
using MissionPlanner.App.Views.Common;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.App.Views.ConfigTuning.Tabs;
using MissionPlanner.App.Views.Connect;
using MissionPlanner.App.Views.Dashboard;
using MissionPlanner.App.Views.Exit;
using MissionPlanner.App.Views.FlightData;
using MissionPlanner.App.Views.FlightData.Hud;
using MissionPlanner.App.Views.FlightData.Map;
using MissionPlanner.App.Views.FlightData.Tabs;
using MissionPlanner.App.Views.FlightPlanner;
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
        services.TryAddSingleton<StatusBarView>();

        services.TryAddSingleton<DashboardPageViewModel>();
        services.TryAddSingleton<DashboardPage>();

        services.TryAddSingleton<TopBarViewModel>();
        services.TryAddSingleton<TopBarView>();

        services.TryAddSingleton<ExitViewModel>();
        services.TryAddSingleton<ExitView>();
        services.TryAddSingleton<ExitContentView>();

        //SubView/Controls

        services.TryAddTransient<ConnectPopupView>();
        services.TryAddTransient<ConnectPopupViewModel>();

        services.TryAddSingleton<FlightDataViewModel>();
        services.TryAddSingleton<ConnectionView>();

        services.TryAddSingleton<HudViewModel>();
        services.TryAddSingleton<HudView>();

        services.TryAddSingleton<FlightDataMapViewModel>();
        services.TryAddSingleton<FlightDataMapView>();

        services.TryAddSingleton<QuickTabViewModel>();
        services.TryAddSingleton<QuickTabView>();

        services.TryAddSingleton<FullParametersListTabViewModel>();
        services.TryAddSingleton<FullParametersListTabView>();

        services.TryAddSingleton<StatusTabViewModel>();
        services.TryAddSingleton<StatusTabView>();

        services.TryAddSingleton<FlightPlannerViewModel>();
        services.TryAddSingleton<FlightPlannerView>();

        services.TryAddSingleton<InitSetupViewModel>();
        services.TryAddSingleton<InitSetupView>();

        services.TryAddSingleton<MenuConfigTuningViewModel>();
        services.TryAddSingleton<MenuConfigTuningView>();

        services.TryAddSingleton<SimulationViewModel>();
        services.TryAddSingleton<SimulationView>();

        services.TryAddSingleton<ExitViewModel>();
        services.TryAddSingleton<ExitView>();

        //TryAddTransient
        services.TryAddSingleton<FullParametersListTabViewModel>();
        services.TryAddSingleton<FullParametersListTabView>();

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
