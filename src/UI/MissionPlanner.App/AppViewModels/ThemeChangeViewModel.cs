using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Helpers;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.App.AppViewModels;

/// <summary>Synchronizes the Shell theme selector with versioned Planner preferences.</summary>
public sealed partial class ThemeChangeViewModel : ObservableObject, IDisposable
{
    private readonly IPlannerSettingsService settingsService;
    private readonly PlannerSettingsRuntime runtime;
    private readonly ILogger<ThemeChangeViewModel> logger;
    private bool synchronizing;
    private bool disposed;

    /// <summary>Initializes the theme selector.</summary>
    /// <param name="settingsService">The Planner settings service.</param>
    /// <param name="runtime">The live settings bridge.</param>
    /// <param name="logger">The logger.</param>
    public ThemeChangeViewModel(
        IPlannerSettingsService settingsService,
        PlannerSettingsRuntime runtime,
        ILogger<ThemeChangeViewModel> logger)
    {
        this.settingsService = settingsService;
        this.runtime = runtime;
        this.logger = logger;
        synchronizing = true;
        SelectedTheme = ToAppTheme(settingsService.Current.Appearance.Theme);
        synchronizing = false;
        settingsService.SettingsChanged += OnSettingsChanged;
    }

    /// <summary>Gets the system, light, and dark theme choices.</summary>
    public AppTheme[] AppThemeList { get; } = [AppTheme.Light, AppTheme.Dark];

    /// <summary>Gets the selected MAUI application theme.</summary>
    [ObservableProperty]
    public partial AppTheme SelectedTheme { get; set; }

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

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        runtime.PreviewTheme(ToPlannerTheme(value));
        if (!synchronizing)
        {
            _ = PersistThemeAsync(ToPlannerTheme(value));
        }
    }

    private void OnSettingsChanged(object? sender, PlannerSettingsChangedEventArgs e)
    {
        var selected = ToAppTheme(e.Current.Appearance.Theme);
        if (SelectedTheme == selected)
        {
            return;
        }

        synchronizing = true;
        try
        {
            SelectedTheme = selected;
        }
        finally
        {
            synchronizing = false;
        }
    }

    private async Task PersistThemeAsync(PlannerTheme theme)
    {
        try
        {
            await settingsService.SaveAsync(settingsService.Current with { Appearance = new PlannerAppearanceSettings { Theme = theme } });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not persist the application theme preference.");
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

    private static PlannerTheme ToPlannerTheme(AppTheme theme)
    {
        return theme switch
        {
            AppTheme.Light => PlannerTheme.Light,
            AppTheme.Dark => PlannerTheme.Dark,
            var _ => PlannerTheme.System
        };
    }
}
