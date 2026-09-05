using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

using MissionPlanner.Maps.Offline;

namespace MissionPlanner.Library.Windows.Maps;

/// <summary>
/// Validates raster MBTiles pack integrity, metadata, schema, and payload.
/// </summary>
public sealed class MbTilesOfflineMapPackValidator : IOfflineMapPackValidator
{
    /// <inheritdoc />
    public async ValueTask ValidateAsync(OfflineMapPackManifest manifest, string archivePath, CancellationToken cancellationToken = default)
    {
        ValidateManifest(manifest);
        var file = new FileInfo(archivePath);
        if (!file.Exists || file.Length != manifest.SizeBytes)
        {
            throw new InvalidDataException("MBTiles archive size does not match its manifest.");
        }

        await using (var stream = file.OpenRead())
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (!hash.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("MBTiles archive SHA-256 does not match its manifest.");
            }
        }

        var builder = new SqliteConnectionStringBuilder { DataSource = archivePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await RequireTableAsync(connection, "metadata", cancellationToken).ConfigureAwait(false);
        await RequireTableAsync(connection, "tiles", cancellationToken).ConfigureAwait(false);
        var metadata = await ReadMetadataAsync(connection, cancellationToken).ConfigureAwait(false);
        if (metadata.TryGetValue("format", out var format) && !IsSameRasterFormat(format, manifest.RasterFormat))
        {
            throw new InvalidDataException($"MBTiles format '{format}' does not match manifest format '{manifest.RasterFormat}'.");
        }

        if (metadata.TryGetValue("type", out var type) && type.Equals("overlay", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Overlay MBTiles are not accepted as basemap packs.");
        }

        await ValidateTilePayloadAsync(connection, manifest.RasterFormat, cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateManifest(OfflineMapPackManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (string.IsNullOrWhiteSpace(manifest.Id) || manifest.Id.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_')))
        {
            throw new InvalidDataException("Pack ID must contain only letters, digits, hyphens, or underscores.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version) || manifest.Version.Any(character => !(char.IsLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new InvalidDataException("Pack version contains unsafe characters.");
        }

        if (Path.GetFileName(manifest.ArchiveFileName) != manifest.ArchiveFileName || string.IsNullOrWhiteSpace(manifest.ArchiveFileName))
        {
            throw new InvalidDataException("Archive file name must not contain a path.");
        }

        if (manifest.SizeBytes <= 0 || manifest.Sha256.Length != 64 || !manifest.Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("Manifest size and SHA-256 are required.");
        }

        if (manifest.MinimumZoom < 0 || manifest.MaximumZoom < manifest.MinimumZoom)
        {
            throw new InvalidDataException("Manifest zoom range is invalid.");
        }

        if (manifest.Bounds is not { West: >= -180 and <= 180, East: >= -180 and <= 180, South: >= -90 and <= 90, North: >= -90 and <= 90 }
            || manifest.Bounds.West > manifest.Bounds.East || manifest.Bounds.South > manifest.Bounds.North)
        {
            throw new InvalidDataException("Manifest bounds are invalid.");
        }

        if (!manifest.Projection.Equals("EPSG:3857", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Only EPSG:3857 raster MBTiles are supported.");
        }

        if (!new[] { "png", "jpg", "jpeg", "webp" }.Contains(manifest.RasterFormat, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Only raster PNG, JPEG, or WebP MBTiles are supported.");
        }
    }

    private static async Task RequireTableAsync(SqliteConnection connection, string name, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Parameters.AddWithValue("$name", name);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) != 1)
        {
            throw new InvalidDataException($"MBTiles archive is missing the '{name}' table.");
        }
    }

    private static async Task<Dictionary<string, string>> ReadMetadataAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, value FROM metadata";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            values[reader.GetString(0)] = reader.GetString(1);
        }

        return values;
    }

    private static async Task ValidateTilePayloadAsync(SqliteConnection connection, string format, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT tile_data FROM tiles LIMIT 1";
        var payload = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as byte[]
                      ?? throw new InvalidDataException("MBTiles archive contains no tile payload.");
        var valid = format.ToLowerInvariant() switch
        {
            "png" => payload.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
            "jpg" or "jpeg" => payload.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8 }),
            "webp" => payload.Length >= 12 && payload.AsSpan(0, 4).SequenceEqual("RIFF"u8) && payload.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            var _ => false
        };
        if (!valid)
        {
            throw new InvalidDataException("MBTiles payload is not the declared raster format.");
        }
    }

    private static bool IsSameRasterFormat(string first, string second)
    {
        return first.Equals(second, StringComparison.OrdinalIgnoreCase)
               || new[] { first, second }.All(value => value.Equals("jpg", StringComparison.OrdinalIgnoreCase) || value.Equals("jpeg", StringComparison.OrdinalIgnoreCase));
    }
}
