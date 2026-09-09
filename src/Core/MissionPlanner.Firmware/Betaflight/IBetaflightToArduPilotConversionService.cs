using MissionPlanner.Firmware.Dfu;

namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Composes reviewed compatibility, MSP handoff, existing DFU installation and runtime verification.</summary>
public interface IBetaflightToArduPilotConversionService
{
    /// <summary>Converts only a reviewed exact board to the chosen matching firmware release.</summary>
    Task<BetaflightConversionResult> ConvertAsync(BetaflightConversionRequest request, IProgress<DfuProgress>? progress = null,
        CancellationToken cancellationToken = default);
}