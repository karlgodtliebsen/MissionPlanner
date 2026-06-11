using CommunityToolkit.Maui;

using Microsoft.Extensions.Logging;

using MissionPlanner.Configuration;

namespace MissionPlanner;

/// <summary>
/// 
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        IServiceCollection services = builder.Services;
        ApplicationConfigurator.AddApplicationConfiguration(services, builder.Configuration);

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}