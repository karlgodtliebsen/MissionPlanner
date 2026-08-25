using System.Diagnostics;
using System.Text;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Creates direct no-shell provider processes with redirected UTF-8 output.</summary>
public sealed class SystemDfuChildProcessFactory : IDfuChildProcessFactory
{
    /// <inheritdoc />
    public IDfuChildProcess Create(string executablePath, IReadOnlyList<string> arguments, Encoding outputEncoding)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = outputEncoding,
            StandardErrorEncoding = outputEncoding
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new SystemDfuChildProcess(new Process { StartInfo = startInfo, EnableRaisingEvents = true });
    }

    private sealed class SystemDfuChildProcess : IDfuChildProcess
    {
        private readonly Process process;
        public SystemDfuChildProcess(Process process)
        {
            this.process = process;
            process.OutputDataReceived += (_, args) => OutputReceived?.Invoke(args.Data);
            process.ErrorDataReceived += (_, args) => ErrorReceived?.Invoke(args.Data);
        }
        public event Action<string?>? OutputReceived;
        public event Action<string?>? ErrorReceived;
        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;
        public bool Start()
        {
            return process.Start();
        }

        public void BeginOutputRead()
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        public Task WaitForExitAsync(CancellationToken cancellationToken = default)
        {
            return process.WaitForExitAsync(cancellationToken);
        }

        public void Kill(bool entireProcessTree)
        {
            process.Kill(entireProcessTree);
        }

        public void Dispose()
        {
            process.Dispose();
        }
    }
}
