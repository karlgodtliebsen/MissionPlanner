using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Helpers;
using MissionPlanner.App.Services;
using MissionPlanner.Core.ConfigTuning.Planner;
using ShellItem = Microsoft.Maui.Controls.ShellItem;

namespace MissionPlanner.App.AppViewModels;

/// <summary>
/// ViewModel for the application's shell content.
/// </summary>
public partial class AppShellContentViewModel : ObservableObject
{
    private readonly IPlannerSettingsService settingsService = null!;
    private readonly ILogger<AppShellContentViewModel> logger = null!;
    private readonly PlannerSettingsRuntime runtime = null!;
    private bool synchronizing;
    private bool disposed;

    [ObservableProperty] public partial Shell CurrentShell { get; set; } = null!;
    [ObservableProperty] public partial ReadOnlyObservableCollection<ShellItem> Items { get; set; } = null!;
    [ObservableProperty] public partial ShellItem? SelectedItem { get; set; } = null!;
    [ObservableProperty] public partial FlyoutBehavior FlyoutBehavior { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the flyout menu is currently presented in the UI.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFlyoutPresented { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tutorial is currently presented in the UI.
    /// </summary>
    [ObservableProperty]
    public partial bool IsTutorialPresented { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the flyout menu is locked in the UI.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFlyoutLocked { get; set; }

    /// <summary>
    /// Gets the system, light, and dark theme choices.
    /// </summary>
    public AppTheme[] AppThemeList { get; } = [AppTheme.Light, AppTheme.Dark];

    /// <summary>
    /// Gets the selected MAUI application theme.
    /// </summary>
    [ObservableProperty]
    public partial AppTheme SelectedTheme { get; set; }

    /// <summary>
    /// Application preferences that are not persisted in the settings file, but are used to control the UI and behavior of the application.
    /// </summary>
    [ObservableProperty]
    public partial bool PreferDarkTheme { get; set; }

    private readonly IDispatcher dispatcher;

    /// <inheritdoc />
    public AppShellContentViewModel()
    {
        dispatcher = ServiceHelper.GetRequiredService<IDispatcher>();

        settingsService = ServiceHelper.GetRequiredService<IPlannerSettingsService>();
        runtime = ServiceHelper.GetRequiredService<PlannerSettingsRuntime>();
        logger = ServiceHelper.GetRequiredService<ILogger<AppShellContentViewModel>>();
        synchronizing = true;
        IsFlyoutLocked = settingsService.Current.Appearance.IsFlyoutLocked;
        IsFlyoutPresented = settingsService.Current.Appearance.IsFlyoutPresented;
        IsTutorialPresented = settingsService.Current.Appearance.IsTutorialPresented;
        FlyoutBehavior = settingsService.Current.Appearance.IsFlyoutLocked ? FlyoutBehavior.Locked : FlyoutBehavior.Flyout;
        PreferDarkTheme = settingsService.Current.Appearance.PreferDarkTheme;
        SelectedTheme = ToAppTheme(settingsService.Current.Appearance.Theme);
        synchronizing = false;
        settingsService.SettingsChanged += OnSettingsChanged;
    }


    partial void OnPreferDarkThemeChanged(bool value)
    {
        if (synchronizing)
        {
            return;
        }

        if (value != settingsService.Current.Appearance.PreferDarkTheme)
        {
            AppTheme selected;
            synchronizing = true;
            try
            {
                settingsService.Current.Appearance.PreferDarkTheme = value;
                selected = ToAppTheme(settingsService.Current.Appearance.Theme);
            }
            finally
            {
                synchronizing = false;
            }

            if (SelectedTheme != selected)
            {
                SelectedTheme = selected;
            }
        }
    }


    partial void OnIsFlyoutLockedChanged(bool value)
    {
        if (synchronizing)
        {
            return;
        }

        settingsService.Current.Appearance.IsFlyoutLocked = value;
        _ = PersistAsync();
    }

    partial void OnIsTutorialPresentedChanged(bool value)
    {
        if (synchronizing)
        {
            return;
        }

        settingsService.Current.Appearance.IsTutorialPresented = value;
        _ = PersistAsync();
    }

    partial void OnIsFlyoutPresentedChanged(bool value)
    {
        if (synchronizing)
        {
            return;
        }

        settingsService.Current.Appearance.IsFlyoutPresented = value;
        _ = PersistAsync();
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
        if (synchronizing)
        {
            return;
        }

        synchronizing = true;
        try
        {
            var flock = e.Current.Appearance.IsFlyoutLocked;
            var fpres = e.Current.Appearance.IsFlyoutPresented;
            var isTut = e.Current.Appearance.IsTutorialPresented;
            if (flock != IsFlyoutLocked || fpres != IsFlyoutPresented || isTut != IsTutorialPresented)
            {
                IsFlyoutLocked = flock;
                IsFlyoutPresented = fpres;
                FlyoutBehavior = settingsService.Current.Appearance.IsFlyoutLocked ? FlyoutBehavior.Locked : FlyoutBehavior.Flyout;
                IsTutorialPresented = isTut;
                _ = PersistAsync();
            }

            var prefer = e.Current.Appearance.PreferDarkTheme;
            if (prefer != PreferDarkTheme)
            {
                PreferDarkTheme = prefer;
            }

            var selected = ToAppTheme(e.Current.Appearance.Theme);
            if (SelectedTheme != selected)
            {
                SelectedTheme = selected;
            }
        }
        finally
        {
            synchronizing = false;
        }
    }


    private AppTheme ToAppTheme(PlannerTheme theme)
    {
        return PreferDarkTheme
            ? AppTheme.Dark
            : theme switch
            {
                PlannerTheme.Light => AppTheme.Light,
                PlannerTheme.Dark => AppTheme.Dark,
                var _ => AppTheme.Unspecified
            };
    }

    private PlannerTheme ToPlannerTheme(AppTheme theme)
    {
        return PreferDarkTheme
            ? PlannerTheme.Dark
            : theme switch
            {
                AppTheme.Light => PlannerTheme.Light,
                AppTheme.Dark => PlannerTheme.Dark,
                var _ => PlannerTheme.System
            };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        settingsService.SettingsChanged -= OnSettingsChanged;
        disposed = true;
    }

    private async Task PersistThemeAsync(PlannerTheme theme)
    {
        try
        {
            await settingsService.SaveTheme(settingsService.Current, theme, PreferDarkTheme);
        }
        catch (Exception exception)
        {
            Debug.Print(exception.Message);
            logger.LogError(exception, "Could not persist the application theme preference.");
        }
    }

    private async Task PersistAsync()
    {
        if (synchronizing)
        {
            return;
        }

        try
        {
            synchronizing = true;
            await settingsService.SaveFlyout(settingsService.Current, IsFlyoutPresented, IsFlyoutLocked, IsTutorialPresented);
        }
        catch (Exception exception)
        {
            Debug.Print(exception.Message);
            logger.LogError(exception, "Could not persist the application Flyout preference.");
        }
        finally
        {
            synchronizing = false;
        }
    }


    partial void OnCurrentShellChanged(Shell value)
    {
        if (synchronizing)
        {
            return;
        }

        value.Loaded += CurrentShell_Loaded;
    }

    private void CurrentShell_Loaded(object? sender, EventArgs e)
    {
        CurrentShell.Loaded -= CurrentShell_Loaded;
        Items = new ReadOnlyObservableCollection<ShellItem>(new ObservableCollection<ShellItem>(CurrentShell.Items));
        if (settingsService.Current.Appearance.IsTutorialPresented)
        {
            var pendingNavigation = "Tutorial";
            var item = CurrentShell.Items.FirstOrDefault(i => i.Title == pendingNavigation) ?? CurrentShell.Items.FirstOrDefault();
            var selectedItem = item ?? CurrentShell.Items.FirstOrDefault();
            _ = Task.Run(() => SetItem(selectedItem));
        }
        else
        {
            var selectedItem = CurrentShell.Items.FirstOrDefault();
            _ = Task.Run(() => SetItem(selectedItem));
        }
        //SelectedItem = CurrentShell.Items.FirstOrDefault();
    }

    private void SetItem(ShellItem? item)
    {
        if (item is not null)
        {
            dispatcher.Dispatch(() => SelectedItem = item);
        }
    }

    [RelayCommand]
    private void CloseFlyout()
    {
        IsFlyoutPresented = false;
    }

    [RelayCommand]
    private void ExpandFlyout()
    {
        IsFlyoutPresented = true;
    }

    partial void OnSelectedItemChanged(ShellItem? value)
    {
        if (CurrentShell.CurrentItem != value)
        {
            CurrentShell.CurrentItem = value;
            return;
        }
    }
}
