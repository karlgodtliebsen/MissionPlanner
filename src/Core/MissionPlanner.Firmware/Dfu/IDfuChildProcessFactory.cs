using System.Text;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Creates one directly invoked, redirectable child process.</summary>
public interface IDfuChildProcessFactory
{
    /// <summary>Creates a child process without starting it.</summary>
    IDfuChildProcess Create(string executablePath, IReadOnlyList<string> arguments, Encoding outputEncoding);
}
