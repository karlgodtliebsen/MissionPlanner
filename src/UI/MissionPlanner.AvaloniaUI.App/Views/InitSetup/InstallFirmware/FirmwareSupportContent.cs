namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.InstallFirmware;

/// <summary>Provides concise offline guidance for firmware selection and recovery.</summary>
public static class FirmwareSupportContent
{
    /// <summary>Gets the embedded help sections in presentation order.</summary>
    public static IReadOnlyList<FirmwareSupportSection> Sections { get; } =
    [
        new(FirmwareSupportTopic.ChoosingFirmware, "Choosing the correct firmware",
            "Choose the exact hardware platform or board, then the release. Vehicle family alone is not enough. Mission Planner checks board ID, bootloader, and USB evidence before erase. Frame geometry is configured later and normally does not select board firmware."),
        new(FirmwareSupportTopic.ReleaseChannels, "Release channels",
            "Stable is recommended for normal use. Beta is wider pre-release testing and may contain defects. Latest is a development build for experienced users. Historical releases are for diagnosis or controlled downgrade. Custom firmware makes provenance and compatibility the operator's responsibility."),
        new(FirmwareSupportTopic.FileTypes, "Firmware file types",
            ".apj is the normal ArduPilot GCS-loadable package. .px4 is an older name in the same package family. .hex is Intel HEX for DFU or programming tools. *_with_bl.hex contains application firmware plus the ArduPilot bootloader and is normally used for initial DFU installation or recovery. *_bl.hex is bootloader-only and is an advanced recovery/update image, not a normal application install."),
        new(FirmwareSupportTopic.InstallationModes, "Standard installation versus USB DFU",
            "Use standard APJ installation when an ArduPilot-compatible serial bootloader exists and the device enumerates as a serial/COM port. Use STM32 ROM USB DFU for initial installation or recovery. DFU normally appears as STM32 BOOTLOADER under USB devices, not as a COM port. Confirm the exact target before using *_with_bl.hex."),
        new(FirmwareSupportTopic.EnteringBootMode, "Entering bootloader or DFU mode",
            "Consult the controller documentation: boards may require holding BOOT/DFU, bridging BOOT pads while connecting USB, or a reset-plus-boot sequence. Release the control after enumeration. Do not confuse the ArduPilot serial bootloader with the STM32 ROM DFU bootloader."),
        new(FirmwareSupportTopic.WindowsDevices, "Windows Device Manager",
            "Application mode normally appears under Ports (COM & LPT) or as a board-specific USB serial device. The ArduPilot bootloader may also expose a COM port. STM32 ROM DFU normally appears as STM32 BOOTLOADER under Universal Serial Bus devices. A yellow warning or Unknown device indicates a driver issue; repeated arrival/removal can indicate cable, power, boot-mode, or driver trouble."),
        new(FirmwareSupportTopic.DriverTools, "Driver and tool order",
            "1. Install or update STM32CubeProgrammer and its bundled DFU driver. 2. Reconnect and inspect Device Manager. 3. Refresh USB in STM32CubeProgrammer. 4. Use Zadig only as a documented third-party fallback after positively identifying the device. Replacing the driver for the wrong USB device can make it unavailable to its normal software; verify VID/PID and device name first."),
        new(FirmwareSupportTopic.PlatformLimitations, "Platform limitations",
            "Catalogue download and validation are designed to be cross-platform but currently validated on Windows. Serial APJ install and STM32CubeProgrammer integration are Windows-first. Device Manager and driver diagnostics are Windows-only. Linux and macOS require later validation; mobile USB-host flashing is outside the initial scope. Connected MAVLink bootloader update depends on a supported active transport and vehicle."),
        new(FirmwareSupportTopic.Recovery, "Recovery",
            "Keep power stable, use a data-capable USB cable, remove hubs where practical, and retry DFU detection before assuming the controller is bricked. Verify the exact target before flashing. Mission Planner must not change STM32 option bytes in the initial DFU workflow. Secure or protected devices require specialized recovery guidance. Preserve the operation ID, selected and detected board IDs, USB identity, stage, failure code, and diagnostic report.")
    ];
}

