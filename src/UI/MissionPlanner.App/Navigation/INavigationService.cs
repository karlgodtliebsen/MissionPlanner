namespace MissionPlanner.App.Navigation;

/// <summary>
/// Navigates to an existing page without coupling the view model to Shell.
/// </summary>
public interface INavigationService
{
    /// <summary>Opens a page identified by its shell title.</summary>
    /// <param name="destination">The target page title.</param>
    /// <returns>A task that completes after navigation.</returns>
    Task OpenPageAsync(string destination);
}
