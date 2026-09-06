using Avalonia;
using Avalonia.Media;
using MissionPlanner.App;
using MissionPlanner.Library.Windows.Configuration;

namespace MissionPlanner;

internal sealed class Program
{
    //    // Initialization code. Don't use any Avalonia, third-party APIs or any
    //    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    //    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        MissionPlannerProgram.Start(args);

        var app = MissionPlannerProgram.BuildAvaloniaApp((sc) => sc.AddWindowsOnlyServices())
            .UseWindowsPlatform()
            .With(new FontManagerOptions
            {
                FontFallbacks =
                [
                    new FontFallback { FontFamily = new FontFamily("Microsoft YaHei") }
                ]
            });

        app
            .With(new Win32PlatformOptions())
            .StartWithClassicDesktopLifetime(args);
    }
}
