namespace MissionPlanner.Core.Simulation;

/// <summary>Provides documented simulation controls and start-location presets.</summary>
public interface ISimulationControlCatalog
{
    /// <summary>Gets all documented controls, including explicitly unavailable capability placeholders.</summary>
    IReadOnlyList<SimulationControlDescriptor> Controls { get; }

    /// <summary>Gets built-in typed start-location presets.</summary>
    IReadOnlyList<SimulationLocationPreset> Locations { get; }
}

/// <summary>Applies documented controls to the exact connected simulator vehicle.</summary>
public interface ISimulationControlService : IAsyncDisposable
{
    /// <summary>Gets the bounded event history for the current application lifetime.</summary>
    IReadOnlyList<SimulationScenarioEvent> Events { get; }

    /// <summary>Discovers controls supported by the current session and live parameter set.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Control capabilities in catalog order.</returns>
    Task<IReadOnlyList<SimulationControlCapability>> DiscoverAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Applies a value or starts a bounded hazardous control.</summary>
    /// <param name="controlKey">Logical control key.</param>
    /// <param name="requestedValue">Requested value for non-fixed controls.</param>
    /// <param name="duration">Hazard duration; required for bounded controls.</param>
    /// <param name="confirmed">Explicit hazardous-action confirmation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ApplyAsync(
        string controlKey,
        double requestedValue,
        TimeSpan? duration,
        bool confirmed,
        CancellationToken cancellationToken = default);

    /// <summary>Resets an active hazardous control to its captured safe value.</summary>
    /// <param name="controlKey">Logical control key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ResetAsync(string controlKey, CancellationToken cancellationToken = default);

    /// <summary>Resets all active hazardous controls that still target the current simulator.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ResetAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>Persists an opaque simulation scenario-preset document.</summary>
public interface ISimulationScenarioPresetStore
{
    /// <summary>Reads the persisted preset document.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The document, or <see langword="null"/>.</returns>
    ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically replaces the persisted preset document.</summary>
    /// <param name="document">Serialized preset document.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask WriteAsync(string document, CancellationToken cancellationToken = default);
}

/// <summary>Loads and saves scenario presets separately from simulator launch profiles.</summary>
public interface ISimulationScenarioPresetService
{
    /// <summary>Gets initialized presets.</summary>
    IReadOnlyList<SimulationScenarioPreset> Presets { get; }

    /// <summary>Loads persisted presets.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Loaded presets.</returns>
    ValueTask<IReadOnlyList<SimulationScenarioPreset>> InitializeAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Adds or replaces one preset.</summary>
    /// <param name="preset">Preset to persist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask SaveAsync(
        SimulationScenarioPreset preset,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes one preset.</summary>
    /// <param name="presetId">Preset identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask DeleteAsync(Guid presetId, CancellationToken cancellationToken = default);
}

/// <summary>Configures bounded simulation-control discovery, readback, and event retention.</summary>
public sealed class SimulationControlOptions
{
    /// <summary>Application configuration section.</summary>
    public const string SectionName = "SimulationControls";

    /// <summary>Gets or sets the parameter discovery wait in milliseconds.</summary>
    public int DiscoveryWaitMilliseconds { get; set; } = 500;

    /// <summary>Gets or sets the confirmed-readback timeout in seconds.</summary>
    public int ReadbackTimeoutSeconds { get; set; } = 3;

    /// <summary>Gets or sets the maximum retained scenario events.</summary>
    public int EventCapacity { get; set; } = 500;
}
