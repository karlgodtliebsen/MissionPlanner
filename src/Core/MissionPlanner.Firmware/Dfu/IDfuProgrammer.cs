namespace MissionPlanner.Firmware.Dfu;

/// <summary>Controls a validated DFU provider.</summary>
public interface IDfuProgrammer
{
    /// <summary>Gets capabilities of the validated provider version.</summary>
    Task<DfuProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>Inspects one selected USB DFU device.</summary>
    Task<DfuDeviceInformation> InspectAsync(DfuDeviceDescriptor device, CancellationToken cancellationToken = default);

    /// <summary>Programs and immediately verifies one inspected Intel HEX artifact.</summary>
    Task<DfuProgrammingResult> ProgramAndVerifyAsync(DfuProgrammingRequest request, IProgress<DfuProgress>? progress = null, CancellationToken cancellationToken = default);
}
