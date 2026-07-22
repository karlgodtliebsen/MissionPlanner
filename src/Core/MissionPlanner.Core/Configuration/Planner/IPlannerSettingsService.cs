namespace MissionPlanner.Core.Configuration.Planner;

/// <summary>Persists the opaque non-secret Planner settings document.</summary>
public interface IPlannerSettingsStore
{
    /// <summary>Reads the persisted document.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The document, or <see langword="null"/> when none exists.</returns>
    ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the persisted document.</summary>
    /// <param name="document">The settings document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask WriteAsync(string document, CancellationToken cancellationToken = default);

    /// <summary>Clears the persisted document.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>Stores sensitive Planner values outside ordinary preferences and exports.</summary>
public interface IPlannerSecretStore
{
    /// <summary>Reads a secret by key.</summary>
    /// <param name="key">The secret key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The secret, or <see langword="null"/> when absent.</returns>
    ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Writes a secret by key.</summary>
    /// <param name="key">The secret key.</param>
    /// <param name="value">The secret value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Removes a secret by key.</summary>
    /// <param name="key">The secret key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>Loads, validates, migrates, persists, and observes local Planner settings.</summary>
public interface IPlannerSettingsService
{
    /// <summary>Gets the current in-memory settings.</summary>
    PlannerSettings Current { get; }

    /// <summary>Occurs after current settings change.</summary>
    event EventHandler<PlannerSettingsChangedEventArgs>? SettingsChanged;

    /// <summary>Loads persisted settings and recovers invalid data.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The load result.</returns>
    ValueTask<PlannerSettingsLoadResult> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates a complete settings snapshot.</summary>
    /// <param name="settings">The candidate settings.</param>
    /// <returns>All validation errors.</returns>
    IReadOnlyList<PlannerSettingsValidationError> Validate(PlannerSettings settings);

    /// <summary>Persists a complete validated settings snapshot.</summary>
    /// <param name="settings">The candidate settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The save result.</returns>
    ValueTask<PlannerSettingsSaveResult> SaveAsync(
        PlannerSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>Resets one section to defaults.</summary>
    /// <param name="section">The section to reset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The save result.</returns>
    ValueTask<PlannerSettingsSaveResult> ResetSectionAsync(
        PlannerSettingsSection section,
        CancellationToken cancellationToken = default);

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
    ValueTask<PlannerSettingsImportResult> ImportAsync(
        string document,
        CancellationToken cancellationToken = default);
}
