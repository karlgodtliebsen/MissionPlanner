using System.Diagnostics;
using MissionPlanner.Core.Simulation;

namespace MissionPlanner.App.Helpers;

/// <summary>Starts and owns local simulator processes using tokenized argument APIs.</summary>
public sealed class LocalSimulatorProcessHost : ISimulatorProcessHost
{
    /// <inheritdoc />
    public Task<ISimulatorProcessSession> StartAsync(
        SimulatorProcessStartInfo startInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Path.IsPathFullyQualified(startInfo.ExecutablePath) || !File.Exists(startInfo.ExecutablePath))
        {
            throw new FileNotFoundException("The selected SITL executable was not found.", startInfo.ExecutablePath);
        }

        Directory.CreateDirectory(startInfo.WorkingDirectory);
        var processStartInfo = new ProcessStartInfo
        {
            FileName = startInfo.ExecutablePath,
            WorkingDirectory = startInfo.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = !startInfo.ShowConsoleWindow,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in startInfo.Arguments)
        {
            processStartInfo.ArgumentList.Add(argument);
        }

        foreach (var variable in startInfo.Environment)
        {
            if (string.IsNullOrWhiteSpace(variable.Key) || variable.Key.Contains('='))
            {
                throw new InvalidOperationException($"Simulator environment name '{variable.Key}' is invalid.");
            }

            processStartInfo.Environment[variable.Key] = variable.Value;
        }

        var process = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The operating system did not start the selected SITL executable.");
            }

            return Task.FromResult<ISimulatorProcessSession>(new LocalSimulatorProcessSession(
                process,
                Path.GetFullPath(startInfo.ExecutablePath),
                new DateTimeOffset(process.StartTime.ToUniversalTime())));
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    private sealed class LocalSimulatorProcessSession : ISimulatorProcessSession
    {
        private readonly Process process;
        private readonly string executablePath;
        private readonly DateTimeOffset startedAt;
        private readonly System.Collections.Concurrent.ConcurrentQueue<SimulatorOutputLine> recentOutput = new();
        private readonly CancellationTokenSource outputCancellation = new();
        private readonly Task standardOutputTask;
        private readonly Task standardErrorTask;
        private readonly Task<SimulatorRuntimeExit> completion;
        private int stopRequested;
        private int disposed;

        public LocalSimulatorProcessSession(Process process, string executablePath, DateTimeOffset startedAt)
        {
            this.process = process;
            this.executablePath = executablePath;
            this.startedAt = startedAt;
            standardOutputTask = PumpAsync(process.StandardOutput, SimulatorOutputStream.StandardOutput, outputCancellation.Token);
            standardErrorTask = PumpAsync(process.StandardError, SimulatorOutputStream.StandardError, outputCancellation.Token);
            completion = ObserveCompletionAsync();
        }

        public int ProcessId => process.Id;

        public string ExecutablePath => executablePath;

        public DateTimeOffset StartedAt => startedAt;

        public Task<SimulatorRuntimeExit> Completion => completion;

        public IReadOnlyList<SimulatorOutputLine> RecentOutput => recentOutput.ToArray();

        public event EventHandler<SimulatorOutputLine>? OutputReceived;

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Exchange(ref stopRequested, 1);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await completion.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            try
            {
                await StopAsync().ConfigureAwait(false);
            }
            finally
            {
                outputCancellation.Cancel();
                outputCancellation.Dispose();
                process.Dispose();
            }
        }

        private async Task<SimulatorRuntimeExit> ObserveCompletionAsync()
        {
            try
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
                await Task.WhenAll(standardOutputTask, standardErrorTask).ConfigureAwait(false);
                return new SimulatorRuntimeExit(
                    process.ExitCode,
                    Volatile.Read(ref stopRequested) != 0,
                    process.ExitCode == 0 ? "SITL process exited." : $"SITL process exited with code {process.ExitCode}.");
            }
            catch (Exception exception)
            {
                return new SimulatorRuntimeExit(null, Volatile.Read(ref stopRequested) != 0, exception.Message);
            }
        }

        private async Task PumpAsync(
            StreamReader reader,
            SimulatorOutputStream stream,
            CancellationToken cancellationToken)
        {
            try
            {
                while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                {
                    var output = new SimulatorOutputLine(DateTimeOffset.UtcNow, stream, line);
                    recentOutput.Enqueue(output);
                    while (recentOutput.Count > 100)
                    {
                        recentOutput.TryDequeue(out _);
                    }

                    OutputReceived?.Invoke(this, output);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
