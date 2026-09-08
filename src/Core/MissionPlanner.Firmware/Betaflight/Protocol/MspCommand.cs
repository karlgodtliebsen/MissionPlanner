namespace MissionPlanner.Firmware.Betaflight.Protocol;

/// <summary>MSP identifiers verified against upstream Betaflight protocol headers.</summary>
public static class MspCommand
{
    /// <summary>Protocol and API version.</summary>
    public const ushort ApiVersion = 1;
    /// <summary>Four-byte firmware variant.</summary>
    public const ushort FcVariant = 2;
    /// <summary>Firmware semantic version.</summary>
    public const ushort FcVersion = 3;
    /// <summary>Board and target information.</summary>
    public const ushort BoardInfo = 4;
    /// <summary>Build date, time and revision.</summary>
    public const ushort BuildInfo = 5;
    /// <summary>Operator-assigned craft name.</summary>
    public const ushort Name = 10;
    /// <summary>Reboot request.</summary>
    public const ushort Reboot = 68;
    /// <summary>Controller status including arming state.</summary>
    public const ushort Status = 101;
    /// <summary>Permanent mode identifiers, used to locate the armed bit in STATUS.</summary>
    public const ushort BoxIds = 119;
    /// <summary>MCU unique identifier.</summary>
    public const ushort Uid = 160;
    /// <summary>Native v2 MCU identifier and length-prefixed name.</summary>
    public const ushort McuInfo = 0x300c;
}
