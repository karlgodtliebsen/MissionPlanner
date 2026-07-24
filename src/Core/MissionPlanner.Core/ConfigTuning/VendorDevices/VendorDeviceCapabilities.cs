namespace MissionPlanner.Core.ConfigTuning.VendorDevices;

/// <summary>Describes vendor-device capabilities without device-specific fields.</summary>
[Flags]
public enum VendorDeviceCapabilities
{
    /// <summary>No verified capability.</summary>
    None = 0,

    /// <summary>The device can be discovered.</summary>
    Discovery = 1,

    /// <summary>Configuration can be read.</summary>
    ReadConfiguration = 2,

    /// <summary>Configuration can be applied.</summary>
    ApplyConfiguration = 4,

    /// <summary>Applied configuration can be read back.</summary>
    ConfirmReadback = 8,

    /// <summary>A failed apply can attempt rollback.</summary>
    Rollback = 16,

    /// <summary>Configuration can be exported without secrets.</summary>
    Export = 32,

    /// <summary>The device requires authentication.</summary>
    Authentication = 64
}
