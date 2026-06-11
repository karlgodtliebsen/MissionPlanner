using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        //ApplicationOptions? applicationOptions = configuration.GetSection(ApplicationOptions.SectionName).Get<ApplicationOptions>();
        //ArgumentNullException.ThrowIfNull(applicationOptions, ApplicationOptions.Template);


        ApplicationOptions applicationOptions = new();

        ApplicationState state = new()
        {
            SelectedBaudRate = applicationOptions.BaudRate,
            SelectedConnectionType = applicationOptions.ConnectionType,
            SelectedPort = applicationOptions.Port
        };

        services.AddSingleton(Options.Create(applicationOptions));
        services.AddSingleton(Options.Create(state));
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