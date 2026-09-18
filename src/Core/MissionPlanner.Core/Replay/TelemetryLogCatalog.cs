using System.Text.Json;
using MissionPlanner.Library.Logging;

namespace MissionPlanner.Core.Replay;

/// <summary>Optional metadata alongside an otherwise independently readable classic tlog.</summary>
public sealed record TelemetryLogMetadata
{
    /// <summary>UTC recording start.</summary>
    public DateTimeOffset? Started { get; init; }
    /// <summary>UTC recording end.</summary>
    public DateTimeOffset? Ended { get; init; }
    /// <summary>Vehicle name or identifier when known.</summary>
    public string? Vehicle { get; init; }
    /// <summary>Firmware family/version when known.</summary>
    public string? Firmware { get; init; }
}

/// <summary>Catalog row with optional sidecar or inspected timing.</summary>
/// <param name="File">Platform-neutral file metadata.</param>
/// <param name="Metadata">Optional recording context.</param>
public sealed record TelemetryStoredLog(LogStorageItem File, TelemetryLogMetadata Metadata)
{
    /// <summary>Storage identifier.</summary>
    public string Id => File.Id;
    /// <summary>Display name.</summary>
    public string Name => File.Name;
    /// <summary>File size in bytes.</summary>
    public long Size => File.Size;
    /// <summary>Best available start time.</summary>
    public DateTimeOffset Created => Metadata.Started ?? File.Created;
    /// <summary>Duration when known.</summary>
    public TimeSpan? Duration => Metadata.Ended - Metadata.Started;
    /// <summary>Vehicle context, if present.</summary>
    public string Vehicle => Metadata.Vehicle ?? "Unknown";
    /// <summary>Firmware context, if present.</summary>
    public string Firmware => Metadata.Firmware ?? "Unknown";
}

/// <summary>Lists recordings and reads small optional sidecars without opening every tlog.</summary>
public sealed class TelemetryLogCatalog(ILogStorage storage)
{
    /// <summary>Returns newest recordings; missing or malformed sidecars never prevent reading a tlog.</summary>
    public async Task<IReadOnlyList<TelemetryStoredLog>> ListAsync(CancellationToken cancellationToken = default)
    {
        var files = await storage.ListAsync(LogStorageArea.Telemetry, cancellationToken).ConfigureAwait(false);
        var byName = files.ToDictionary(file => file.Id);
        var result = new List<TelemetryStoredLog>();
        foreach (var file in files.Where(file => file.Name.EndsWith(".tlog", StringComparison.OrdinalIgnoreCase)))
        {
            var metadata = new TelemetryLogMetadata();
            if (byName.TryGetValue(file.Id + ".meta.json", out var sidecar) && sidecar.Size <= 65536)
            {
                try
                {
                    await using var input = await storage.OpenReadAsync(LogStorageArea.Telemetry, sidecar.Id, cancellationToken).ConfigureAwait(false);
                    metadata = await JsonSerializer.DeserializeAsync<TelemetryLogMetadata>(input,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken).ConfigureAwait(false) ?? metadata;
                }
                catch (Exception ex) when (ex is JsonException or IOException)
                {
                    // Optional context cannot make the classic recording unreadable.
                }
            }

            result.Add(new(file, metadata));
        }

        return result.OrderByDescending(item => item.Created).ToArray();
    }
}
