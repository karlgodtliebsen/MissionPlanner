using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Dialogs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Configuration;
using MissionPlanner.Library;

namespace MissionPlanner.App;


[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public class MissionPlannerProgram
{
    private const string title = "MissionPlanner Next Generation";
    private const string appName = "MissionPlanner";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    //[STAThread]
    public static void Main(string[] args)
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
        IServiceCollection services = new ServiceCollection();
        services.AddApplicationConfiguration(configurationBuilder.Build());
        serviceAction.Invoke(services);
        services.AddSingleton(cancellationTokenSource);
        serviceProvider = services.BuildServiceProvider();
        DomainException.ThrowIfNull(serviceProvider);
        serviceProvider.UseApplication();

        return AppBuilder
            .Configure(() => new App(serviceProvider))
                .UseManagedSystemDialogs()
                .WithDataAnnotationsValidation()
                .UsePlatformDetect()
                .WithInterFont()
#if DEBUG
                .WithDeveloperTools()
#endif
                .LogToTrace();
    }
}
