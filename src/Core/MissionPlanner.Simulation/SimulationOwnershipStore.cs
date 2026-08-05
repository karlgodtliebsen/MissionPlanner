using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.Simulation;

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
        File.Move(temporaryPath, path, true);
        activeSessions[ownedProcess.SessionId] = 0;
    }

    /// <inheritdoc />
    public Task ReleaseAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        activeSessions.TryRemove(sessionId, out var _);
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

    private string MarkerPath(Guid sessionId)
    {
        return Path.Combine(markerDirectory, $"{sessionId:N}.owned.json");
    }
}
