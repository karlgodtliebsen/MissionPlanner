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

    /// <summary>
    /// Opens a sub-view identified by its root and destination shell titles.
    /// </summary>
    /// <param name="root">The root page title.</param>
    /// <param name="destination">The target sub-view title.</param>
    /// <returns>A task that completes after navigation.</returns>
    Task OpenSubViewAsync(string root, string destination);
}
