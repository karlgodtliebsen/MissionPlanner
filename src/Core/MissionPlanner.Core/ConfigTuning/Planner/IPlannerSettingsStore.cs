namespace MissionPlanner.Core.ConfigTuning.Planner;

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
