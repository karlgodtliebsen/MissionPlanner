using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Configuration;
using Mopups.Hosting;
using SkiaSharp.Views.Maui.Controls.Hosting;
using UraniumUI;

namespace MissionPlanner.App;

/// <summary>
/// Extension methods for configuring the MauiAppBuilder.
/// </summary>
public static class MauiProgramExtensions
{
    /// <summary>
    /// Configures the MauiAppBuilder with shared settings and services.
    /// </summary>
    /// <param name="builder">The MauiAppBuilder instance to configure.</param>
    /// <returns>The configured MauiAppBuilder instance.</returns>
    public static MauiAppBuilder UseSharedMauiApp(this MauiAppBuilder builder)
    {
        List<IConfigurationSource> configurationSources = [new JsonConfigurationSource { Path = "appsettings.json", Optional = false, ReloadOnChange = true }];

        var configurationBuilder = builder.Configuration;

        foreach (var source in configurationSources)
        {
            configurationBuilder.Sources.Add(source);
        }

        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(configurationBuilder.GetSection("Logging"));
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
        builder.Logging.AddFilter("System", LogLevel.Warning);
        //builder.Logging.AddConsole();
        //builder.Logging.Services.AddSingleton<ILoggerProvider, BufferedLoggerProvider>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseUraniumUI()
            .UseUraniumUIMaterial()
            .UseUraniumUIBlurs(false)
            .ConfigureMopups()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFontAwesomeIconFonts();
                //fonts.AddMaterialSymbolsFonts();
                //fonts.AddFluentIconFonts();
            });


        builder.Services
            .AddApplicationConfiguration(builder.Configuration)
            .AddCommunityToolkitDialogs()
            .AddMopupsDialogs();

        return builder;
    }
}
