namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Groups firmware support links by owner and purpose.</summary>
public enum FirmwareSupportCategory
{
    /// <summary>Official ArduPilot firmware and documentation resources.</summary>
    ArduPilot,
    /// <summary>Official STMicroelectronics programming resources.</summary>
    StMicroelectronics,
    /// <summary>Clearly identified third-party driver fallback resources.</summary>
    DriverFallback
}

/// <summary>Describes one curated firmware support destination.</summary>
public sealed record FirmwareSupportLink
{
    /// <summary>Initializes a validated support link.</summary>
    public FirmwareSupportLink(string title, string description, Uri uri, FirmwareSupportCategory category, bool isThirdParty = false)
    {
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("A support-link title is required.", nameof(title)) : title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentException("A support-link description is required.", nameof(description)) : description.Trim();
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Firmware support links must use absolute HTTPS URIs.", nameof(uri));

        Uri = uri;
        Category = category;
        IsThirdParty = isThirdParty;
    }

    /// <summary>Gets the display title.</summary>
    public string Title { get; }
    /// <summary>Gets the concise destination description.</summary>
    public string Description { get; }
    /// <summary>Gets the HTTPS destination.</summary>
    public Uri Uri { get; }
    /// <summary>Gets the support category.</summary>
    public FirmwareSupportCategory Category { get; }
    /// <summary>Gets whether the resource is maintained by a third party.</summary>
    public bool IsThirdParty { get; }
}

/// <summary>Provides the curated firmware-support link catalogue.</summary>
public interface IFirmwareSupportLinkProvider
{
    /// <summary>Gets all supported links in presentation order.</summary>
    IReadOnlyList<FirmwareSupportLink> GetLinks();
}

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

/// <summary>Launches a validated external support destination through the host.</summary>
public interface IExternalLinkLauncher
{
    /// <summary>Opens an absolute HTTPS destination.</summary>
    Task OpenAsync(Uri uri, CancellationToken cancellationToken = default);
}

/// <summary>Uses the MAUI host launcher for external HTTPS destinations.</summary>
public sealed class ExternalLinkLauncher : IExternalLinkLauncher
{
    /// <inheritdoc />
    public async Task OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("External firmware links must use HTTPS.", nameof(uri));

        cancellationToken.ThrowIfCancellationRequested();
        if (!await Launcher.Default.OpenAsync(uri))
            throw new InvalidOperationException($"The host could not open {uri.Host}.");
    }
}
