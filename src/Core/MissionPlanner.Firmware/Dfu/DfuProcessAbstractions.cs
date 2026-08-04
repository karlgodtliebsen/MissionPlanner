using System.Text;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Creates one directly invoked, redirectable child process.</summary>
public interface IDfuChildProcessFactory
{
    /// <summary>Creates a child process without starting it.</summary>
    IDfuChildProcess Create(string executablePath, IReadOnlyList<string> arguments, Encoding outputEncoding);
}

/// <summary>Wraps the minimum child-process lifecycle required by the bounded DFU runner.</summary>
public interface IDfuChildProcess : IDisposable
{
    /// <summary>Raised for each decoded standard-output line.</summary>
    event Action<string?>? OutputReceived;
    /// <summary>Raised for each decoded standard-error line.</summary>
    event Action<string?>? ErrorReceived;
    /// <summary>Gets whether the process has terminated.</summary>
    bool HasExited { get; }
    /// <summary>Gets the exit code after termination.</summary>
    int ExitCode { get; }
    /// <summary>Starts the child process.</summary>
    bool Start();
    /// <summary>Begins asynchronous output and error reads.</summary>
    void BeginOutputRead();
    /// <summary>Waits for process termination.</summary>
    Task WaitForExitAsync(CancellationToken cancellationToken = default);
    /// <summary>Terminates the process, optionally including descendants.</summary>
    void Kill(bool entireProcessTree);
}
