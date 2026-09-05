using MissionPlanner.App;
using MissionPlanner.App.Configuration;

namespace MissionPlanner.Mac;

/// <summary>
/// Provides the public API for MauiProgram.
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// Provides the public API for CreateMauiApp.
    /// </summary>
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
