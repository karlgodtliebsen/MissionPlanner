namespace MissionPlanner.App.Views.InitSetup;

/// <summary>Navigates from Setup to an existing Config page without coupling the view model to Shell.</summary>
public interface ISetupNavigationService
{
    /// <summary>Opens a Config page identified by its shell title.</summary>
    /// <param name="destination">The target Config page title.</param>
    /// <returns>A task that completes after navigation.</returns>
    Task OpenConfigAsync(string destination);
}
