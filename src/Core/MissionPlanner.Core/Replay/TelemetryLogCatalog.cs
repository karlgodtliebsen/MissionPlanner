using System.Text.Json;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.Logging;

namespace MissionPlanner.Core.Replay;

/// <summary>Optional metadata alongside an otherwise independently readable classic tlog.</summary>
public sealed record TelemetryLogMetadata
{
    /// <summary>Number of indexed packets.</summary>
    public int PacketCount { get; init; }
    /// <summary>Autopilot system identifier.</summary>
    public byte? SystemId { get; init; }
    /// <summary>Autopilot component identifier.</summary>
    public byte? ComponentId { get; init; }
    /// <summary>Heartbeat vehicle type.</summary>
    public string? VehicleType { get; init; }
    /// <summary>Reported board identity or startup target text.</summary>
    public string? Board { get; init; }
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

/// <summary>Lists recordings using indexed file evidence and optional sidecar context.</summary>
public sealed class TelemetryLogCatalog(ILogStorage storage, ITelemetryLogReader reader, IMavLinkMessageDecodeHandler decoder)
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

            try
            {
                await using var recording = await storage.OpenReadAsync(LogStorageArea.Telemetry, file.Id, cancellationToken).ConfigureAwait(false);
                var index = await reader.IndexAsync(recording, file.Name, cancellationToken).ConfigureAwait(false);
                result.Add(await RefreshAsync(new(file, metadata), recording, index, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException)
            {
                // Keep damaged or temporarily unavailable recordings visible for export/deletion.
                result.Add(new(file, new TelemetryLogMetadata()));
            }
        }

        return result.OrderByDescending(item => item.Created).ToArray();
    }

    /// <summary>Derives metadata from the same indexed snapshot used for browsing and export.</summary>
    public async Task<TelemetryStoredLog> RefreshAsync(TelemetryStoredLog item, Stream stream,
        TelemetryLogIndex index, CancellationToken cancellationToken = default)
    {
        var identities = new Dictionary<(byte, byte), HeartbeatMessage>();
        var versions = new Dictionary<(byte, byte), AutopilotVersionMessage>();
        var startup = new Dictionary<(byte, byte), List<string>>();
        foreach (var entry in index.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = await reader.ReadAsync(stream, entry, cancellationToken).ConfigureAwait(false);
            var raw = record.Packet;
            var v2 = raw.Span[0] == 0xFD;
            var id = v2 ? (uint)(raw.Span[7] | raw.Span[8] << 8 | raw.Span[9] << 16) : raw.Span[5];
            if (id is not (0 or 148 or 253))
            {
                continue;
            }
            var frame = new MavLinkFrame(raw.Span[v2 ? 5 : 3], raw.Span[v2 ? 6 : 4],
                new TransportEndPoint("replay"), id, raw.Span[v2 ? 4 : 2],
                raw.Slice(v2 ? 10 : 6, raw.Span[1]), raw, entry.Timestamp);
            if (!decoder.TryDecode(frame, out var message))
            {
                continue;
            }
            var key = (frame.SystemId, frame.ComponentId);
            switch (message)
            {
                case HeartbeatMessage heartbeat when heartbeat.Autopilot != 8 && heartbeat.VehicleType != 6:
                    identities[key] = heartbeat;
                    break;
                case AutopilotVersionMessage version:
                    versions[key] = version;
                    break;
                case StatusTextMessage status when entry.Timestamp - index.StartedAt < TimeSpan.FromMinutes(2):
                    if (!startup.TryGetValue(key, out var lines))
                    {
                        startup[key] = lines = [];
                    }
                    if (lines.Count < 100 && !lines.Contains(status.Text))
                    {
                        lines.Add(status.Text);
                    }
                    break;
            }
        }
        var identity = identities.Values.OrderBy(h => h.ComponentId == 1 ? 0 : 1).FirstOrDefault();
        var metadata = new TelemetryLogMetadata { Started = index.StartedAt, Ended = index.EndedAt, PacketCount = index.Entries.Count };
        var source = identity is not null ? (identity.SystemId, identity.ComponentId) : versions.Count > 0 ? versions.Keys.First() : startup.Keys.FirstOrDefault();
        versions.TryGetValue(source, out var firmware);
        startup.TryGetValue(source, out var texts);
        var family = identity is null ? null : VehicleFirmwareIdentityFactory.MapFamily(identity.VehicleType, identity.Autopilot).ToString();
        var startupFirmware = texts?.FirstOrDefault(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"^(ArduCopter|ArduPlane|ArduRover|ArduSub|Rover|PX4)\s+V?\d", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        var target = texts?.FirstOrDefault(t => System.Text.RegularExpressions.Regex.IsMatch(t, @"(?:Cube|Pixhawk|Matek|Kakute|Durandal|CUAV|fmuv|SITL|board:|target:)", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        var versionText = firmware is { FlightSoftwareVersion: > 0 }
            ? $"{firmware.FlightSoftwareVersion >> 24}.{(firmware.FlightSoftwareVersion >> 16) & 255}.{(firmware.FlightSoftwareVersion >> 8) & 255} (build {Convert.ToHexString(firmware.FlightCustomVersion)})"
            : null;
        metadata = metadata with
        {
            SystemId = identity?.SystemId ?? firmware?.SystemId,
            ComponentId = identity?.ComponentId ?? firmware?.ComponentId,
            VehicleType = identity is null ? null : ((MissionPlanner.MavLink.Generated.MavType)identity.VehicleType).ToString(),
            Vehicle = identity is null ? source == default ? item.Metadata.Vehicle : $"System {source.Item1} / component {source.Item2}" : $"System {identity.SystemId} / component {identity.ComponentId} · {(MissionPlanner.MavLink.Generated.MavType)identity.VehicleType}",
            Firmware = versionText is not null ? $"{family} {versionText}".Trim() : startupFirmware ?? family,
            Board = target ?? (firmware is { BoardVersion: > 0 } ? $"Board {firmware.BoardVersion}, vendor {firmware.VendorId}, product {firmware.ProductId}" : null)
        };
        return item with { File = item.File with { Size = index.Length }, Metadata = metadata };
    }
}
