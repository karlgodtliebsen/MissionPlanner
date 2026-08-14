using MissionPlanner.Firmware.Dfu;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Displays one STM32 ROM DFU device and its driver readiness.</summary>
public sealed class DfuDeviceItemViewModel
{
    /// <summary>Gets the detected DFU device.</summary>
    public DfuDeviceDescriptor Descriptor { get; }

    /// <summary>Initializes a DFU device row.</summary>
    public DfuDeviceItemViewModel(DfuDeviceDescriptor descriptor)
    {
        Descriptor = descriptor;
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"{Descriptor.ProductName ?? "STM32 Bootloader"} · VID_{Descriptor.VendorId:X4}&PID_{Descriptor.ProductId:X4} · {DriverText(Descriptor.DriverState)}";

    private static string DriverText(DfuDriverState state) => state switch
    {
        DfuDriverState.PresentReady => "driver ready",
        DfuDriverState.PresentWrongDriver => "incompatible driver",
        DfuDriverState.PresentWithProblem => "driver problem",
        DfuDriverState.Busy => "device busy",
        _ => "driver state unknown"
    };
}
