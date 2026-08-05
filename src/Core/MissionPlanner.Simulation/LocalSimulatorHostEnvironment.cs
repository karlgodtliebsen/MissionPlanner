using System.Net.NetworkInformation;

namespace MissionPlanner.Core.Simulation;

/// <summary>Provides read-only local host validation for simulator profiles.</summary>
public sealed class LocalSimulatorHostEnvironment : ISimulatorHostEnvironment
{
    /// <inheritdoc />
    public ValueTask<SimulationValidationIssue?> ValidateExecutableAsync(
        string executablePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return ValueTask.FromResult<SimulationValidationIssue?>(new SimulationValidationIssue(
                "host.executable-required",
                "binary.executablePath",
                "Select an installed simulator executable."));
        }

        if (!Path.IsPathFullyQualified(executablePath))
        {
            return ValueTask.FromResult<SimulationValidationIssue?>(new SimulationValidationIssue(
                "host.executable-absolute",
                "binary.executablePath",
                "The simulator executable path must be absolute."));
        }

        if (!File.Exists(executablePath))
        {
            return ValueTask.FromResult<SimulationValidationIssue?>(new SimulationValidationIssue(
                "host.executable-missing",
                "binary.executablePath",
                $"Simulator executable was not found at '{executablePath}'."));
        }

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                var mode = File.GetUnixFileMode(executablePath);
                const UnixFileMode executeBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
                if ((mode & executeBits) == 0)
                {
                    return ValueTask.FromResult<SimulationValidationIssue?>(new SimulationValidationIssue(
                        "host.executable-permission",
                        "binary.executablePath",
                        "The selected simulator file does not have executable permission."));
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                return ValueTask.FromResult<SimulationValidationIssue?>(new SimulationValidationIssue(
                    "host.executable-access",
                    "binary.executablePath",
                    $"The selected simulator executable cannot be inspected: {exception.Message}"));
            }
        }

        return ValueTask.FromResult<SimulationValidationIssue?>(null);
    }

    /// <inheritdoc />
    public ValueTask<bool> IsPortAvailableAsync(
        SimulationEndpoint endpoint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var inUse = endpoint.Transport == SimulationEndpointTransport.Udp
            ? properties.GetActiveUdpListeners().Any(item => item.Port == endpoint.Port)
            : properties.GetActiveTcpListeners().Any(item => item.Port == endpoint.Port);
        return ValueTask.FromResult(!inUse);
    }
}
