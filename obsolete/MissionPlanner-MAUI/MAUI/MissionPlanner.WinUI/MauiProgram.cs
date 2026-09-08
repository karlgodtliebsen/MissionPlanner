using MissionPlanner.App;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MissionPlanner.WinUI;

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
        var builder = MauiApp.CreateBuilder();

        builder
            .UseSharedMauiApp();
        builder.Services.Replace(
            ServiceDescriptor.Singleton<IWindowTitleBarThemeService, WinUiWindowTitleBarThemeService>());
        var host = builder.Build();
        host.Services.UseApplication();
        return host;
    }
}
