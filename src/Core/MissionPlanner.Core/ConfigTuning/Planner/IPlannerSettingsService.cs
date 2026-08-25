namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>
/// Loads, validates, migrates, persists, and observes local Planner settings.
/// </summary>
public interface IPlannerSettingsService
{
    /// <summary>Gets the current in-memory settings.</summary>
    PlannerSettings Current
    {
        get;
    }

    /// <summary>Occurs after current settings change.</summary>
    event Action<PlannerSettingsChangedEventArgs>? SettingsChanged;

    /// <summary>Loads persisted settings and recovers invalid data.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The load result.</returns>
    ValueTask<PlannerSettingsLoadResult> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates a complete settings snapshot.</summary>
    /// <param name="settings">The candidate settings.</param>
    /// <returns>All validation errors.</returns>
    IReadOnlyList<PlannerSettingsValidationError> Validate(PlannerSettings settings);

    /// <summary>
    /// Persists a complete validated settings snapshot.
    /// </summary>
    /// <param name="settings">The candidate settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The save result.</returns>
    ValueTask<PlannerSettingsSaveResult> SaveAsync(PlannerSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a complete validated settings snapshot with updated Theme.
    /// This is a convenience method that updates the Theme section and saves the settings.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="themeId">The stable theme or selection-policy identifier.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<PlannerSettingsSaveResult> SaveTheme(PlannerSettings settings, string themeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a complete validated settings snapshot with updated Flyout.
    /// This is a convenience method that updates the Flyout section and saves the settings.
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="isFlyoutVisibleAtStartup"></param>
    /// <param name="isFlyoutLocked"></param>
    /// <param name="isTutorialVisibleAtStartup"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    ValueTask<PlannerSettingsSaveResult> SaveFlyout(PlannerSettings settings, bool isFlyoutVisibleAtStartup, bool isFlyoutLocked, bool isTutorialVisibleAtStartup, CancellationToken cancellationToken = default);

    /// <summary>Resets one section to defaults.</summary>
    /// <param name="section">The section to reset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The save result.</returns>
    ValueTask<PlannerSettingsSaveResult> ResetSectionAsync(PlannerSettingsSection section, CancellationToken cancellationToken = default);

    /// <summary>Resets all settings to defaults.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The save result.</returns>
    ValueTask<PlannerSettingsSaveResult> ResetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Exports non-secret settings as a versioned JSON document.</summary>
    /// <returns>The exported document.</returns>
    string Export();

    /// <summary>Imports, migrates, validates, and persists a settings document.</summary>
    /// <param name="document">The JSON document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The import result.</returns>
    ValueTask<PlannerSettingsImportResult> ImportAsync(string document, CancellationToken cancellationToken = default);
}
