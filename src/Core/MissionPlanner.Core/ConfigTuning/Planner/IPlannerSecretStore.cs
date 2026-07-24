namespace MissionPlanner.Core.ConfigTuning.Planner;

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
