namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Provides host-platform detection and safe executable version probing.</summary>
public interface ISitlPlatformService
{
    /// <summary>Gets current platform capabilities.</summary>
    SitlPlatformCapability Current { get; }

    /// <summary>Attempts to query an external SITL executable version without a shell.</summary>
    /// <param name="executablePath">Absolute configured executable path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The version output, or <see langword="null"/> when unavailable.</returns>
    Task<string?> TryQueryVersionAsync(string executablePath, CancellationToken cancellationToken = default);
}
