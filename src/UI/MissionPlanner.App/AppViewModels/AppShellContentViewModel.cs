using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Theming;
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
    private readonly IThemeManager themeManager = null!;
    private bool synchronizing;
    private bool disposed;

    [ObservableProperty]
    public partial Shell CurrentShell
    {
        get;
        set;
    } = null!;

    [ObservableProperty]
    public partial ReadOnlyObservableCollection<ShellItem> Items
    {
        get;
        set;
    } = null!;

    [ObservableProperty]
    public partial ShellItem? SelectedItem
    {
        get;
        set;
    } = null!;

    [ObservableProperty]
    public partial FlyoutBehavior FlyoutBehavior
    {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the flyout menu is currently presented in the UI.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFlyoutPresented
    {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the flyout menu is presented at startup in the UI.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFlyoutVisibleAtStartup
    {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the tutorial is currently presented in the UI.
    /// </summary>
    [ObservableProperty]
    public partial bool IsTutorialVisibleAtStartup
    {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the flyout menu is locked in the UI.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFlyoutLocked
    {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the flyout menu is locked in the UI.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFlyoutCollapseButtonVisible
    {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the flyout menu is locked in the UI.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFlyoutExpandButtonVisible
    {
        get;
        set;
    }


    /// <summary>
    /// Gets the installed application theme choices.
    /// </summary>
    public IReadOnlyList<ThemeOption> ThemeOptions => themeManager.AvailableThemes;

    /// <summary>
    /// Gets or sets the selected application theme.
    /// </summary>
    [ObservableProperty]
    public partial ThemeOption? SelectedThemeOption
    {
        get;
        set;
    }

    private readonly IDispatcher dispatcher = null!;

    /// <inheritdoc />
    public AppShellContentViewModel(
        IDispatcher dispatcher,
        IPlannerSettingsService settingsService,
        IThemeManager themeManager,
        ILogger<AppShellContentViewModel> logger)
    {
        this.dispatcher = dispatcher;
        this.settingsService = settingsService;
        this.themeManager = themeManager;
        this.logger = logger;
        SetState(settingsService.Current.Appearance);
        settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void SetState(PlannerAppearanceSettings currentAppearance)
    {
        if (synchronizing)
        {
            return;
        }

        synchronizing = true;
        try
        {
            IsFlyoutLocked = currentAppearance.IsFlyoutLocked;
            IsFlyoutVisibleAtStartup = currentAppearance.IsFlyoutVisibleAtStartup;
            if (IsFlyoutVisibleAtStartup)
            {
                IsFlyoutPresented = true;
            }

            IsTutorialVisibleAtStartup = currentAppearance.IsTutorialVisibleAtStartup;
            FlyoutBehavior = currentAppearance.IsFlyoutLocked ? FlyoutBehavior.Locked : FlyoutBehavior.Flyout;
            IsFlyoutCollapseButtonVisible = FlyoutBehavior == FlyoutBehavior.Locked && IsFlyoutPresented;
            IsFlyoutExpandButtonVisible = FlyoutBehavior == FlyoutBehavior.Locked && !IsFlyoutPresented;
            var selected = ThemeOptions.FirstOrDefault(option =>
                string.Equals(option.Id, currentAppearance.ThemeId, StringComparison.Ordinal));
            selected ??= ThemeOptions.First(option => option.Id == ThemeIds.System);
            if (SelectedThemeOption != selected)
            {
                SelectedThemeOption = selected;
            }
        }
        finally
        {
            synchronizing = false;
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
        IsFlyoutCollapseButtonVisible = FlyoutBehavior == FlyoutBehavior.Locked && IsFlyoutPresented;
        IsFlyoutExpandButtonVisible = FlyoutBehavior == FlyoutBehavior.Locked && !IsFlyoutPresented;
    }

    partial void OnIsFlyoutPresentedChanged(bool value)
    {
        if (synchronizing)
        {
            return;
        }

        IsFlyoutCollapseButtonVisible = FlyoutBehavior == FlyoutBehavior.Locked && IsFlyoutPresented;
        IsFlyoutExpandButtonVisible = FlyoutBehavior == FlyoutBehavior.Locked && !IsFlyoutPresented;
    }

    partial void OnFlyoutBehaviorChanged(FlyoutBehavior value)
    {
        if (synchronizing)
        {
            return;
        }

        IsFlyoutCollapseButtonVisible = FlyoutBehavior == FlyoutBehavior.Locked && IsFlyoutPresented;
        IsFlyoutExpandButtonVisible = FlyoutBehavior == FlyoutBehavior.Locked && !IsFlyoutPresented;
    }

    partial void OnIsTutorialVisibleAtStartupChanged(bool value)
    {
        if (synchronizing)
        {
            return;
        }

        settingsService.Current.Appearance.IsTutorialVisibleAtStartup = value;
        _ = PersistAsync();
    }

    partial void OnIsFlyoutVisibleAtStartupChanged(bool value)
    {
        if (synchronizing)
        {
            return;
        }

        settingsService.Current.Appearance.IsFlyoutVisibleAtStartup = value;
        _ = PersistAsync();
    }

    partial void OnSelectedThemeOptionChanged(ThemeOption? value)
    {
        if (value is null)
        {
            return;
        }

        _ = themeManager.ApplyAsync(value.Id);
        if (!synchronizing)
        {
            _ = PersistThemeAsync(value.Id);
        }
    }

    private void OnSettingsChanged(object? sender, PlannerSettingsChangedEventArgs e)
    {
        if (synchronizing)
        {
            return;
        }

        SetState(e.Current.Appearance);
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

    private async Task PersistThemeAsync(string newThemeId)
    {
        try
        {
            await settingsService.SaveTheme(settingsService.Current, newThemeId);
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
            await settingsService.SaveFlyout(settingsService.Current, IsFlyoutVisibleAtStartup, IsFlyoutLocked, IsTutorialVisibleAtStartup);
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
        if (settingsService.Current.Appearance.IsTutorialVisibleAtStartup)
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
    }

    private void SetItem(ShellItem? item)
    {
        if (item is not null)
        {
            dispatcher.Dispatch(() => SelectedItem = item);
        }
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
