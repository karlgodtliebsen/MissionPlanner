using MissionPlanner.App;
using MissionPlanner.App.Configuration;

namespace MissionPlanner.Mac;

public static class MauiProgram
{
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
