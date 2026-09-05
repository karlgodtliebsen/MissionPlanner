using Avalonia;
using Avalonia.Browser;
using Microsoft.Extensions.Configuration;
using MissionPlanner.Library.Browser.Configuration;
using MissionPlanner.App;

internal sealed partial class Program
{

    public static async Task Main(string[] args)
    {
        MissionPlannerProgram.Main(args);
        Mapsui.Logging.Logger.LogDelegate = (level, message, exception) =>
        {
            if (level is Mapsui.Logging.LogLevel.Warning or Mapsui.Logging.LogLevel.Error)
                Console.WriteLine($"Mapsui {level}: {message} {exception}");
        };
        using var settings = typeof(Program).Assembly.GetManifestResourceStream("MissionPlanner.Browser.appsettings.json")
            ?? throw new InvalidOperationException("Embedded browser configuration is missing.");
        var configuration = new ConfigurationBuilder().AddJsonStream(settings).Build();
        var app = await MissionPlannerProgram.BuildAvaloniaAppAsync(
            services => services.AddBrowserOnlyServices(), configuration);
        await app.StartBrowserAppAsync("out");
    }

}
