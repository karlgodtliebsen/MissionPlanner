namespace MissionPlanner.Firmware.Connected;

/// <summary>Contains the connected update result and reboot guidance.</summary>
public sealed record BootloaderUpdateResult(
    Guid OperationId,
    ConnectedFirmwareCommandResult Result,
    string Code,
    bool RebootRequired,
    string? TechnicalDetail = null);
