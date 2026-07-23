using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.ConfigTuning;

/// <summary>
/// A shared, vehicle-scoped parameter editing session that tracks original, live, and pending
/// values with validation, grouped apply, revert, refresh, confirmed readback, and reboot aggregation.
/// </summary>
public interface IParameterEditSession
{
    /// <summary>Gets the vehicle and firmware identity captured by this session.</summary>
    ParameterEditScope Scope { get; }

    /// <summary>Gets the vehicle this session edits.</summary>
    VehicleId VehicleId { get; }

    /// <summary>Gets the editable fields in load order.</summary>
    IReadOnlyList<ParameterEditField> Fields { get; }

    /// <summary>Gets whether any field has an unwritten modification.</summary>
    bool IsDirty { get; }

    /// <summary>Gets whether the session still targets the active online vehicle and captured firmware.</summary>
    bool IsValid { get; }

    /// <summary>Gets the reason the session can no longer be used, when invalid.</summary>
    string? InvalidReason { get; }

    /// <summary>Occurs when the field set, values, or dirty state change.</summary>
    event EventHandler? Changed;

    /// <summary>Loads editable fields for the given parameter names, or all known parameters.</summary>
    /// <param name="names">The parameter names to load, or null for all known parameters.</param>
    /// <param name="cancellationToken">A token that cancels metadata resolution.</param>
    /// <returns>A task that completes when the fields are loaded.</returns>
    Task LoadAsync(IReadOnlyList<string>? names = null, CancellationToken cancellationToken = default);

    /// <summary>Loads fields resolved from explicit parameter aliases and presence rules.</summary>
    /// <param name="definitions">The logical field definitions to resolve.</param>
    /// <param name="cancellationToken">A token that cancels metadata resolution.</param>
    /// <returns>A task that completes when all present fields are loaded.</returns>
    Task LoadDefinitionsAsync(IReadOnlyList<ParameterFieldDefinition> definitions, CancellationToken cancellationToken = default);

    /// <summary>Gets a field by name.</summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>The field, or null when it is not loaded.</returns>
    ParameterEditField? GetField(string name);

    /// <summary>Sets a field's pending value, validating it against firmware metadata.</summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The pending value.</param>
    /// <param name="error">The validation error, when the value is rejected.</param>
    /// <returns><see langword="true"/> when the value is valid.</returns>
    bool TrySetPending(string name, double value, out string? error);

    /// <summary>Reverts one field's pending value to its live value.</summary>
    /// <param name="name">The parameter name.</param>
    void Revert(string name);

    /// <summary>Reverts every field's pending value to its live value.</summary>
    void RevertAll();

    /// <summary>Applies pending edits for the given names, or all modified fields, confirming each by readback.</summary>
    /// <param name="names">The names to apply, or null for all modified fields.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The aggregate apply report.</returns>
    Task<ParameterApplyReport> ApplyAsync(IReadOnlyList<string>? names = null, CancellationToken cancellationToken = default);

    /// <summary>Requests refreshed live values for the given names, or all loaded fields.</summary>
    /// <param name="names">The names to refresh, or null for all loaded fields.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>A task that completes after refresh requests are sent.</returns>
    Task RefreshAsync(IReadOnlyList<string>? names = null, CancellationToken cancellationToken = default);
}
