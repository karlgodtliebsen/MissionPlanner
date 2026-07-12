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
        if (CurrentShell.CurrentItem == value)
        {
            return;
        }

        CurrentShell.CurrentItem = value;
        if (CurrentShell.FlyoutBehavior == FlyoutBehavior.Flyout)
        {
            CurrentShell.FlyoutIsPresented = false;
        }

        SelectedItem = CurrentShell.CurrentItem;
    }
}
