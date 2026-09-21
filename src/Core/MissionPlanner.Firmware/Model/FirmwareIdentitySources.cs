using System.Text.Json;

namespace MissionPlanner.Firmware.Model;

/// <summary>Origin of identity evidence; a firmware claim is not physical board proof.</summary>
public enum FirmwareIdentitySource
{
    /// <summary>USB descriptors.</summary>
    UsbDevice,
    /// <summary>ROM DFU descriptors.</summary>
    DfuDescriptor,
    /// <summary>Queried bootloader protocol.</summary>
    BootloaderProtocol,
    /// <summary>MAVLink AUTOPILOT_VERSION.</summary>
    AutopilotVersion,
    /// <summary>MAVLink startup text.</summary>
    StatusText,
    /// <summary>Metadata embedded in the APJ.</summary>
    ApjManifest,
    /// <summary>Official firmware catalogue.</summary>
    OfficialCatalogue,
    /// <summary>Explicit operator selection, not hardware proof.</summary>
    UserSelection
}

/// <summary>Physical USB/chip evidence, kept separate from installed firmware.</summary>
public sealed record PhysicalDeviceIdentity(string? McuFamily, UsbIdentifier? Usb, string? HardwareUid,
    FirmwareIdentitySource Source = FirmwareIdentitySource.UsbDevice);

/// <summary>Claims made by the currently running application.</summary>
public sealed record RunningFirmwareIdentity(string? Target, int? BoardId, FirmwareVehicleType VehicleType,
    string? Version, string? GitHash, VehicleFirmwareIdentity Telemetry)
{
    /// <summary>Gets whether multiple conflicting startup targets were observed.</summary>
    public bool IsAmbiguous { get; init; }

    /// <summary>Origin of the reported board ID.</summary>
    public FirmwareIdentitySource BoardIdSource => FirmwareIdentitySource.AutopilotVersion;
    /// <summary>Origin of the reported target, when present.</summary>
    public FirmwareIdentitySource TargetSource => FirmwareIdentitySource.StatusText;

    /// <summary>Reads application claims without treating them as physical-board identity.</summary>
    public static RunningFirmwareIdentity FromTelemetry(VehicleFirmwareIdentity identity, IEnumerable<string> startupText)
    {
        var targets = startupText.Select(ParseTarget).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new(targets.Length == 1 ? targets[0] : null,
            identity.BoardVersion >> 16 is var board && board > 0 ? (int)board : null,
            identity.Family switch
            {
                FirmwareFamily.ArduCopter when identity.MavType == 4 => FirmwareVehicleType.Helicopter,
                FirmwareFamily.ArduCopter => FirmwareVehicleType.Copter,
                FirmwareFamily.ArduPlane => FirmwareVehicleType.Plane,
                FirmwareFamily.Rover => FirmwareVehicleType.Rover,
                FirmwareFamily.ArduSub => FirmwareVehicleType.Sub,
                _ => FirmwareVehicleType.Unknown
            },
            identity.FlightVersion is { } version ? $"{version.Major}.{version.Minor}.{version.Patch}" : null,
            identity.FlightGitHash, identity) { IsAmbiguous = targets.Length > 1 };
    }

    /// <summary>Recognizes a startup target followed by the controller's three UID words.</summary>
    public static string? ParseTarget(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length == 4 && words.Skip(1).All(word => word.Length == 8 && word.All(Uri.IsHexDigit))
            ? words[0] : null;
    }
}

/// <summary>Identity of the selected artifact, including exact target and vehicle variant.</summary>
public sealed record SelectedFirmwareIdentity(string? Target, int BoardId, FirmwareVehicleType VehicleType,
    string? Version, string? GitHash, string? McuFamily, FirmwareIdentitySource Source, Uri? Uri = null)
{
    /// <summary>Reads embedded metadata; filenames and folders are never target evidence.</summary>
    public static SelectedFirmwareIdentity FromPackage(ApjFirmwarePackage package)
    {
        string? Read(string key)
        {
            if (!package.RawMetadata.TryGetValue(key, out var json))
            {
                return null;
            }
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.String ? document.RootElement.GetString() : null;
        }
        var target = Read("platform") ?? Read("target") ?? package.Summary;
        var vehicle = Read("vehicle_type") ?? Read("mav-type");
        var type = Enum.TryParse<FirmwareVehicleType>(vehicle, true, out var parsedType) ? parsedType : FirmwareVehicleType.Unknown;
        var variantKnown = type != FirmwareVehicleType.Unknown;
        var version = Read("firmware_version") ?? Read("version");
        // Common APJs use version=0.1 for the container, not the running ArduPilot release.
        if (!System.Version.TryParse(version, out var parsed) || parsed.Build < 0)
        {
            version = null;
        }
        // AP_FWVersion is embedded in the decoded application image. Read only its fixed header,
        // never absolute pointers. Offsets follow ArduPilot's firmware_version_decoder.py.
        ReadOnlySpan<byte> magic = [0xFB, 0x72, 0x65, 0x76, 0x77, 0x66, 0x70, 0x61];
        var image = package.Image.Span;
        var offset = image.IndexOf(magic);
        if (offset >= 0 && image.Length - offset >= 24 && image[(offset + 8)..].IndexOf(magic) < 0)
        {
            var header = image[offset..];
            if (header[9] is 1 or 2 && header[10] is 4 or 8)
            {
                version ??= $"{header[16]}.{header[17]}.{header[18]}";
                if (type == FirmwareVehicleType.Unknown)
                {
                    type = header[12] switch
                    {
                        1 => FirmwareVehicleType.Rover,
                        2 => target?.EndsWith("-heli", StringComparison.OrdinalIgnoreCase) == true
                            ? FirmwareVehicleType.Helicopter : FirmwareVehicleType.Copter,
                        3 => FirmwareVehicleType.Plane,
                        4 => FirmwareVehicleType.AntennaTracker,
                        7 => FirmwareVehicleType.Sub,
                        _ => FirmwareVehicleType.Unknown
                    };
                }
            }
        }
        var mcu = Read("mcu");
        if (mcu is null && package.Description?.StartsWith("Firmware for a ", StringComparison.Ordinal) == true)
        {
            mcu = package.Description["Firmware for a ".Length..].Replace(" board", "", StringComparison.Ordinal).Trim();
        }
        return new(target, package.BoardId, type, version, package.GitIdentity, mcu, FirmwareIdentitySource.ApjManifest)
        { IsVehicleVariantKnown = variantKnown || type != FirmwareVehicleType.Copter || target?.EndsWith("-heli", StringComparison.OrdinalIgnoreCase) == true };
    }

    /// <summary>Gets whether metadata distinguishes the vehicle variant, rather than only its family.</summary>
    public bool IsVehicleVariantKnown { get; init; } = true;

    /// <summary>Gets whether this identity contains sufficient expectations for verified reconnect.</summary>
    public bool HasVerifiableRelease => !string.IsNullOrWhiteSpace(Target) && VehicleType != FirmwareVehicleType.Unknown &&
        System.Version.TryParse(Version, out var version) && version.Build >= 0;

    /// <summary>Gets whether an official production release is required.</summary>
    public bool RequireOfficialRelease { get; init; }

    /// <summary>Projects the official exact variant and release identity.</summary>
    public static SelectedFirmwareIdentity FromRelease(FirmwareManifestEntry release) =>
        new(release.Target.Platform, release.Target.BoardId, release.Target.MavType,
            release.Version.Value, release.GitSha, null, FirmwareIdentitySource.OfficialCatalogue, release.Artifact.DownloadUri)
        { RequireOfficialRelease = release.Channel == FirmwareReleaseChannel.Stable };
}

/// <summary>Independent identity observations for one selected endpoint.</summary>
public sealed record FirmwareIdentitySnapshot(PhysicalDeviceIdentity? Physical, BootloaderIdentity? Bootloader,
    RunningFirmwareIdentity? Running, SelectedFirmwareIdentity? Selected);
