namespace MissionPlanner.Firmware.Dfu;

/// <summary>Performs bounded Intel HEX parsing and policy inspection.</summary>
public interface IIntelHexInspector
{
    /// <summary>Parses a bounded Intel HEX stream into sorted validated ranges.</summary>
    Task<DfuArtifactMetadata> InspectAsync(Stream stream, CancellationToken cancellationToken = default);
}
