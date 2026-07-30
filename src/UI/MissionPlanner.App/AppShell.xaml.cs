using Microsoft.Extensions.Logging;
using MissionPlanner.App.Helpers;
using MissionPlanner.App.Helpers.Navigation;
using MissionPlanner.App.Navigation;
using MissionPlanner.App.Views.ConfigTuning;

namespace MissionPlanner.App;

/// <summary>The main application Shell and guarded workspace navigation host.</summary>
public partial class AppShell : Shell
{
    private readonly IConfigNavigationGuard navigationGuard;
    private readonly ILogger<AppShell> logger;
    private readonly INavigationEventHub navigationEventHub;

    /// <summary>Initializes the main application Shell.</summary>
    public AppShell()
    {
        InitializeComponent();
        navigationGuard = ServiceHelper.GetRequiredService<IConfigNavigationGuard>();
        logger = ServiceHelper.GetRequiredService<ILogger<AppShell>>();

        navigationEventHub = ServiceHelper.GetRequiredService<INavigationEventHub>();

        Navigating += OnNavigating;
        Navigated += OnNavigated;
    }

    private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        var previous = e.Current?.Location.ToString();
        var current = e.Target?.Location.ToString();
        navigationEventHub.Publish(new NavigatingEvent(previous, current, e));
    }

    private void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        var previous = e.Previous?.Location.ToString();
        var current = e.Current?.Location.ToString();
        navigationEventHub.Publish(new NavigatedEvent(previous, current, e));
    }
}
