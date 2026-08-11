using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Operations;

/// <summary>Enforces process-wide firmware-operation exclusivity.</summary>
public interface IFirmwareOperationCoordinator
{
    /// <summary>Starts and owns a new firmware operation.</summary>
    /// <param name="kind">The operation use case.</param>
    /// <returns>A uniquely identified state-machine session.</returns>
    IFirmwareOperationSession Begin(FirmwareOperationKind kind);
}
