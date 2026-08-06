using MissionPlanner.Firmware.Diagnostics;

namespace MissionPlanner.Firmware.Model;

/// <summary>Represents the terminal result of a firmware operation.</summary>
public sealed record FirmwareOperationResult(
    Guid OperationId,
    FirmwareOperationKind Kind,
    FirmwareOperationState State,
    FirmwareOperationFailure? Failure = null,
    SerialDeviceDescriptor? ApplicationDevice = null,
    bool ReconnectSuggested = false,
    FirmwareDiagnosticReport? DiagnosticReport = null);
