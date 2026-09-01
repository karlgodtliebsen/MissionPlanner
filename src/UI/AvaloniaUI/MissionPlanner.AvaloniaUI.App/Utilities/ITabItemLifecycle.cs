namespace MissionPlanner.AvaloniaUI.App.Utilities;

/// <summary>Applies the ViewModel lifecycle when a Flight Data tab enters or leaves the visual tree.</summary>
public interface ITabItemLifecycle
{
    /// <summary>
    /// Activates the tab item.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ActivateAsync();

    /// <summary>
    /// Deactivates the tab item.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeactivateAsync();
}
