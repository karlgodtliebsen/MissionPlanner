namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Provides official firmware resources and explicitly labeled driver fallbacks.</summary>
public sealed class FirmwareSupportLinkProvider : IFirmwareSupportLinkProvider
{
    private static readonly IReadOnlyList<FirmwareSupportLink> Links =
    [
        Link("ArduPilot firmware server", "Browse official pre-built firmware.", "https://firmware.ardupilot.org/", FirmwareSupportCategory.ArduPilot),
        Link("Pre-built binaries and file types", "Understand APJ, PX4, HEX, and bootloader images.", "https://ardupilot.org/dev/docs/pre-built-binaries.html", FirmwareSupportCategory.ArduPilot),
        Link("Firmware manifest resources", "Review the GCS manifest format and resources.", "https://ardupilot.org/dev/docs/gcs-resources.html", FirmwareSupportCategory.ArduPilot),
        Link("Initial ChibiOS installation", "Load boards that do not yet contain ArduPilot firmware.", "https://ardupilot.org/copter/docs/common-loading-firmware-onto-chibios-only-boards.html", FirmwareSupportCategory.ArduPilot),
        Link("Bootloader update", "Review the supported connected bootloader-update procedure.", "https://ardupilot.org/copter/docs/common-bootloader-update.html", FirmwareSupportCategory.ArduPilot),
        Link("Bootloader technical documentation", "Review ArduPilot bootloader behavior and recovery.", "https://ardupilot.org/dev/docs/bootloader.html", FirmwareSupportCategory.ArduPilot),
        Link("Custom Firmware Builder", "Build an official custom ArduPilot feature set.", "https://custom.ardupilot.org/", FirmwareSupportCategory.ArduPilot),
        Link("Custom builder documentation", "Read the custom-build server guidance.", "https://ardupilot.org/dev/docs/custom-build-server.html", FirmwareSupportCategory.ArduPilot),
        Link("STM32CubeProgrammer", "Download ST's official programmer and bundled DFU driver.", "https://www.st.com/content/st_com/en/stm32cubeprogrammer.html", FirmwareSupportCategory.StMicroelectronics),
        Link("STM32CubeProgrammer documentation", "Read the current official programming documentation.", "https://dev.st.com/stm32cube-docs/prog/latest/en/index.html", FirmwareSupportCategory.StMicroelectronics),
        Link("STM32CubeProgrammer CLI", "Review official command-line usage.", "https://dev.st.com/stm32cube-docs/prog/latest/en/docs/markup/CubeProg_Command_Lines.html", FirmwareSupportCategory.StMicroelectronics),
        Link("Zadig user guide", "Third-party driver fallback; identify the exact USB device before replacing its driver.", "https://github.com/pbatard/libwdi/wiki/Zadig", FirmwareSupportCategory.DriverFallback, true)
    ];

    /// <inheritdoc />
    public IReadOnlyList<FirmwareSupportLink> GetLinks() => Links;

    private static FirmwareSupportLink Link(string title, string description, string uri, FirmwareSupportCategory category, bool thirdParty = false) =>
        new(title, description, new Uri(uri), category, thirdParty);
}
