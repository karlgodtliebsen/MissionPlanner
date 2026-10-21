using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.App.Services;

/// <summary>Persists simulation scenario presets as JSON in the user's local application-data folder.</summary>
public sealed class JsonSimulationScenarioPresetStore : JsonDocumentFileStore, ISimulationScenarioPresetStore
{
    /// <summary>Creates a store using the default per-user application-data location.</summary>
    public JsonSimulationScenarioPresetStore()
        : base("simulation-scenario-presets.json")
    {
    }

    /// <summary>Creates a store that persists scenario presets to an explicit file.</summary>
    /// <param name="filePath">Absolute document path.</param>
    public JsonSimulationScenarioPresetStore(string filePath)
        : base(filePath, true)
    {
    }

    /// <inheritdoc />
    public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) => ReadDocumentAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask WriteAsync(string document, CancellationToken cancellationToken = default) => WriteDocumentAsync(document, cancellationToken);
}
