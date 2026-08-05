using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.Core.Simulation;

/// <summary>Loads and persists schema-versioned simulation scenario presets.</summary>
public sealed class SimulationScenarioPresetService(
    ISimulationScenarioPresetStore store,
    ILogger<SimulationScenarioPresetService> logger) : ISimulationScenarioPresetService
{
    private const int SchemaVersion = 1;
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private IReadOnlyList<SimulationScenarioPreset> presets = [];
    private bool initialized;

    /// <inheritdoc />
    public IReadOnlyList<SimulationScenarioPreset> Presets => presets;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SimulationScenarioPreset>> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return presets;
        }

        var document = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(document))
        {
            try
            {
                var persisted = JsonSerializer.Deserialize<PresetDocument>(document, jsonOptions);
                if (persisted is { Version: SchemaVersion } && persisted.Presets.All(IsValid))
                {
                    presets = persisted.Presets;
                }
                else
                {
                    logger.LogWarning("Simulation scenario presets had an unsupported schema or invalid content.");
                }
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "Simulation scenario preset persistence was corrupt; using an empty set.");
            }
        }

        initialized = true;
        return presets;
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(
        SimulationScenarioPreset preset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (!IsValid(preset))
        {
            throw new ArgumentException("The simulation scenario preset is invalid.", nameof(preset));
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        presets = presets.Where(item => item.Id != preset.Id).Append(preset)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        await PersistAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DeleteAsync(Guid presetId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        presets = presets.Where(item => item.Id != presetId).ToArray();
        await PersistAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!initialized)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private ValueTask PersistAsync(CancellationToken cancellationToken) =>
        store.WriteAsync(
            JsonSerializer.Serialize(new PresetDocument(SchemaVersion, presets), jsonOptions),
            cancellationToken);

    private static bool IsValid(SimulationScenarioPreset preset) =>
        preset.Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(preset.Name) &&
        preset.Controls is not null &&
        preset.Controls.All(control =>
            !string.IsNullOrWhiteSpace(control.ControlKey) &&
            double.IsFinite(control.Value) &&
            (control.Duration is null || control.Duration > TimeSpan.Zero));

    private sealed record PresetDocument(int Version, IReadOnlyList<SimulationScenarioPreset> Presets);
}
