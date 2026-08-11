namespace MissionPlanner.Firmware.Dfu;

internal sealed class UnavailableDfuProcessRunner : IDfuProcessRunner
{
    public Task<DfuProcessResult> RunAsync(DfuProcessRequest request, IProgress<DfuProcessOutput>? output = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DfuProcessResult(null, [], FailureCode: "ProcessRunnerUnavailable"));
    }
}
