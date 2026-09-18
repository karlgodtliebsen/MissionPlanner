using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;

namespace MissionPlanner.Core.Replay;

/// <summary>A bounded decoded packet-table row, retaining unknown packet bytes.</summary>
/// <param name="Index">Index in the source log.</param>
/// <param name="Time">Recorded UTC time.</param>
/// <param name="Delta">Time since the preceding source packet.</param>
/// <param name="SystemId">Source system.</param>
/// <param name="ComponentId">Source component.</param>
/// <param name="MessageId">Wire message identifier.</param>
/// <param name="Name">Dialect message name or Unknown.</param>
/// <param name="Summary">Decoded summary or hexadecimal bytes.</param>
/// <param name="Severity">STATUSTEXT severity, when available.</param>
/// <param name="Command">Command identifier for ACK correlation.</param>
/// <param name="RawHex">Unmodified frame as hexadecimal.</param>
public sealed record TelemetryPacketRow(int Index, DateTimeOffset Time, TimeSpan Delta, byte SystemId,
    byte ComponentId, uint MessageId, string Name, string Summary, int? Severity, ushort? Command, string RawHex);

/// <summary>Filters a packet table without retaining all decoded messages.</summary>
/// <param name="Search">Text or hexadecimal search.</param>
/// <param name="Message">Message ID or name fragment.</param>
/// <param name="SystemId">Optional source system.</param>
/// <param name="ComponentId">Optional source component.</param>
/// <param name="MaximumSeverity">Optional maximum numeric MAV severity (zero is most severe).</param>
public sealed record TelemetryPacketFilter(string? Search = null, string? Message = null,
    byte? SystemId = null, byte? ComponentId = null, int? MaximumSeverity = null)
{
    /// <summary>Evaluates a decoded row against all selected filters.</summary>
    public bool Matches(TelemetryPacketRow row)
        => (string.IsNullOrWhiteSpace(Search) || (row.Summary + " " + row.RawHex).Contains(Search, StringComparison.OrdinalIgnoreCase))
           && (string.IsNullOrWhiteSpace(Message) || row.MessageId.ToString() == Message || row.Name.Contains(Message, StringComparison.OrdinalIgnoreCase))
           && (SystemId is null || row.SystemId == SystemId)
           && (ComponentId is null || row.ComponentId == ComponentId)
           && (MaximumSeverity is null || row.Severity <= MaximumSeverity);
}

/// <summary>A bounded page and the source cursor for the next page.</summary>
/// <param name="Rows">At most the requested number of decoded rows.</param>
/// <param name="NextIndex">Next source packet to scan.</param>
public sealed record TelemetryPacketPage(IReadOnlyList<TelemetryPacketRow> Rows, int NextIndex);

/// <summary>Decodes only requested pages using the live decoder and dialect registries. Owns no UI or live connection.</summary>
public sealed class TelemetryPacketBrowser(ITelemetryLogReader reader, IMavLinkMessageDecodeHandler decoder,
    IMavLinkMessageDefinitionRegistry definitions)
{
    /// <summary>Scans from a source cursor, retaining at most 256 decoded rows.</summary>
    public async Task<TelemetryPacketPage> ReadPageAsync(Stream stream, TelemetryLogIndex index, int start,
        TelemetryPacketFilter filter, int pageSize = 200, CancellationToken cancellationToken = default)
    {
        if (pageSize is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var rows = new List<TelemetryPacketRow>(pageSize);
        var cursor = Math.Clamp(start, 0, index.Entries.Count);
        while (cursor < index.Entries.Count && rows.Count < pageSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = index.Entries[cursor];
            var record = await reader.ReadAsync(stream, entry, cancellationToken).ConfigureAwait(false);
            var row = Decode(record, cursor == 0 ? TimeSpan.Zero : entry.Timestamp - index.Entries[cursor - 1].Timestamp);
            if (filter.Matches(row))
            {
                rows.Add(row);
            }

            cursor++;
            if (cursor % 256 == 0)
            {
                await Task.Yield();
            }
        }

        return new(rows, cursor);
    }

    /// <summary>Finds the first packet at or after a recorded timestamp using binary search.</summary>
    public static int FindIndex(TelemetryLogIndex index, DateTimeOffset timestamp)
    {
        var low = 0;
        var high = index.Entries.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (index.Entries[middle].Timestamp < timestamp)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private TelemetryPacketRow Decode(TelemetryLogRecord record, TimeSpan delta)
    {
        var raw = record.Packet.Span;
        var v2 = raw[0] == 0xFD;
        var header = v2 ? 10 : 6;
        var id = v2 ? (uint)(raw[7] | raw[8] << 8 | raw[9] << 16) : raw[5];
        var system = raw[v2 ? 5 : 3];
        var component = raw[v2 ? 6 : 4];
        var frame = new MavLinkFrame(system, component, new TransportEndPoint("replay"), id,
            raw[v2 ? 4 : 2], record.Packet.Slice(header, raw[1]), record.Packet, record.Entry.Timestamp);
        var name = definitions.TryGet(id, out var definition) ? definition.Name : "Unknown";
        var hex = Convert.ToHexString(raw);
        string summary = hex;
        int? severity = null;
        ushort? command = null;
        try
        {
            if (decoder.TryDecode(frame, out var message) && message is not null)
            {
                summary = message.ToString();
                if (message is StatusTextMessage status)
                {
                    severity = (int)status.Severity;
                    summary = status.Text;
                }
                else if (message is CommandAckMessage ack)
                {
                    command = ack.Command;
                    summary = $"Command {ack.Command}: ACK result {ack.Result}, progress {ack.Progress}";
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IndexOutOfRangeException)
        {
            summary = $"Decode unavailable: {ex.Message} · {hex}";
        }

        return new(record.Entry.FrameNumber, record.Entry.Timestamp, delta, system, component, id, name,
            summary, severity, command, hex);
    }
}
