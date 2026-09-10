using MissionPlanner.Firmware.Downloads;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Preparation;

/// <summary>A structurally valid local APJ imported into the shared artifact store; compatibility remains pending.</summary>
/// <param name="Package">Parsed application image and board metadata.</param>
/// <param name="ArtifactMetadata">Content hash, cache key and import timestamp.</param>
/// <param name="FileName">Original selected file name.</param>
/// <param name="OriginalPath">Original path when supplied by the picker.</param>
/// <param name="WasCacheHit">Whether the identical content already existed in the cache.</param>
public sealed record LocalFirmwarePreparationResult(ApjFirmwarePackage Package, FirmwareArtifactMetadata ArtifactMetadata,
    string FileName, string? OriginalPath, bool WasCacheHit);
