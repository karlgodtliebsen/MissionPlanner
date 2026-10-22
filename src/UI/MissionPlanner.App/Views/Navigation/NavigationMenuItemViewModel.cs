using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;

namespace MissionPlanner.App.Views.Navigation;

/// <summary>
/// Represents a navigation menu item in the application, including its header, route, icon, and any child menu items.
/// </summary>
public sealed class NavigationMenuItemViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationMenuItemViewModel"/> class.  
    /// </summary>
    /// <param name="header"></param>
    /// <param name="route"></param>
    /// <param name="icon"></param>
    /// <param name="children"></param>
    public NavigationMenuItemViewModel(string header, string? route = null, Bitmap? icon = null,
        IEnumerable<NavigationMenuItemViewModel>? children = null)
    {
        Header = header;
        Route = route;
        Icon = icon;
        Children = children is null ? [] : new ObservableCollection<NavigationMenuItemViewModel>(children);
    }

    /// <summary>Gets the menu text.</summary>
    public string Header
    {
        get;
    }
    /// <summary>Gets the route the item navigates to, or null for a group.</summary>
    public string? Route
    {
        get;
    }
    /// <summary>Gets the menu icon.</summary>
    public Bitmap? Icon
    {
        get;
    }
    /// <summary>Gets the child items of a group.</summary>
    public ObservableCollection<NavigationMenuItemViewModel> Children
    {
        get;
    }
}
