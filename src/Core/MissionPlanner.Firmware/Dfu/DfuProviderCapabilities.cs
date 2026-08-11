namespace MissionPlanner.Firmware.Dfu;

/// <summary>Describes version-dependent operations supported by a DFU provider.</summary>
public sealed record DfuProviderCapabilities(
    bool CanListDevices,
    bool CanInspectDevice,
    bool CanProgramIntelHex,
    bool CanVerify,
    bool CanDetach,
    bool CanSafelyCancelProgramming,
    Version? ProviderVersion = null);
