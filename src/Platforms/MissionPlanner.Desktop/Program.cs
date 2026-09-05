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
        MissionPlannerProgram.Main(args);

        var app = MissionPlannerProgram.BuildAvaloniaApp((sc) => sc.AddWindowsOnlyServices())
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


    //    app.StartWithClassicDesktopLifetime(args, lifetimeBuilder =>
    //    {
    //        lifetimeBuilder.ShutdownMode = ShutdownMode.OnLastWindowClose;
    //        GC.Collect();
    //        GC.WaitForPendingFinalizers();
    //    });

    //    app.StartWithClassicDesktopLifetime(args, lifetimeBuilder =>
    //    {
    //        lifetimeBuilder.ShutdownMode = ShutdownMode.OnLastWindowClose;
    //        lifetimeBuilder.Exit += async (_, exit) =>
    //        {
    //            //exit.ApplicationExitCode = 0;
    //            Log.Logger.Information("Shutting Down {title}", title);

    //            await cancellationTokenSource.CancelAsync();

    //            try
    //            {
    //                await host.StopAsync(cancellationTokenSource.Token);
    //            }
    //            catch (Exception ex)
    //            {
    //                Log.Logger.Error(ex, "Error during host shutdown");
    //            }

    //            GC.Collect();
    //            GC.WaitForPendingFinalizers();
    //        };
    //    });

}
