namespace MissionPlanner.Core.Simulation;

/// <summary>Persists an opaque, non-secret simulator-profile document.</summary>
public interface ISimulatorProfileStore
{
    /// <summary>Reads the persisted profile document.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The document, or <see langword="null"/> when no document exists.</returns>
    ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically replaces the persisted profile document.</summary>
    /// <param name="document">The serialized document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask WriteAsync(string document, CancellationToken cancellationToken = default);
}
