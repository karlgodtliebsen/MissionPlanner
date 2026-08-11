namespace MissionPlanner.Firmware.Dfu;

/// <summary>Runs only controlled DFU-provider process requests.</summary>
public interface IDfuProcessRunner
{
    /// <summary>Runs a controlled direct provider invocation and captures bounded output.</summary>
    Task<DfuProcessResult> RunAsync(DfuProcessRequest request, IProgress<DfuProcessOutput>? output = null, CancellationToken cancellationToken = default);
}
