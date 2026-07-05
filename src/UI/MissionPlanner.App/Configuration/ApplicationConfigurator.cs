using Domain.Library;
using Domain.Library.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.App.AppViewModels;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.App.Views.Connect;
using MissionPlanner.App.Views.Dashboard;
using MissionPlanner.App.Views.FlightData;
using MissionPlanner.App.Views.FlightData.Hud;
using MissionPlanner.App.Views.FlightData.Map;
using MissionPlanner.App.Views.FlightData.Tabs;
using MissionPlanner.App.Views.FlightPlanner;
using MissionPlanner.App.Views.Help;
using MissionPlanner.App.Views.InitSetup;
using MissionPlanner.App.Views.Simulation;
using MissionPlanner.Core.Configuration;
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

        // ApplicationOptions applicationOptions = new();

        services.AddSingleton(Options.Create(applicationOptions));


        // Configure ApplicationOptions using the options pattern
        //services.Configure<ApplicationOptions>(options =>
        //{
        //    options.BaudRate = applicationOptions.BaudRate;
        //    options.ConnectionType = applicationOptions.ConnectionType;
        //    options.Port = applicationOptions.Port;
        //});
        //// Configure ApplicationState using the options pattern (for initial values)
        //services.Configure<ApplicationState>(options =>
        //{
        //    options.SelectedBaudRate = state.SelectedBaudRate;
        //    options.SelectedConnectionType = state.SelectedConnectionType;
        //    options.SelectedPort = state.SelectedPort;
        //});

        ApplicationState state = new() { SelectedBaudRate = applicationOptions.BaudRate, SelectedConnectionType = applicationOptions.ConnectionType, SelectedPort = applicationOptions.Port };
        // Register shared state service as singleton for runtime state management
        ApplicationStateService stateService = new();
        stateService.Initialize(state);
        services.TryAddSingleton(stateService);


        //services.TryAddSingleton<Views.Vehicles.Views.ModelMapper>();
        services.TryAddSingleton<ThemeChangeViewModel>();

        services.TryAddSingleton(new CancellationTokenSource());

        services
            .AddLibraryServices()
            .AddDomainServices(configuration)
            .AddMavLinkTransportServices(configuration)
            .AddMavLinkServices(configuration)
            .AddLogging(configuration, (s, l, c) =>
            {
                /*Customize logging*/
            })
            .AddViewsConfiguration();

        return services;
    }

    private static IServiceCollection AddViewsConfiguration(this IServiceCollection services)
    {
        services.TryAddSingleton<App>();
        services.TryAddSingleton<AppShell>();

        services.TryAddSingleton<DashboardPageViewModel>();
        services.TryAddSingleton<DashboardPage>();

        //SubView/Controls
        services.TryAddSingleton<ConnectPopupView>();
        services.TryAddSingleton<ConnectPopupViewModel>();

        services.TryAddSingleton<ConnectionView>();
        services.TryAddSingleton<ConnectionViewModel>();

        //SubView/Controls
        services.TryAddSingleton<FlightDataViewModel>();
        services.TryAddSingleton<ConnectionView>();

        services.TryAddSingleton<HudViewModel>();
        services.TryAddSingleton<HudView>();

        services.TryAddSingleton<FlightDataMapViewModel>();
        services.TryAddSingleton<FlightDataMapView>();

        services.TryAddSingleton<QuickTabViewModel>();
        services.TryAddSingleton<QuickTabView>();

        services.TryAddSingleton<ActionsTabViewModel>();
        services.TryAddSingleton<ActionsTabView>();

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

        services.TryAddSingleton<HelpViewModel>();
        services.TryAddSingleton<HelpView>();


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

        //var endPoint = serviceProvider.GetRequiredService<IOptions<TransportEndpoint>>();

        //endPoint.Value.RemoteHost = "127.0.0.1";
        //endPoint.Value.RemotePort = 14551;

        //endPoint.Value.LocalHost = "0.0.0.0";
        //endPoint.Value.LocalPort = 14550;
        //logger.LogInformation($"Console configuration initialized. UDP local:  {endPoint.Value.LocalHost}:{endPoint.Value.LocalPort}");
        //logger.LogInformation($"Console configuration initialized. UDP remote: {endPoint.Value.RemoteHost}:{endPoint.Value.RemotePort}");

        //serviceProvider.GetRequiredService<MessagesPageViewModel>().SetupSubscriptions();
        //serviceProvider.GetRequiredService<DashboardPageViewModel>().SetupSubscriptions();

        //serviceProvider.GetRequiredService<DashboardPage>();
        //serviceProvider.GetRequiredService<SettingsPage>();
        //serviceProvider.GetRequiredService<MessagesPage>();


        //serviceProvider.GetRequiredService<Views.Vehicles.VehiclesPage>();
        //serviceProvider.GetRequiredService<VehiclesView>();

        //serviceProvider
        //    .UseMavLinkServices()
        //    .UseDomainServices()
        //    ;

        return serviceProvider;
    }
}
