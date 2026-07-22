namespace MissionPlanner.App.Views.FlightData;

/// <summary>
/// Defines the activation boundary implemented by Flight Data tabs with background work.
/// </summary>
public interface IFlightDataTabLifecycle
{
    /// <summary>Gets the stable tab key used by the parent Flight Data view model.</summary>
    string Key { get; }

    /// <summary>Gets a value indicating whether the tab is currently visible and active.</summary>
    bool IsActive { get; }

    /// <summary>Gets a value indicating whether the tab has completed its first lazy initialization.</summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Activates the tab and starts its vehicle-bound work.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels activation.</param>
    Task ActivateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates the tab and stops its vehicle-bound work.
    /// </summary>
    Task DeactivateAsync();
}
