using Microsoft.Extensions.Logging;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Theming;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.App.Services;

/// <summary>Applies safe live Planner settings to application runtime state.</summary>
public sealed class PlannerSettingsRuntime : IDisposable
{
    private readonly IPlannerSettingsService settingsService;
    private readonly ApplicationStateService applicationState;
    private readonly IThemeManager themeManager;
    private readonly ILogger<PlannerSettingsRuntime> logger;
    private bool disposed;

    /// <summary>Initializes and subscribes the runtime settings bridge.</summary>
    public PlannerSettingsRuntime(
        IPlannerSettingsService settingsService,
        ApplicationStateService applicationState,
        IThemeManager themeManager,
        ILogger<PlannerSettingsRuntime> logger)
    {
        this.settingsService = settingsService;
        this.applicationState = applicationState;
        this.themeManager = themeManager;
        this.logger = logger;
        settingsService.SettingsChanged += OnSettingsChanged;
        Apply(settingsService.Current);
    }

    /// <summary>Applies a temporary theme preview without persisting it.</summary>
    /// <param name="themeId">The theme or selection-policy identifier to preview.</param>
    public void PreviewTheme(string themeId)
    {
        _ = ApplyThemeAsync(themeId, true);
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
        _ = ApplyThemeAsync(settings.Appearance.ThemeId, false);
        if (!applicationState.IsConnected)
        {
            applicationState.SelectedChannel = settings.Connection.Channel;
            applicationState.SelectedHost = settings.Connection.Host;
            applicationState.SelectedPort = settings.Connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            applicationState.SelectedBaudRate = settings.Connection.BaudRate.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private async Task ApplyThemeAsync(string themeId, bool preview)
    {
        try
        {
            if (preview)
            {
                await themeManager.PreviewAsync(themeId).ConfigureAwait(false);
            }
            else
            {
                await themeManager.ApplyAsync(themeId).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Applying Planner theme {ThemeId} failed.", themeId);
        }
    }
}
