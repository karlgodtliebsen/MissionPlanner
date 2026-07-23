using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MissionPlanner.Core.Simulation;

/// <summary>Contains sufficient process identity to recover only a MissionPlanner-owned orphan.</summary>
/// <param name="SessionId">Owning simulation session.</param>
/// <param name="OwnershipToken">Unique marker token.</param>
/// <param name="ProcessId">Operating-system process identifier.</param>
/// <param name="ExecutablePath">Normalized executable path.</param>
/// <param name="StartedAt">Operating-system process start time.</param>
public sealed record SimulationOwnedProcess(
    Guid SessionId,
    Guid OwnershipToken,
    int ProcessId,
    string ExecutablePath,
    DateTimeOffset StartedAt);

/// <summary>Identifies the result of a safe orphan recovery attempt.</summary>
public enum SimulationOrphanRecoveryState
{
    /// <summary>The process no longer exists.</summary>
    NotRunning,

    /// <summary>The exact path and start time matched and the process was stopped.</summary>
    Recovered,

    /// <summary>The PID exists but its path or start time did not match, so it was not touched.</summary>
    IdentityMismatch,

    /// <summary>The exact process could not be inspected or stopped.</summary>
    Failed
}

/// <summary>Provides one orphan recovery result.</summary>
/// <param name="OwnedProcess">Persisted owned-process identity.</param>
/// <param name="State">Recovery state.</param>
/// <param name="Message">Diagnostic detail.</param>
public sealed record SimulationOrphanRecoveryResult(
    SimulationOwnedProcess OwnedProcess,
    SimulationOrphanRecoveryState State,
    string Message);

/// <summary>Performs platform-specific exact-process identity verification and recovery.</summary>
public interface ISimulatorOwnedProcessRecovery
{
    /// <summary>Recovers a process only when PID, executable path, and start time all match.</summary>
    /// <param name="ownedProcess">Persisted process identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The recovery result.</returns>
    Task<SimulationOrphanRecoveryResult> RecoverAsync(
        SimulationOwnedProcess ownedProcess,
        CancellationToken cancellationToken = default);
}

/// <summary>Persists and recovers exact process ownership markers.</summary>
public interface ISimulationOwnershipStore
{
    /// <summary>Marks an exact process as owned by the current application lifetime.</summary>
    /// <param name="ownedProcess">Owned process identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task MarkAsync(SimulationOwnedProcess ownedProcess, CancellationToken cancellationToken = default);

    /// <summary>Releases one exact ownership marker after cleanup.</summary>
    /// <param name="sessionId">Owning session identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ReleaseAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Recovers persisted markers that are not active in this application lifetime.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All attempted recovery results.</returns>
    Task<IReadOnlyList<SimulationOrphanRecoveryResult>> RecoverOrphansAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>Declines orphan recovery on hosts without an exact process-identity implementation.</summary>
public sealed class UnavailableSimulatorOwnedProcessRecovery : ISimulatorOwnedProcessRecovery
{
    /// <inheritdoc />
    public Task<SimulationOrphanRecoveryResult> RecoverAsync(
        SimulationOwnedProcess ownedProcess,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SimulationOrphanRecoveryResult(
            ownedProcess,
            SimulationOrphanRecoveryState.Failed,
            "Safe local process recovery is unavailable on this host."));
    }
}

/// <summary>Stores non-secret ownership markers under the configured simulation artifact root.</summary>
public sealed class SimulationOwnershipStore : ISimulationOwnershipStore
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly ISimulatorOwnedProcessRecovery processRecovery;
    private readonly ILogger<SimulationOwnershipStore> logger;
    private readonly string markerDirectory;
    private readonly ConcurrentDictionary<Guid, byte> activeSessions = [];

    /// <summary>Initializes the ownership marker store.</summary>
    /// <param name="options">Simulation workspace options.</param>
    /// <param name="processRecovery">Safe platform process recovery.</param>
    /// <param name="logger">Logger.</param>
    public SimulationOwnershipStore(
        IOptions<SimulationWorkspaceOptions> options,
        ISimulatorOwnedProcessRecovery processRecovery,
        ILogger<SimulationOwnershipStore> logger)
    {
        var root = string.IsNullOrWhiteSpace(options.Value.LogRootDirectory)
            ? Path.Combine(Path.GetTempPath(), "MissionPlanner", "Simulation")
            : options.Value.LogRootDirectory;
        markerDirectory = Path.Combine(Path.GetFullPath(root), "ownership");
        this.processRecovery = processRecovery;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task MarkAsync(SimulationOwnedProcess ownedProcess, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownedProcess);
        if (ownedProcess.SessionId == Guid.Empty || ownedProcess.OwnershipToken == Guid.Empty ||
            ownedProcess.ProcessId <= 0 || !Path.IsPathFullyQualified(ownedProcess.ExecutablePath))
        {
            throw new ArgumentException("Owned process identity is incomplete.", nameof(ownedProcess));
        }

        Directory.CreateDirectory(markerDirectory);
        var path = MarkerPath(ownedProcess.SessionId);
        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(
            temporaryPath,
            JsonSerializer.Serialize(ownedProcess, jsonOptions),
            cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, path, overwrite: true);
        activeSessions[ownedProcess.SessionId] = 0;
    }

    /// <inheritdoc />
    public Task ReleaseAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        activeSessions.TryRemove(sessionId, out _);
        var path = MarkerPath(sessionId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SimulationOrphanRecoveryResult>> RecoverOrphansAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(markerDirectory))
        {
            return [];
        }

        var results = new List<SimulationOrphanRecoveryResult>();
        foreach (var path in Directory.EnumerateFiles(markerDirectory, "*.owned.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            SimulationOwnedProcess? marker;
            try
            {
                marker = JsonSerializer.Deserialize<SimulationOwnedProcess>(
                    await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false),
                    jsonOptions);
            }
            catch (Exception exception) when (exception is JsonException or IOException)
            {
                logger.LogWarning(exception, "Ignored invalid simulation ownership marker {MarkerPath}.", path);
                continue;
            }

            if (marker is null || activeSessions.ContainsKey(marker.SessionId))
            {
                continue;
            }

            var result = await processRecovery.RecoverAsync(marker, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            if (result.State is SimulationOrphanRecoveryState.NotRunning or SimulationOrphanRecoveryState.Recovered)
            {
                File.Delete(path);
            }
            else
            {
                logger.LogWarning(
                    "Preserved ownership marker for session {SessionId}; recovery state was {RecoveryState}: {Message}",
                    marker.SessionId,
                    result.State,
                    result.Message);
            }
        }

        return results;
    }

    private string MarkerPath(Guid sessionId) => Path.Combine(markerDirectory, $"{sessionId:N}.owned.json");
}
