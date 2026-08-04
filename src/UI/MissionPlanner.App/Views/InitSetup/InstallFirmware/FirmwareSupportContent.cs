using System.Diagnostics;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Contains one offline firmware-help section.</summary>
public sealed record FirmwareSupportSection(string Title, string Content);

/// <summary>Captures presentation evidence used to choose concise contextual guidance.</summary>
public sealed record FirmwareSupportContext(
    bool DfuDevicePresent = false,
    bool CubeProgrammerAvailable = true,
    bool WrongDfuDriver = false,
    bool SerialDevicePresent = true,
    bool TargetAmbiguous = false,
    bool PackageBoardMismatch = false,
    FirmwareReleaseChannel Channel = FirmwareReleaseChannel.Stable,
    bool CustomPackageSelected = false);

/// <summary>Contains one context-sensitive help result.</summary>
public sealed record FirmwareContextHelp(string Title, string Content, FirmwareSupportCategory? LinkCategory = null);

/// <summary>Maps current firmware evidence to user guidance without exposing exception text.</summary>
public static class FirmwareContextHelpResolver
{
    /// <summary>Returns the highest-priority guidance for the supplied context.</summary>
    public static FirmwareContextHelp Resolve(FirmwareSupportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.PackageBoardMismatch)
            return new("Firmware target does not match", "Compare the manifest and package board IDs, then select the exact hardware platform. Compatibility cannot be overridden before erase.", FirmwareSupportCategory.ArduPilot);
        if (context.WrongDfuDriver)
            return new("DFU driver needs attention", "Verify the STM32 BOOTLOADER VID/PID in Device Manager. Install STM32CubeProgrammer and its bundled driver first; use Zadig only as a clearly identified fallback.", FirmwareSupportCategory.DriverFallback);
        if (context.DfuDevicePresent && !context.CubeProgrammerAvailable)
            return new("STM32 DFU device detected", "Install STM32CubeProgrammer before continuing. For initial ArduPilot installation, confirm the exact target and choose its *_with_bl.hex image.", FirmwareSupportCategory.StMicroelectronics);
        if (context.DfuDevicePresent)
            return new("STM32 DFU device detected", "Use the DFU workflow rather than serial APJ installation. Confirm the exact target before selecting *_with_bl.hex.", FirmwareSupportCategory.StMicroelectronics);
        if (context.TargetAmbiguous)
            return new("Identify the exact hardware target", "Vehicle family is not sufficient. Search the printed board/platform name and compare board ID, USB identity, and bootloader aliases before selecting firmware.", FirmwareSupportCategory.ArduPilot);
        if (context.CustomPackageSelected)
            return new("Custom firmware provenance", "Confirm the package source, board ID, features, and build identity. Compatibility and provenance are your responsibility.", FirmwareSupportCategory.ArduPilot);
        if (context.Channel == FirmwareReleaseChannel.Latest)
            return new("Latest is a development build", "Use Latest only for experienced testing. Prefer Stable for normal operation and preserve a recovery path.", FirmwareSupportCategory.ArduPilot);
        if (context.Channel == FirmwareReleaseChannel.Beta)
            return new("Beta may contain defects", "Beta supports wider pre-release testing. Prefer Stable unless you intend to test and report issues.", FirmwareSupportCategory.ArduPilot);
        if (!context.SerialDevicePresent)
            return new("No serial flight controller detected", "Check a data-capable cable, power, boot mode, and Device Manager. STM32 ROM DFU is a USB device and normally is not a COM port.");
        return new("Standard serial installation", "Confirm the exact board target, use Download & Validate first, and keep power connected through erase, programming, verification, and reboot.");
    }
}

/// <summary>Provides concise offline guidance for firmware selection and recovery.</summary>
public static class FirmwareSupportContent
{
    /// <summary>Gets the embedded help sections in presentation order.</summary>
    public static IReadOnlyList<FirmwareSupportSection> Sections { get; } =
    [
        new("Choosing the correct firmware",
            "Choose the exact hardware platform or board, then the release. Vehicle family alone is not enough. Mission Planner checks board ID, bootloader, and USB evidence before erase. Frame geometry is configured later and normally does not select board firmware."),
        new("Release channels",
            "Stable is recommended for normal use. Beta is wider pre-release testing and may contain defects. Latest is a development build for experienced users. Historical releases are for diagnosis or controlled downgrade. Custom firmware makes provenance and compatibility the operator's responsibility."),
        new("Firmware file types",
            ".apj is the normal ArduPilot GCS-loadable package. .px4 is an older name in the same package family. .hex is Intel HEX for DFU or programming tools. *_with_bl.hex contains application firmware plus the ArduPilot bootloader and is normally used for initial DFU installation or recovery. *_bl.hex is bootloader-only and is an advanced recovery/update image, not a normal application install."),
        new("Standard installation versus USB DFU",
            "Use standard APJ installation when an ArduPilot-compatible serial bootloader exists and the device enumerates as a serial/COM port. Use STM32 ROM USB DFU for initial installation or recovery. DFU normally appears as STM32 BOOTLOADER under USB devices, not as a COM port. Confirm the exact target before using *_with_bl.hex."),
        new("Entering bootloader or DFU mode",
            "Consult the controller documentation: boards may require holding BOOT/DFU, bridging BOOT pads while connecting USB, or a reset-plus-boot sequence. Release the control after enumeration. Do not confuse the ArduPilot serial bootloader with the STM32 ROM DFU bootloader."),
        new("Windows Device Manager",
            "Application mode normally appears under Ports (COM & LPT) or as a board-specific USB serial device. The ArduPilot bootloader may also expose a COM port. STM32 ROM DFU normally appears as STM32 BOOTLOADER under Universal Serial Bus devices. A yellow warning or Unknown device indicates a driver issue; repeated arrival/removal can indicate cable, power, boot-mode, or driver trouble."),
        new("Driver and tool order",
            "1. Install or update STM32CubeProgrammer and its bundled DFU driver. 2. Reconnect and inspect Device Manager. 3. Refresh USB in STM32CubeProgrammer. 4. Use Zadig only as a documented third-party fallback after positively identifying the device. Replacing the driver for the wrong USB device can make it unavailable to its normal software; verify VID/PID and device name first."),
        new("Platform limitations",
            "Catalogue download and validation are designed to be cross-platform but currently validated on Windows. Serial APJ install and STM32CubeProgrammer integration are Windows-first. Device Manager and driver diagnostics are Windows-only. Linux and macOS require later validation; mobile USB-host flashing is outside the initial scope. Connected MAVLink bootloader update depends on a supported active transport and vehicle."),
        new("Recovery",
            "Keep power stable, use a data-capable USB cable, remove hubs where practical, and retry DFU detection before assuming the controller is bricked. Verify the exact target before flashing. Mission Planner must not change STM32 option bytes in the initial DFU workflow. Secure or protected devices require specialized recovery guidance. Preserve the operation ID, selected and detected board IDs, USB identity, stage, failure code, and diagnostic report.")
    ];
}

/// <summary>Opens Windows Device Manager through a host-specific boundary.</summary>
public interface IDeviceManagerLauncher
{
    /// <summary>Gets whether Device Manager is available on this host.</summary>
    bool IsAvailable { get; }
    /// <summary>Opens Device Manager.</summary>
    Task OpenAsync(CancellationToken cancellationToken = default);
}

/// <summary>Implements the Windows-only Device Manager host action.</summary>
public sealed class DeviceManagerLauncher : IDeviceManagerLauncher
{
    /// <inheritdoc />
    public bool IsAvailable => OperatingSystem.IsWindows();

    /// <inheritdoc />
    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsAvailable)
            throw new PlatformNotSupportedException("Device Manager is available only on Windows.");

        Process.Start(new ProcessStartInfo("devmgmt.msc") { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
