using Avalonia.Browser;
using Microsoft.Extensions.Configuration;
using MissionPlanner.App;
using MissionPlanner.Library.Browser.Configuration;

internal sealed partial class Program
{

    public static async Task Main(string[] args)
    {
        MissionPlannerProgram.Start(args);
        Mapsui.Logging.Logger.LogDelegate = (level, message, exception) =>
        {
            if (level is Mapsui.Logging.LogLevel.Warning or Mapsui.Logging.LogLevel.Error)
            {
                Console.WriteLine($"Mapsui {level}: {message} {exception}");
            }
        };
        using var settings = typeof(Program).Assembly.GetManifestResourceStream("MissionPlanner.Browser.appsettings.json")
            ?? throw new InvalidOperationException("Embedded browser configuration is missing.");
        var configuration = new ConfigurationBuilder().AddJsonStream(settings).Build();
        var app = await MissionPlannerProgram.BuildAvaloniaAppAsync(
            services => services.AddBrowserOnlyServices(), configuration);
        await app.StartBrowserAppAsync("out");
    }

}
