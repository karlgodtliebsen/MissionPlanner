using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using ShellItem = Microsoft.Maui.Controls.ShellItem;

namespace MissionPlanner.App.AppViewModels;

/// <summary>
/// ViewModel for the application's shell content.
/// </summary>
public partial class AppShellContentViewModel : ObservableObject
{
    [ObservableProperty] public partial Shell CurrentShell { get; set; } = null!;
    [ObservableProperty] public partial string SearchText { get; set; } = null!;
    [ObservableProperty] public partial ReadOnlyObservableCollection<ShellItem> Items { get; set; } = null!;
    [ObservableProperty] public partial ShellItem? SelectedItem { get; set; } = null!;


    partial void OnCurrentShellChanged(Shell value)
    {
        CurrentShell.Loaded += CurrentShell_Loaded;
    }

    private void CurrentShell_Loaded(object? sender, EventArgs e)
    {
        Items = new ReadOnlyObservableCollection<ShellItem>(new ObservableCollection<ShellItem>(CurrentShell.Items));
        CurrentShell.CurrentItem = Items.FirstOrDefault();
        SelectedItem = CurrentShell.CurrentItem;
    }

    partial void OnSearchTextChanged(string value)
    {
        IEnumerable<ShellItem> filtered = CurrentShell.Items.Where(item => item.FlyoutItemIsVisible
                                                                           && (string.IsNullOrEmpty(SearchText) || item.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));
        Items = new ReadOnlyObservableCollection<ShellItem>(new ObservableCollection<ShellItem>(filtered.ToList()));
        SelectedItem = CurrentShell.CurrentItem;
    }

    partial void OnSelectedItemChanged(ShellItem? value)
    {
        if (CurrentShell.CurrentItem == value) return;

        CurrentShell.CurrentItem = value;
        if (CurrentShell.FlyoutBehavior == FlyoutBehavior.Flyout) CurrentShell.FlyoutIsPresented = false;

        SelectedItem = CurrentShell.CurrentItem;
    }
}
/*
/// <summary>
/// ViewModel for the application's shell content.
/// </summary>
public class AppShellContentViewModel : ReactiveObject
{
    private Shell? currentShell;

    /// <summary>
    /// The current shell instance.
    /// </summary>
    public Shell CurrentShell
    {
        get => currentShell;
        set
        {
            currentShell = value;
            OnCurrentShellChanged();
        }
    }

    private void OnCurrentShellChanged()
    {
        CurrentShell.Loaded += CurrentShell_Loaded;
    }

    private void CurrentShell_Loaded(object? sender, EventArgs e)
    {
        var observableFilter = this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .Select(searchText => (Func<ShellItem, bool>)(item => item.FlyoutItemIsVisible
                                                                  && (string.IsNullOrEmpty(SearchText) || item.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase))));

        CurrentShell.Items.AsObservableChangeSet()
            .Filter(observableFilter)
            //.GroupOn(x => x.GetId() ?? "Others")
            //.Transform(group => new ShellItemGroup(group.GroupKey, group.List.Items.ToList()))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out var _shellItems)
            .Subscribe();

        Items = _shellItems;

        SelectedItem = CurrentShell.CurrentItem;

        this.WhenAnyValue(x => x.SelectedItem)
            .WhereNotNull()
            .Subscribe(shellItem =>
            {
                CurrentShell.CurrentItem = shellItem;
                if (CurrentShell.FlyoutBehavior == FlyoutBehavior.Flyout)
                {
                    CurrentShell.FlyoutIsPresented = false;
                }
            });
    }

    /// <summary>
    /// The text used to filter the items in the shell.
    /// </summary>
    [Reactive]
    public string SearchText { get; set; } = null!;

    /// <summary>
    /// The collection of items in the shell.
    /// </summary>
    [Reactive]
    public ReadOnlyObservableCollection<ShellItem> Items { get; private set; } = null!;

    /// <summary>
    /// The currently selected item in the shell.
    /// </summary>
    [Reactive]
    public ShellItem? SelectedItem { get; set; } = null!;
}

*/