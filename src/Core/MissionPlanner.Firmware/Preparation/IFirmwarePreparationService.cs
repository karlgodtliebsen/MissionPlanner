using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Preparation;

/// <summary>Downloads and validates firmware without accessing hardware.</summary>
public interface IFirmwarePreparationService
{
    /// <summary>Prepares a selected manifest artifact for later installation.</summary>
    Task<FirmwarePreparationResult> PrepareAsync(FirmwarePreparationRequest request, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Parses, hashes and atomically imports local APJ content without asserting target compatibility.</summary>
    Task<LocalFirmwarePreparationResult> ImportAsync(Stream content, string fileName, string? originalPath = null,
        CancellationToken cancellationToken = default);
}
