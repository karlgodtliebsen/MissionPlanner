using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Options;

namespace MissionPlanner.Firmware.Dfu;

/// <summary>Runs strictly validated DFU provider commands with bounded lifetime and output.</summary>
public sealed class DfuProcessRunner(
    IDfuChildProcessFactory processFactory,
    IOptions<DfuOptions> options,
    TimeProvider timeProvider) : IDfuProcessRunner
{
    /// <inheritdoc />
    public async Task<DfuProcessResult> RunAsync(
        DfuProcessRequest request,
        IProgress<DfuProcessOutput>? output = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validationFailure = DfuProcessRequestValidator.Validate(request);
        if (validationFailure is not null) return new DfuProcessResult(null, [], FailureCode: "RequestRejected");
        if (cancellationToken.IsCancellationRequested)
            return new DfuProcessResult(null, [], WasCancelled: true, FailureCode: "CancelledBeforeStart");

        var configured = options.Value;
        var retained = new List<DfuProcessOutput>();
        var retainedCharacters = 0;
        var truncated = false;
        var gate = new object();
        using var child = processFactory.Create(request.ExecutablePath, request.Arguments,
            new UTF8Encoding(false, false));

        void Capture(bool isError, string? text)
        {
            if (text is null) return;
            var item = new DfuProcessOutput(timeProvider.GetUtcNow(), isError, text);
            lock (gate)
            {
                if (retained.Count >= configured.MaximumProviderOutputLines ||
                    retainedCharacters + text.Length > configured.MaximumProviderOutputCharacters)
                {
                    truncated = true;
                    return;
                }
                retained.Add(item);
                retainedCharacters += text.Length;
            }
            try { output?.Report(item); }
            catch (Exception) { }
        }

        child.OutputReceived += text => Capture(false, text);
        child.ErrorReceived += text => Capture(true, text);

        bool started;
        try
        {
            started = await Task.Run(child.Start).WaitAsync(request.StartupTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return Snapshot(null, "StartupTimeout", timedOut: true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Snapshot(null, "CancelledBeforeStart", wasCancelled: true);
        }
        catch (Exception exception) when (exception is Win32Exception or FileNotFoundException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Snapshot(null, exception is FileNotFoundException or Win32Exception { NativeErrorCode: 2 } ? "ExecutableMissing" : "StartFailed");
        }
        if (!started) return Snapshot(null, "StartFailed");

        child.BeginOutputRead();
        var exitTask = child.WaitForExitAsync(CancellationToken.None);
        var timeoutTask = Task.Delay(request.ExecutionTimeout, CancellationToken.None);
        var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        var completed = await Task.WhenAny(exitTask, timeoutTask, cancellationTask).ConfigureAwait(false);

        if (completed == exitTask)
        {
            await exitTask.ConfigureAwait(false);
            return Snapshot(child.ExitCode, child.ExitCode == 0 ? null : "NonzeroExit");
        }

        if (completed == cancellationTask)
        {
            if (request.MayKillProcessTreeOnCancellation) TryKill(child);
            else
            {
                completed = await Task.WhenAny(exitTask, timeoutTask).ConfigureAwait(false);
                if (completed == exitTask)
                {
                    await exitTask.ConfigureAwait(false);
                    return Snapshot(child.ExitCode, "CancelledAtSafeBoundary", wasCancelled: true);
                }
                return Snapshot(null, "CancellationDeferredTimeout", timedOut: true, wasCancelled: true);
            }
            await AwaitTerminationAsync(exitTask).ConfigureAwait(false);
            return Snapshot(child.HasExited ? child.ExitCode : null, "CancelledAndTerminated", wasCancelled: true);
        }

        if (request.MayKillProcessTreeOnCancellation) TryKill(child);
        await AwaitTerminationAsync(exitTask).ConfigureAwait(false);
        return Snapshot(child.HasExited ? child.ExitCode : null, "ExecutionTimeout", timedOut: true);

        DfuProcessResult Snapshot(int? exitCode, string? failureCode, bool timedOut = false, bool wasCancelled = false)
        {
            lock (gate) return new DfuProcessResult(exitCode, retained.ToArray(), timedOut, wasCancelled, failureCode, truncated);
        }
    }

    private static void TryKill(IDfuChildProcess child)
    {
        try { if (!child.HasExited) child.Kill(true); }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }
    }

    private static async Task AwaitTerminationAsync(Task exitTask)
    {
        await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
    }
}
