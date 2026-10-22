using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MissionPlanner.App;
using MissionPlanner.Library.MacOs.Configuration;

namespace MissionPlanner;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        MissionPlannerProgram.Start(args);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // The previewer discovers this method on the executable's entry-point class.
    // Design mode loads resources without starting runtime services.
    public static AppBuilder BuildAvaloniaApp()
    {
        return (Design.IsDesignMode
                ? MissionPlanner.App.Program.BuildAvaloniaApp()
                : MissionPlannerProgram.BuildAvaloniaApp(sc => sc.AddMacOsOnlyServices()))
            .UseMacOsPlatform()
            .With(new FontManagerOptions
            {
                FontFallbacks =
                [
                    new FontFallback { FontFamily = new FontFamily("PingFang SC") }
                ]
            });
    }
}
