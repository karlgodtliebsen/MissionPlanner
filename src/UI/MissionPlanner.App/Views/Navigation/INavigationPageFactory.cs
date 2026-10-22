using Avalonia.Controls;

namespace MissionPlanner.App.Views.Navigation;

/// <summary>Creates the page for a route.</summary>
public interface INavigationPageFactory
{
    /// <summary>Creates the page of the route.</summary>
    Page Create(string route);
}