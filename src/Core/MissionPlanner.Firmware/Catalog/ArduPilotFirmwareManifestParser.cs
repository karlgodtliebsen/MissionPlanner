using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Parses and normalizes ArduPilot firmware manifests.</summary>
public sealed class ArduPilotFirmwareManifestParser(IOptions<FirmwareOptions> options) : IFirmwareManifestParser
{
    /// <inheritdoc />
    public IReadOnlyList<FirmwareManifestEntry> Parse(ReadOnlyMemory<byte> content)
        => ParseWithDiagnostics(content).Entries;

    /// <inheritdoc />
    public FirmwareManifestParseResult ParseWithDiagnostics(ReadOnlyMemory<byte> content)
    {
        try
        {
            var json = IsGzip(content.Span) ? Decompress(content) : content.ToArray();
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("firmware", out var releases) || releases.ValueKind != JsonValueKind.Array)
            {
                throw new FirmwareManifestException("Manifest does not contain a firmware array.");
            }

            var accepted = new List<FirmwareManifestEntry>();
            var reasons = new Dictionary<string, int>(StringComparer.Ordinal);
            var total = 0;
            foreach (var item in releases.EnumerateArray())
            {
                total++;
                try { accepted.Add(ParseEntry(item)); }
                catch (Exception exception) when (exception is FirmwareManifestException or ArgumentException or FormatException)
                {
                    var reason = SkipReason(exception);
                    reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
                }
            }

            if (accepted.Count == 0)
                throw new FirmwareManifestException("Manifest contains no usable firmware entries.");
            var entries = accepted
                .GroupBy(EntryKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.Version.SemanticVersion).First())
                .OrderBy(item => item.Target.VehicleType).ThenBy(item => item.Target.Platform, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(item => item.Version.SemanticVersion).ThenBy(item => item.Version.Value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (accepted.Count > entries.Length) reasons["duplicate-mirror"] = accepted.Count - entries.Length;
            return new(entries, new(total, entries.Length, total - entries.Length, reasons));
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
        if (format == FirmwareImageFormat.Unknown) throw new FirmwareManifestException("Manifest release has an unsupported format.");
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
            {
                throw new FirmwareManifestException("Decompressed manifest exceeds the configured size limit.");
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    private static bool IsGzip(ReadOnlySpan<byte> value) => value.Length >= 2 && value[0] == 0x1f && value[1] == 0x8b;
    private static string EntryKey(FirmwareManifestEntry entry) => $"{entry.Target.BoardId}|{entry.Target.Platform}|{entry.Target.VehicleType}|{entry.Channel}|{entry.Version.Value}|{entry.Artifact.Format}";
    private static string RequiredString(JsonElement item, string name) => GetString(item, name) is { Length: > 0 } value ? value : throw new FirmwareManifestException($"Manifest release is missing {name}.");
    private static int RequiredInt(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : throw new FirmwareManifestException($"Manifest release is missing {name}.");
    private static string? GetString(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;
    private static long? GetLong(JsonElement item, string name) => item.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) ? number : null;

    private static IEnumerable<string> ParseStrings(JsonElement item, string name) => item.TryGetProperty(name, out var value)
        ? value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Select(element => element.GetString() ?? string.Empty) : [value.GetString() ?? string.Empty]
        : [];

    private static IReadOnlyList<UsbIdentifier> ParseUsb(JsonElement item)
    {
        var values = ParseStrings(item, "USBID").Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        var parsed = new List<UsbIdentifier>();
        foreach (var text in values)
        {
            var parts = text.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase).Split('/');
            if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var vid) &&
                int.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var pid) && vid > 0 && pid > 0)
            {
                parsed.Add(new UsbIdentifier(vid, pid));
            }
        }

        if (values.Length > 0 && parsed.Count == 0) throw new FirmwareManifestException("Manifest release contains an invalid USB identifier.");
        return parsed;
    }

    private static string SkipReason(Exception exception) => exception.Message switch
    {
        var value when value.Contains("USB identifier", StringComparison.OrdinalIgnoreCase) => "invalid-usb-id",
        var value when value.Contains("unsupported format", StringComparison.OrdinalIgnoreCase) => "unsupported-format",
        var value when value.Contains("board", StringComparison.OrdinalIgnoreCase) => "invalid-board-id",
        var value when value.Contains("URI", StringComparison.OrdinalIgnoreCase) || value.Contains("URL", StringComparison.OrdinalIgnoreCase) => "invalid-uri",
        _ => "missing-or-invalid-field"
    };

    private static FirmwareReleaseChannel ParseChannel(string? value, JsonElement item)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (normalized is not null && (normalized.StartsWith("OFFICIAL", StringComparison.Ordinal) || normalized.StartsWith("STABLE", StringComparison.Ordinal)))
        {
            return FirmwareReleaseChannel.Stable;
        }

        if (normalized?.StartsWith("BETA", StringComparison.Ordinal) == true)
        {
            return FirmwareReleaseChannel.Beta;
        }

        if (normalized is not null && (normalized.StartsWith("DEV", StringComparison.Ordinal) || normalized.StartsWith("LATEST", StringComparison.Ordinal)))
        {
            return FirmwareReleaseChannel.Latest;
        }

        return item.TryGetProperty("latest", out var latest) && latest.ValueKind == JsonValueKind.Number && latest.GetInt32() != 0
            ? FirmwareReleaseChannel.Latest
            : FirmwareReleaseChannel.Historical;
    }

    private static FirmwareVehicleType ParseVehicle(string? value) => value?.Replace("-", string.Empty).Replace("_", string.Empty).ToUpperInvariant() switch
    {
        "COPTER" => FirmwareVehicleType.Copter, "HELICOPTER" or "HELI" => FirmwareVehicleType.Helicopter,
        "PLANE" => FirmwareVehicleType.Plane, "ROVER" => FirmwareVehicleType.Rover, "SUB" => FirmwareVehicleType.Sub,
        "ANTENNATRACKER" => FirmwareVehicleType.AntennaTracker, "BLIMP" => FirmwareVehicleType.Blimp, var _ => FirmwareVehicleType.Unknown
    };

    private static FirmwareImageFormat ParseFormat(string? value, Uri uri) => (value ?? Path.GetExtension(uri.AbsolutePath).TrimStart('.')).ToUpperInvariant() switch
    {
        "APJ" => FirmwareImageFormat.Apj, "PX4" => FirmwareImageFormat.Px4, "HEX" => FirmwareImageFormat.IntelHex, "ABIN" => FirmwareImageFormat.Abin, var _ => FirmwareImageFormat.Unknown
    };
}
