using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;
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

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // The previewer discovers this method on the executable's entry-point class.
    // Design mode loads resources without starting runtime services.
    public static AppBuilder BuildAvaloniaApp()
    {
        var app = (Design.IsDesignMode
                ? MissionPlanner.App.Program.BuildAvaloniaApp()
                : MissionPlannerProgram.BuildAvaloniaApp(sc => sc.AddWindowsOnlyServices()))
            .UseWindowsPlatform()
            .With(new FontManagerOptions
            {
                FontFallbacks =
                [
                    new FontFallback { FontFamily = new FontFamily("Microsoft YaHei") }
                ]
            });

        return app.With(new Win32PlatformOptions());
    }
}
