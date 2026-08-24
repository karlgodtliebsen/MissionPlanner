namespace UraniumUI.Material.TabViews;

/// <summary>
/// Defines a lifecycle with activation and deactivation capabilities.
/// </summary>
public interface IActivationLifeCycle
{
    /// <summary>
    /// Activates the lifecycle.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ActivateAsync();

    /// <summary>
    /// Deactivates the lifecycle.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeactivateAsync();
}
