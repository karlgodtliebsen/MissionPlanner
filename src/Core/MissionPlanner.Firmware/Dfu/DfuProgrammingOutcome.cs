namespace MissionPlanner.Firmware.Dfu;

/// <summary>Identifies the conservative outcome of a provider program-and-verify operation.</summary>
public enum DfuProgrammingOutcome
{
    /// <summary>The external tool was not available.</summary>
    ToolNotFound,

    /// <summary>No selected USB DFU device was available.</summary>
    NoDfuDevice,

    /// <summary>The provider could not connect to the selected device.</summary>
    ConnectionFailed,

    /// <summary>The provider rejected the firmware file.</summary>
    FileRejected,

    /// <summary>The provider reported an erase failure.</summary>
    EraseFailed,

    /// <summary>The provider did not prove programming success.</summary>
    ProgrammingFailed,

    /// <summary>The provider reported or failed to prove verification success.</summary>
    VerificationFailed,

    /// <summary>A requested detach operation failed.</summary>
    DetachFailed,

    /// <summary>Programming and immediate verification both succeeded.</summary>
    Succeeded
}
