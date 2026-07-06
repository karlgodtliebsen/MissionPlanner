using MissionPlanner.App;
using MissionPlanner.App.Configuration;

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
        var host = builder.Build();
        host.Services.UseApplication();
        return host;
    }
}
