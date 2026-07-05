using MissionPlanner.App;

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
        MauiAppBuilder builder = MauiApp.CreateBuilder();

        builder
            .UseSharedMauiApp();

        return builder.Build();
    }
}