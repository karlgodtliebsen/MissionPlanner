using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Storage;
using Microsoft.Extensions.Logging;
using Mopups.Hosting;
using UraniumUI.Material.Extensions.Samples.AppViewModels;
using UraniumUI.Material.Extensions.Samples.Models;

namespace UraniumUI.Material.Extensions.Samples;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseUraniumUI()
            .UseUraniumUIMaterial()
            .ConfigureMopups()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

                fonts.AddMaterialSymbolsFonts();
            });

        builder.Services.AddMopupsDialogs();

        builder.Logging.AddDebug();
        builder.Services.AddLogging(configure => configure.AddDebug());

        builder.Services.AddSingleton<AppShellContentViewModel>();
        builder.Services.AddSingleton<ThemeChangeViewModel>();
        builder.Services.AddSingleton<ParametersFileHandler>();
        builder.Services.AddSingleton<IFileSaver>(FileSaver.Default);
        builder.Services.AddSingleton<VirtualizedDataGridViewModel>();

        //builder.Services.AddTransientWithShellRoute<ProjectDetailPage, ProjectDetailPageModel>("project");
        //builder.Services.AddTransientWithShellRoute<TaskDetailPage, TaskDetailPageModel>("task");

        builder.Services.AddCommunityToolkitDialogs();

        return builder.Build();
    }
}

/// <summary>
/// Helper class for retrieving services from the MAUI application's service provider.
/// </summary>
public static class ServiceHelper
{
    /// <summary>
    /// Retrieves a required service from the MAUI application's service provider.
    /// </summary>
    /// <typeparam name="T">The type of the service to retrieve.</typeparam>
    /// <returns>The requested service.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the MAUI app is not initialized.</exception>
    public static T GetRequiredService<T>() where T : notnull
    {
        return IPlatformApplication.Current!.Services.GetRequiredService<T>()
               ?? throw new InvalidOperationException("MAUI app not initialized.");
    }
}
