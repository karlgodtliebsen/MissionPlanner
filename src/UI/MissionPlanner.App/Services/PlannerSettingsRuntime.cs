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
    }

    /// <summary>Applies a temporary theme preview without persisting it.</summary>
    /// <param name="themeId">The theme or selection-policy identifier to preview.</param>
    public void PreviewTheme(string themeId)
    {
        _ = ApplyThemeSafelyAsync(themeId, true);
    }

    /// <summary>Applies the current safe live settings after MAUI application creation.</summary>
    /// <returns>A task that completes after the initial theme is fully applied.</returns>
    public Task ApplyCurrentAsync()
    {
        return ApplyAsync(settingsService.Current);
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

    private void OnSettingsChanged(PlannerSettingsChangedEventArgs e)
    {
        _ = ApplySafelyAsync(e.Current);
    }

    private async Task ApplyAsync(PlannerSettings settings)
    {
        if (!applicationState.IsConnected)
        {
            applicationState.SelectedChannel = settings.Connection.Channel;
            applicationState.SelectedHost = settings.Connection.Host;
            applicationState.SelectedPort = settings.Connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
            applicationState.SelectedBaudRate = settings.Connection.BaudRate.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        await themeManager.ApplyAsync(settings.Appearance.ThemeId).ConfigureAwait(false);
    }

    private async Task ApplySafelyAsync(PlannerSettings settings)
    {
        try
        {
            await ApplyAsync(settings).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Applying Planner runtime settings failed.");
        }
    }

    private async Task ApplyThemeSafelyAsync(string themeId, bool preview)
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
