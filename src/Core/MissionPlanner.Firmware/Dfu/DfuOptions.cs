namespace MissionPlanner.Firmware.Dfu;

/// <summary>Configures bounded Intel HEX inspection and conservative STM32 flash policy.</summary>
public sealed class DfuOptions
{
    /// <summary>Gets or sets the default STM32 system-bootloader USB vendor identifier.</summary>
    public ushort DefaultUsbVendorId { get; set; } = 0x0483;

    /// <summary>Gets or sets the default STM32 system-bootloader USB product identifier.</summary>
    public ushort DefaultUsbProductId { get; set; } = 0xDF11;

    /// <summary>Gets or sets Windows driver services accepted by the external DFU provider.</summary>
    public string[] AcceptedWindowsDriverServices { get; set; } = ["WinUSB", "STTub30"];

    /// <summary>Gets or sets the maximum interval between fallback DFU device snapshots.</summary>
    public TimeSpan DevicePollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets or sets the maximum encoded Intel HEX input size.</summary>
    public long MaximumIntelHexSourceBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>Gets or sets the maximum number of unique data bytes represented by a HEX file.</summary>
    public long MaximumIntelHexDataBytes { get; set; } = 4 * 1024 * 1024;

    /// <summary>Gets or sets the maximum inclusive span between the lowest and highest represented addresses.</summary>
    public long MaximumIntelHexAddressSpan { get; set; } = 4 * 1024 * 1024;

    /// <summary>Gets or sets the first address accepted by the conservative STM32 internal-flash policy.</summary>
    public uint Stm32FlashStartAddress { get; set; } = 0x08000000;

    /// <summary>Gets or sets the exclusive end address accepted by the conservative STM32 internal-flash policy.</summary>
    public uint Stm32FlashEndAddressExclusive { get; set; } = 0x08200000;

    /// <summary>Gets or sets the first application address used only to classify package evidence.</summary>
    public uint ExpectedApplicationStartAddress { get; set; } = 0x08010000;
}
