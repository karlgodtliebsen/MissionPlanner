using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

using MissionPlanner.Views.Connect;
using MissionPlanner.Views.MenuConfigTuning;
using MissionPlanner.Views.MenuFlightData;
using MissionPlanner.Views.MenuFlightPlanner;
using MissionPlanner.Views.MenuHelp;
using MissionPlanner.Views.MenuInitSetup;
using MissionPlanner.Views.MenuSimulation;

using MainPageViewModel = MissionPlanner.Views.MainPageViewModel;

namespace MissionPlanner.Configuration;

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
    public static IServiceCollection AddApplicationConfiguration(IServiceCollection services, IConfiguration configuration)
    {
        //TODO: add app-settings/config file
        //ApplicationOptions? applicationOptions = configuration.GetSection(ApplicationOptions.SectionName).Get<ApplicationOptions>();
        //ArgumentNullException.ThrowIfNull(applicationOptions, ApplicationOptions.Template);


        ApplicationOptions applicationOptions = new();

        // Configure ApplicationOptions using the options pattern
        services.Configure<ApplicationOptions>(options =>
        {
            options.BaudRate = applicationOptions.BaudRate;
            options.ConnectionType = applicationOptions.ConnectionType;
            options.Port = applicationOptions.Port;
        });

        ApplicationState state = new()
        {
            SelectedBaudRate = applicationOptions.BaudRate,
            SelectedConnectionType = applicationOptions.ConnectionType,
            SelectedPort = applicationOptions.Port
        };

        // Configure ApplicationState using the options pattern (for initial values)
        services.Configure<ApplicationState>(options =>
        {
            options.SelectedBaudRate = state.SelectedBaudRate;
            options.SelectedConnectionType = state.SelectedConnectionType;
            options.SelectedPort = state.SelectedPort;
        });

        // Register shared state service as singleton for runtime state management
        var stateService = new ApplicationStateService();
        stateService.Initialize(state);
        services.AddSingleton(stateService);

        services
            .AddViewsConfiguration()
            ;


        return services;
    }

    private static IServiceCollection AddViewsConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<App>();
        services.AddSingleton<AppShell>();

        services.AddTransient<MainPage>();
        services.TryAddSingleton<MainPageViewModel>();
        services.TryAddSingleton<ConnectPopup>();
        services.TryAddSingleton<ConnectPopupViewModel>();

        //SubView/Controls
        services.TryAddSingleton<MenuFlightDataViewModel>();
        services.TryAddSingleton<MenuFlightDataView>();

        services.TryAddSingleton<MenuFlightPlannerViewModel>();
        services.TryAddSingleton<MenuFlightPlannerView>();

        services.TryAddSingleton<MenuInitSetupViewModel>();
        services.TryAddSingleton<MenuInitSetupView>();

        services.TryAddSingleton<MenuConfigTuningViewModel>();
        services.TryAddSingleton<MenuConfigTuningView>();

        services.TryAddSingleton<MenuSimulationViewModel>();
        services.TryAddSingleton<MenuSimulationView>();

        services.TryAddSingleton<MenuHelpViewModel>();
        services.TryAddSingleton<MenuHelpView>();


        return services;
    }


    public static IServiceProvider UseDispatchApp(this IServiceProvider serviceProvider)
    {
        ILogger<ApplicationOptions> logger = serviceProvider.GetRequiredService<ILogger<ApplicationOptions>>();
        logger.LogInformation("UseDispatchApp - Setting up Dispatch Messaging");
        //Instantiation Activates subscriptions
        //serviceProvider.GetRequiredService<InformationViewModel>();
        //serviceProvider.GetRequiredService<DispatchServerSentEventsMessagesHub>().SetupSubscriptions();
        return serviceProvider;
    }
}