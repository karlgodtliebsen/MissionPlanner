using MissionPlanner.Firmware.Model;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.InstallFirmware;

/// <summary>Maps current firmware evidence to user guidance without exposing exception text.</summary>
public static class FirmwareContextHelpResolver
{
    /// <summary>Returns the highest-priority guidance for the supplied context.</summary>
    public static FirmwareContextHelp Resolve(FirmwareSupportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.PackageBoardMismatch)
        {
            return new FirmwareContextHelp("Firmware target does not match", "Select the exact hardware platform. Only a deliberately selected local APJ/PX4 may use the expert board-ID override; all other compatibility checks remain mandatory.", FirmwareSupportCategory.ArduPilot);
        }

        if (context.WrongDfuDriver)
        {
            return new FirmwareContextHelp("DFU driver needs attention", "Verify the STM32 BOOTLOADER VID/PID in Device Manager. Install STM32CubeProgrammer and its bundled driver first; use Zadig only as a clearly identified fallback.", FirmwareSupportCategory.DriverFallback);
        }

        if (context.DfuDevicePresent && !context.CubeProgrammerAvailable)
        {
            return new FirmwareContextHelp("STM32 DFU device detected", "Install STM32CubeProgrammer before continuing. For initial ArduPilot installation, confirm the exact target and choose its *_with_bl.hex image.", FirmwareSupportCategory.StMicroelectronics);
        }

        if (context.DfuDevicePresent)
        {
            return new FirmwareContextHelp("STM32 DFU device detected", "Use the DFU workflow rather than serial APJ installation. Confirm the exact target before selecting *_with_bl.hex.", FirmwareSupportCategory.StMicroelectronics);
        }

        if (context.TargetAmbiguous)
        {
            return new FirmwareContextHelp("Identify the exact hardware target", "Vehicle family is not sufficient. Search the printed board/platform name and compare board ID, USB identity, and bootloader aliases before selecting firmware.", FirmwareSupportCategory.ArduPilot);
        }

        return context.CustomPackageSelected
            ? new FirmwareContextHelp("Custom firmware provenance", "Confirm the package source, board ID, features, and build identity. Compatibility and provenance are your responsibility.", FirmwareSupportCategory.ArduPilot)
            : context.Channel == FirmwareReleaseChannel.Latest
                ? new FirmwareContextHelp("Latest is a development build", "Use Latest only for experienced testing. Prefer Stable for normal operation and preserve a recovery path.", FirmwareSupportCategory.ArduPilot)
                : context.Channel == FirmwareReleaseChannel.Beta
                    ? new FirmwareContextHelp("Beta may contain defects", "Beta supports wider pre-release testing. Prefer Stable unless you intend to test and report issues.", FirmwareSupportCategory.ArduPilot)
                    : !context.SerialDevicePresent
                        ? new FirmwareContextHelp("No serial flight controller detected", "Check a data-capable cable, power, boot mode, and Device Manager. STM32 ROM DFU is a USB device and normally is not a COM port.")
                        : new FirmwareContextHelp("Standard serial installation", "Confirm the exact board target, use Download & Validate first, and keep power connected through erase, programming, verification, and reboot.");
    }
}

