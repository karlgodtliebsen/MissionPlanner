using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Parses and normalizes ArduPilot firmware manifests.</summary>
public sealed class ArduPilotFirmwareManifestParser(IOptions<FirmwareOptions> options) : IFirmwareManifestParser
{
    /// <inheritdoc />
    public IReadOnlyList<FirmwareManifestEntry> Parse(ReadOnlyMemory<byte> content)
    {
        try
        {
            var json = IsGzip(content.Span) ? Decompress(content) : content.ToArray();
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("firmware", out var releases) || releases.ValueKind != JsonValueKind.Array)
                throw new FirmwareManifestException("Manifest does not contain a firmware array.");

            return releases.EnumerateArray().Select(ParseEntry)
                .GroupBy(EntryKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.Version.SemanticVersion).First())
                .OrderBy(item => item.Target.VehicleType).ThenBy(item => item.Target.Platform, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(item => item.Version.SemanticVersion).ThenBy(item => item.Version.Value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (FirmwareManifestException) { throw; }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or FormatException or ArgumentException)
        {
            throw new FirmwareManifestException("Firmware manifest is invalid.", exception);
        }
    }

    private FirmwareManifestEntry ParseEntry(JsonElement item)
    {
        var boardId = RequiredInt(item, "board_id");
        var platform = RequiredString(item, "platform");
        var url = new Uri(RequiredString(item, "url"), UriKind.Absolute);
        var versionText = GetString(item, "mav-firmware-version") ?? GetString(item, "version") ?? "unknown";
        Version.TryParse(versionText.Split('-', '+')[0], out var semantic);
        var usb = ParseUsb(item);
        var bootloaders = ParseStrings(item, "bootloader_str");
        var target = new FirmwareBoardTarget(boardId, platform, ParseVehicle(GetString(item, "vehicletype")), usb, bootloaders);
        var format = ParseFormat(GetString(item, "format"), url);
        var encodedSize = GetLong(item, "size");
        var imageSize = GetLong(item, "image_size") ?? GetLong(item, "image-size");
        var artifact = new FirmwareArtifact(url, format, encodedSize, GetString(item, "sha256"), imageSize);
        var raw = item.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetRawText(), StringComparer.Ordinal);
        return new FirmwareManifestEntry(
            new FirmwareVersion(versionText, semantic),
            ParseChannel(GetString(item, "mav-firmware-version-type"), item),
            target,
            artifact,
            GetString(item, "git-sha"),
            raw);
    }

    private byte[] Decompress(ReadOnlyMemory<byte> content)
    {
        using var source = new MemoryStream(content.ToArray(), false);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = gzip.Read(buffer)) > 0)
        {
            if (output.Length + read > options.Value.MaximumManifestBytes)
                throw new FirmwareManifestException("Decompressed manifest exceeds the configured size limit.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static bool IsGzip(ReadOnlySpan<byte> value) => value.Length >= 2 && value[0] == 0x1f && value[1] == 0x8b;
    private static string EntryKey(FirmwareManifestEntry entry) => $"{entry.Target.BoardId}|{entry.Target.VehicleType}|{entry.Channel}|{entry.Version.Value}|{entry.Artifact.DownloadUri}";
    private static string RequiredString(JsonElement item, string name) => GetString(item, name) is { Length: > 0 } value ? value : throw new FirmwareManifestException($"Manifest release is missing {name}.");
    private static int RequiredInt(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : throw new FirmwareManifestException($"Manifest release is missing {name}.");
    private static string? GetString(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;
    private static long? GetLong(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : null;
    private static IEnumerable<string> ParseStrings(JsonElement item, string name) => item.TryGetProperty(name, out var value)
        ? value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(element => element.GetString() ?? string.Empty) : [value.GetString() ?? string.Empty]
        : [];

    private static IEnumerable<UsbIdentifier> ParseUsb(JsonElement item)
    {
        foreach (var text in ParseStrings(item, "USBID"))
        {
            var parts = text.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase).Split('/');
            if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var vid) &&
                int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var pid) && vid > 0 && pid > 0)
                yield return new UsbIdentifier(vid, pid);
        }
    }

    private static FirmwareReleaseChannel ParseChannel(string? value, JsonElement item)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (normalized is not null && (normalized.StartsWith("OFFICIAL", StringComparison.Ordinal) || normalized.StartsWith("STABLE", StringComparison.Ordinal)))
            return FirmwareReleaseChannel.Stable;
        if (normalized?.StartsWith("BETA", StringComparison.Ordinal) == true)
            return FirmwareReleaseChannel.Beta;
        if (normalized is not null && (normalized.StartsWith("DEV", StringComparison.Ordinal) || normalized.StartsWith("LATEST", StringComparison.Ordinal)))
            return FirmwareReleaseChannel.Latest;
        return item.TryGetProperty("latest", out var latest) && latest.ValueKind == JsonValueKind.Number && latest.GetInt32() != 0
            ? FirmwareReleaseChannel.Latest
            : FirmwareReleaseChannel.Historical;
    }

    private static FirmwareVehicleType ParseVehicle(string? value) => value?.Replace("-", string.Empty).Replace("_", string.Empty).ToUpperInvariant() switch
    {
        "COPTER" => FirmwareVehicleType.Copter, "HELICOPTER" or "HELI" => FirmwareVehicleType.Helicopter,
        "PLANE" => FirmwareVehicleType.Plane, "ROVER" => FirmwareVehicleType.Rover, "SUB" => FirmwareVehicleType.Sub,
        "ANTENNATRACKER" => FirmwareVehicleType.AntennaTracker, "BLIMP" => FirmwareVehicleType.Blimp, _ => FirmwareVehicleType.Unknown
    };

    private static FirmwareImageFormat ParseFormat(string? value, Uri uri) => (value ?? Path.GetExtension(uri.AbsolutePath).TrimStart('.')).ToUpperInvariant() switch
    { "APJ" => FirmwareImageFormat.Apj, "PX4" => FirmwareImageFormat.Px4, "HEX" => FirmwareImageFormat.IntelHex, "ABIN" => FirmwareImageFormat.Abin, _ => FirmwareImageFormat.Unknown };
}
