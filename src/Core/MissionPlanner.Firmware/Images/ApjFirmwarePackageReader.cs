using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Images;

/// <summary>Reads bounded APJ/PX4 JSON packages and validates their compressed images.</summary>
public sealed class ApjFirmwarePackageReader(IOptions<FirmwareOptions> options) : IFirmwarePackageReader
{
    /// <inheritdoc />
    public async Task<ApjFirmwarePackage> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 32 }, cancellationToken)
                .ConfigureAwait(false);
            var root = document.RootElement;
            var magic = RequiredString(root, "magic");
            if (magic is not ("APJFWv1" or "PX4FWv1")) throw new FirmwarePackageException($"Unsupported package magic '{magic}'.");
            var boardId = RequiredPositiveInt(root, "board_id");
            var imageSize = RequiredPositiveInt(root, "image_size");
            var maximum = RequiredPositiveInt(root, "image_maxsize");
            if (imageSize > maximum || imageSize > options.Value.MaximumFirmwareImageBytes)
                throw new FirmwarePackageException("Firmware image exceeds a declared or configured safety limit.");
            var image = Decompress(RequiredString(root, "image"), imageSize, cancellationToken);

            var externalSize = OptionalInt(root, "extf_image_size");
            if (externalSize < 0 || externalSize > options.Value.MaximumFirmwareImageBytes)
                throw new FirmwarePackageException("External image exceeds the configured safety limit.");
            var external = externalSize > 0
                ? Decompress(RequiredString(root, "extf_image"), externalSize, cancellationToken)
                : [];
            var raw = root.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetRawText(), StringComparer.Ordinal);
            return new ApjFirmwarePackage(
                boardId, image, maximum, external, OptionalInt(root, "board_revision"), OptionalNullableInt(root, "board_revision_max"),
                OptionalInt(root, "bootloader_min_version"), OptionalBool(root, "secure_boot"), OptionalBool(root, "signed_firmware"), GetString(root, "description"),
                GetString(root, "summary"), GetString(root, "version"),
                GetString(root, "git_identity") ?? GetString(root, "ardupilot_git_hash"), raw);
        }
        catch (FirmwarePackageException) { throw; }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidDataException or OverflowException)
        {
            throw new FirmwarePackageException("Firmware package is invalid.", exception);
        }
    }

    private static byte[] Decompress(string base64, int declaredSize, CancellationToken cancellationToken)
    {
        var compressed = Convert.FromBase64String(base64);
        using var source = new MemoryStream(compressed, false);
        using var zlib = new ZLibStream(source, CompressionMode.Decompress);
        var output = new byte[declaredSize];
        var offset = 0;
        while (offset < output.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = zlib.Read(output, offset, output.Length - offset);
            if (read == 0) break;
            offset += read;
        }
        if (offset != declaredSize || zlib.ReadByte() != -1) throw new FirmwarePackageException("Decompressed image size does not match its declaration.");
        return output;
    }

    private static int RequiredPositiveInt(JsonElement root, string name)
    {
        var value = OptionalInt(root, name);
        return value > 0 ? value : throw new FirmwarePackageException($"Package field {name} must be positive.");
    }
    private static int OptionalInt(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : 0;
    private static int? OptionalNullableInt(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : null;
    private static bool? OptionalBool(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
    private static string RequiredString(JsonElement root, string name) => GetString(root, name) is { Length: > 0 } value ? value : throw new FirmwarePackageException($"Package field {name} is required.");
    private static string? GetString(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
