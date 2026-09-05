using System.Globalization;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Dialogs;
using Avalonia.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.AvaloniaUI.App.Configuration;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.Library;

namespace MissionPlanner.AvaloniaUI.App;


[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal class Program
{
    private const string title = "MissionPlanner Next Generation";
    private const string appName = "MissionPlanner";



    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var ci = new CultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = ci;
        CultureInfo.DefaultThreadCurrentCulture = ci;
        Thread.CurrentThread.CurrentUICulture = ci;
        Thread.CurrentThread.CurrentCulture = ci;

        ApplicationRunner.SetAppDomainExceptionHandling(title);

        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? null;

        var app = BuildAvaloniaApp()
        .With(new FontManagerOptions
        {
            FontFallbacks =
            [
                new FontFallback
                {
                    FontFamily = new FontFamily("Microsoft YaHei")
                }
            ]
        });

        app.StartWithClassicDesktopLifetime(args);

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


    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp(/*string[] args*/)
    {
        IServiceCollection? services = null;
        IServiceProvider? serviceProvider = null;

        List<IConfigurationSource> configurationSources = [new JsonConfigurationSource
        {
            Path = "appsettings.json", Optional = false, ReloadOnChange = true
        }];
        var configurationBuilder = new ConfigurationBuilder();
        foreach (var source in configurationSources)
        {
            configurationBuilder.Sources.Add(source);
        }
        var cancellationTokenSource = new CancellationTokenSource();
        services = new ServiceCollection();
        services.AddApplicationConfiguration(configurationBuilder.Build());
        services.AddSingleton(cancellationTokenSource);
        serviceProvider = services.BuildServiceProvider();
        DomainException.ThrowIfNull(serviceProvider);
        serviceProvider.UseApplication();

        return AppBuilder
            .Configure(() => new App(serviceProvider))
                .UseManagedSystemDialogs()
                .WithDataAnnotationsValidation()
                .UsePlatformDetect()
                .With(new Win32PlatformOptions())
                .WithInterFont()
#if DEBUG
                .WithDeveloperTools()
#endif
                .LogToTrace();
    }
}
