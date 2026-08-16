namespace MissionPlanner.Firmware.Compatibility;

/// <summary>Defines the narrowly scoped compatibility rules approved for one firmware installation.</summary>
public sealed record FirmwareCompatibilityPolicy(bool AllowBoardIdMismatch = false)
{
    /// <summary>Gets the default fail-closed compatibility policy.</summary>
    public static FirmwareCompatibilityPolicy Strict { get; } = new();
}
