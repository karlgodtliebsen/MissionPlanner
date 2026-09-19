using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Media;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Configuration;
using MissionPlanner.Library;

namespace MissionPlanner.App;


[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
[SupportedOSPlatform("browser")]
public static class MissionPlannerProgram
{
    private const string title = "MissionPlanner Next Generation";
    private const string appName = "MissionPlanner";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    //[STAThread]
    public static void Start(string[] args)
    {
        var ci = new CultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = ci;
        CultureInfo.DefaultThreadCurrentCulture = ci;
        Thread.CurrentThread.CurrentUICulture = ci;
        Thread.CurrentThread.CurrentCulture = ci;
        ApplicationRunner.SetAppDomainExceptionHandling(title);
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? null;
        Debug.Print("Using Environment " + environment);
    }


    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp(Action<IServiceCollection> serviceAction)
    {
        return BuildAvaloniaAppAsync(serviceAction).GetAwaiter().GetResult();
    }

    public static async Task<AppBuilder> BuildAvaloniaAppAsync(
        Action<IServiceCollection> serviceAction, IConfiguration? configuration = null)
    {
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
        IServiceCollection services = new ServiceCollection();
        serviceAction.Invoke(services);
        services.AddApplicationConfiguration(configuration ?? configurationBuilder.Build());
        services.AddSingleton(cancellationTokenSource);
        IServiceProvider serviceProvider = services.BuildServiceProvider();
        DomainException.ThrowIfNull(serviceProvider);
        await serviceProvider.UseApplicationAsync();

        var builder = AppBuilder
            .Configure(() => new App(serviceProvider))
                .WithDataAnnotationsValidation()
                .WithInterFont()
                .LogToTrace();
        return builder;
    }
}

// Shared resource-only builder used by the Desktop preview host. This project is
// a library: the previewer discovers BuildAvaloniaApp on the Desktop entry point.
public class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp()
            .With(new FontManagerOptions
            {
                FontFallbacks =
                [
                    new FontFallback
                    {
                        FontFamily = new FontFamily("Microsoft YaHei")
                    }
                ]
            })
            .StartWithClassicDesktopLifetime(args);
    }


    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure(() => new App())
            .WithInterFont()
            .LogToTrace();
    }
}

