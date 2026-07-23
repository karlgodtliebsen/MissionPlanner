using System.ComponentModel;
using System.Diagnostics;
using MissionPlanner.Core.Simulation;

namespace MissionPlanner.App.Configuration;

/// <summary>Recovers only local simulator processes whose complete persisted identity still matches.</summary>
public sealed class LocalSimulatorOwnedProcessRecovery : ISimulatorOwnedProcessRecovery
{
    /// <inheritdoc />
    public async Task<SimulationOrphanRecoveryResult> RecoverAsync(
        SimulationOwnedProcess ownedProcess,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownedProcess);
        cancellationToken.ThrowIfCancellationRequested();
        Process process;
        try
        {
            process = Process.GetProcessById(ownedProcess.ProcessId);
        }
        catch (ArgumentException)
        {
            return Result(SimulationOrphanRecoveryState.NotRunning, "The owned process is no longer running.");
        }

        using (process)
        {
            try
            {
                if (process.HasExited)
                {
                    return Result(SimulationOrphanRecoveryState.NotRunning, "The owned process has already exited.");
                }

                var currentPath = process.MainModule?.FileName;
                var currentStart = new DateTimeOffset(process.StartTime.ToUniversalTime());
                var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (string.IsNullOrWhiteSpace(currentPath) ||
                    !Path.GetFullPath(currentPath).Equals(Path.GetFullPath(ownedProcess.ExecutablePath), comparison) ||
                    Math.Abs((currentStart - ownedProcess.StartedAt).TotalMilliseconds) > 1)
                {
                    return Result(
                        SimulationOrphanRecoveryState.IdentityMismatch,
                        "The PID is now owned by a different executable or process generation; it was left untouched.");
                }

                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                return Result(SimulationOrphanRecoveryState.Recovered, "The exact owned simulator process was stopped.");
            }
            catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or IOException)
            {
                return Result(SimulationOrphanRecoveryState.Failed, exception.Message);
            }
        }

        SimulationOrphanRecoveryResult Result(SimulationOrphanRecoveryState state, string message) =>
            new(ownedProcess, state, message);
    }
}
