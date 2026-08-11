namespace MissionPlanner.Firmware.Dfu;

/// <summary>Orchestrates the complete, separately modeled DFU workflow.</summary>
public interface IDfuInstallationService
{
    /// <summary>Runs the separately modeled DFU installation workflow.</summary>
    Task<DfuProgrammingResult> InstallAsync(DfuInstallationRequest request, IProgress<DfuProgress>? progress = null, CancellationToken cancellationToken = default);
}
