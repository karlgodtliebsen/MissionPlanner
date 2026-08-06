using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Represents one exact runtime session owned by MissionPlanner.</summary>
public interface ISimulatorRuntimeSession : IAsyncDisposable
{
    /// <summary>Gets the exact runtime identity used for safe cleanup.</summary>
    SimulatorRuntimeIdentity Identity { get; }

    /// <summary>Gets the verified connected vehicle identity after heartbeat readiness.</summary>
    VehicleId? ConnectedVehicleId { get; }

    /// <summary>Gets the runtime-confirmed connection endpoints.</summary>
    IReadOnlyList<SimulationEndpoint> ConnectionEndpoints { get; }

    /// <summary>Gets adapter-provided command, version, process, and heartbeat diagnostics when available.</summary>
    SimulationRuntimeDiagnostics? Diagnostics => null;

    /// <summary>Gets runtime termination.</summary>
    Task<SimulatorRuntimeExit> Completion { get; }

    /// <summary>Occurs when the runtime emits one complete output line.</summary>
    event EventHandler<SimulatorOutputLine>? OutputReceived;

    /// <summary>Waits for the expected simulator heartbeat or readiness signal.</summary>
    /// <param name="timeout">Maximum wait.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task WaitForHeartbeatAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>Stops only this exactly identified owned runtime session.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}
