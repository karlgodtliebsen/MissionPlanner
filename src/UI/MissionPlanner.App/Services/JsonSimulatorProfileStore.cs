using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.App.Services;

/// <summary>Persists simulator profiles as JSON in the user's local application-data folder.</summary>
public sealed class JsonSimulatorProfileStore : JsonDocumentFileStore, ISimulatorProfileStore
{
    /// <summary>Creates a store using the default per-user application-data location.</summary>
    public JsonSimulatorProfileStore()
        : base("simulator-profiles.json")
    {
    }

    /// <summary>Creates a store that persists simulator profiles to an explicit file.</summary>
    /// <param name="filePath">Absolute document path.</param>
    public JsonSimulatorProfileStore(string filePath)
        : base(filePath, true)
    {
    }

    /// <inheritdoc />
    public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) => ReadDocumentAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(string document, CancellationToken cancellationToken = default) => WriteDocumentAsync(document, cancellationToken);
}
