namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Represents one exact local process owned by a simulator runtime.</summary>
public interface ISimulatorProcessSession : IAsyncDisposable
{
    /// <summary>Gets the operating-system process identifier.</summary>
    int ProcessId { get; }

    /// <summary>Gets the normalized executable path used to start the process.</summary>
    string ExecutablePath { get; }

    /// <summary>Gets the operating-system process start time used to prevent PID-reuse mistakes.</summary>
    DateTimeOffset StartedAt { get; }

    /// <summary>Gets process termination.</summary>
    Task<SimulatorRuntimeExit> Completion { get; }

    /// <summary>Gets bounded output captured before downstream observers subscribed.</summary>
    IReadOnlyList<SimulatorOutputLine> RecentOutput { get; }

    /// <summary>Occurs for each complete stdout or stderr line.</summary>
    event EventHandler<SimulatorOutputLine>? OutputReceived;

    /// <summary>Stops this exact process and its descendants.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task StopAsync(CancellationToken cancellationToken = default);
}
