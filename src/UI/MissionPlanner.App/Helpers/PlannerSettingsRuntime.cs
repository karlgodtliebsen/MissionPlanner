using MissionPlanner.App.Configuration;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.App.Helpers;

/// <summary>Applies safe live Planner settings to application runtime state.</summary>
public sealed class PlannerSettingsRuntime : IDisposable
{
    private readonly IPlannerSettingsService settingsService;
    private readonly ApplicationStateService applicationState;
    private bool disposed;

    /// <summary>Initializes and subscribes the runtime settings bridge.</summary>
    /// <param name="settingsService">The observable settings service.</param>
    /// <param name="applicationState">The shared connection UI state.</param>
    public PlannerSettingsRuntime(
        IPlannerSettingsService settingsService,
        ApplicationStateService applicationState)
    {
        this.settingsService = settingsService;
        this.applicationState = applicationState;
        settingsService.SettingsChanged += OnSettingsChanged;
        Apply(settingsService.Current);
    }

    /// <summary>Applies a temporary theme preview without persisting it.</summary>
    /// <param name="theme">The theme to preview.</param>
    public void PreviewTheme(PlannerTheme theme)
    {
        if (Application.Current is { } application)
        {
            application.UserAppTheme = ToAppTheme(theme);
        }
    }

    /// <summary>Reapplies the current safe live settings after MAUI application creation.</summary>
    public void ApplyCurrent()
    {
        Apply(settingsService.Current);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        settingsService.SettingsChanged -= OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, PlannerSettingsChangedEventArgs e)
    {
        Apply(e.Current);
    }

    private void Apply(PlannerSettings settings)
    {
        PreviewTheme(settings.Appearance.Theme);
        if (!applicationState.IsConnected)
        {
            applicationState.SelectedChannel = settings.Connection.Channel;
            applicationState.SelectedHost = settings.Connection.Host;
            applicationState.SelectedPort = settings.Connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            applicationState.SelectedBaudRate = settings.Connection.BaudRate.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private static AppTheme ToAppTheme(PlannerTheme theme)
    {
        return theme switch
        {
            PlannerTheme.Light => AppTheme.Light,
            PlannerTheme.Dark => AppTheme.Dark,
            var _ => AppTheme.Unspecified
        };
    }
}
