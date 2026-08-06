using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Simulation.Abstractions;

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

    /// <summary>Discovers controls for one exact simulator session and vehicle.</summary>
    /// <param name="sessionId">Exact simulation session identity.</param>
    /// <param name="vehicleId">Exact vehicle identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Control capabilities in catalog order.</returns>
    Task<IReadOnlyList<SimulationControlCapability>> DiscoverAsync(
        Guid sessionId,
        VehicleId vehicleId,
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

    /// <summary>Applies a value to one exact simulator session and vehicle.</summary>
    /// <param name="sessionId">Exact simulation session identity.</param>
    /// <param name="vehicleId">Exact vehicle identity.</param>
    /// <param name="controlKey">Logical control key.</param>
    /// <param name="requestedValue">Requested value.</param>
    /// <param name="duration">Bounded hazard duration.</param>
    /// <param name="confirmed">Explicit hazardous-action confirmation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ApplyAsync(
        Guid sessionId,
        VehicleId vehicleId,
        string controlKey,
        double requestedValue,
        TimeSpan? duration,
        bool confirmed,
        CancellationToken cancellationToken = default);

    /// <summary>Resets an active hazardous control to its captured safe value.</summary>
    /// <param name="controlKey">Logical control key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ResetAsync(string controlKey, CancellationToken cancellationToken = default);

    /// <summary>Resets one control on an exact simulator session and vehicle.</summary>
    /// <param name="sessionId">Exact simulation session identity.</param>
    /// <param name="vehicleId">Exact vehicle identity.</param>
    /// <param name="controlKey">Logical control key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ResetAsync(
        Guid sessionId,
        VehicleId vehicleId,
        string controlKey,
        CancellationToken cancellationToken = default);

    /// <summary>Resets all active hazardous controls that still target the current simulator.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ResetAllAsync(CancellationToken cancellationToken = default);
}
