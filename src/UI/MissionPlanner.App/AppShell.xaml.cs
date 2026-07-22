using MissionPlanner.App.Configuration;
using MissionPlanner.App.Views.ConfigTuning;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.App;

/// <summary>The main application Shell and guarded workspace navigation host.</summary>
public partial class AppShell : Shell
{
    private static readonly HashSet<string> configRoutes =
    [
        "ConfigGeoFence",
        "ConfigBasicTuning",
        "ConfigExtendedTuning",
        "ConfigOnboardOSD",
        "ConfigMavFtp",
        "ConfigFullParameters",
        "ConfigPlanner",
        "ConfigCubeLan8PortSwitch"
    ];
    private readonly IConfigNavigationGuard navigationGuard;
    private readonly ILogger<AppShell> logger;

    /// <summary>Initializes the main application Shell.</summary>
    public AppShell()
    {
        InitializeComponent();
        navigationGuard = ServiceHelper.GetRequiredService<IConfigNavigationGuard>();
        logger = ServiceHelper.GetRequiredService<ILogger<AppShell>>();
        Navigating += OnNavigating;
    }

    private async void OnNavigating(object? sender, ShellNavigatingEventArgs args)
    {
        if (!args.CanCancel || !IsConfigLocation(args.Current?.Location))
        {
            return;
        }

        var deferral = args.GetDeferral();
        try
        {
            if (!await navigationGuard.CanNavigateAsync(IsConfigLocation(args.Target.Location)))
            {
                args.Cancel();
            }
        }
        catch (OperationCanceledException)
        {
            args.Cancel();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Config navigation validation failed; navigation was cancelled.");
            args.Cancel();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static bool IsConfigLocation(Uri? location) =>
        location is not null && location.OriginalString
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(configRoutes.Contains);
}
