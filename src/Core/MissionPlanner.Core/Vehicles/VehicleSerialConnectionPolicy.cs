using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Workflow;

namespace MissionPlanner.Core.Vehicles;

/// <summary>Excludes identifiable non-telemetry devices without rejecting generic USB serial adapters.</summary>
public static class VehicleSerialConnectionPolicy
{
    /// <summary>Returns a reason to reject a device, or null when runtime verification is still appropriate.</summary>
    public static string? GetBlockReason(SerialDeviceDescriptor device)
    {
        var product = device.ProductName ?? string.Empty;
        if (device.UsbIdentifier is { VendorId: 0x0483, ProductId: 0xdf11 }
            || device.BootloaderIdentity is not null
            || device.RuntimeProbe?.BootEnvironment is FirmwareBootEnvironment.ArduPilotBootloader or FirmwareBootEnvironment.Stm32RomDfu
            || product.Contains("bootloader", StringComparison.OrdinalIgnoreCase)
            || product.Contains("DFU", StringComparison.OrdinalIgnoreCase))
        {
            return "This device is in bootloader mode. Use Install Firmware, then reconnect after ArduPilot starts.";
        }

        if (device.BetaflightIdentity is not null || device.RuntimeProbe?.Runtime == FirmwareRuntimeKind.Betaflight
            || product.Contains("Betaflight", StringComparison.OrdinalIgnoreCase)
            || product.Contains("INAV", StringComparison.OrdinalIgnoreCase))
        {
            return "This device is identified as non-ArduPilot firmware and cannot be connected as an ArduPilot vehicle.";
        }

        // STMicroelectronics Virtual COM Port and USB/UART adapters can carry ArduPilot.
        // A generic descriptor is not evidence of a different autopilot.
        return null;
    }
}
