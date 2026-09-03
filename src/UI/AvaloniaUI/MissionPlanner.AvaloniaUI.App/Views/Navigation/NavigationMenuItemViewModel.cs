using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;

namespace MissionPlanner.AvaloniaUI.App.Views.Navigation;

public sealed class NavigationMenuItemViewModel
{
    public NavigationMenuItemViewModel(string header, string? route = null, Bitmap? icon = null,
        IEnumerable<NavigationMenuItemViewModel>? children = null)
    {
        Header = header;
        Route = route;
        Icon = icon;
        Children = children is null ? [] : new ObservableCollection<NavigationMenuItemViewModel>(children);
    }

    public string Header { get; }
    public string? Route { get; }
    public Bitmap? Icon { get; }
    public ObservableCollection<NavigationMenuItemViewModel> Children { get; }
}
