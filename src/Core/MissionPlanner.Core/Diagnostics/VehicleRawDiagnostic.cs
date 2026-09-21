using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Diagnostics;

/// <summary>Bounded advanced wire observation normalized outside the UI.</summary>
/// <param name="VehicleId">Owning connection vehicle.</param>
/// <param name="At">Wire timestamp.</param>
/// <param name="SystemId">Source system.</param>
/// <param name="ComponentId">Source component.</param>
/// <param name="MessageId">Wire message ID.</param>
/// <param name="Name">Known dialect name or Unknown.</param>
/// <param name="Summary">Direction and verification evidence.</param>
/// <param name="Payload">Exact payload bytes as hexadecimal.</param>
public sealed record VehicleRawDiagnostic(VehicleId VehicleId, DateTimeOffset At, byte SystemId,
    byte ComponentId, uint MessageId, string Name, string Summary, string Payload)
{
    /// <summary>Gets traffic direction as observed at the connection boundary.</summary>
    public string Direction { get; init; } = "Inbound";

    /// <summary>Gets the exact wire payload length, before decoder zero expansion.</summary>
    public int PayloadLength => Payload.Length / 2;

    /// <summary>Gets an immutable copy of the complete wire payload, serialized as byte values.</summary>
    public System.Collections.Immutable.ImmutableArray<byte> PayloadBytes =>
        System.Collections.Immutable.ImmutableArray.Create(Convert.FromHexString(Payload));

    /// <summary>Gets the complete frame including header, CRC and any signature.</summary>
    public System.Collections.Immutable.ImmutableArray<byte> FrameBytes { get; init; } = [];

    /// <summary>Gets the complete captured frame length.</summary>
    public int FrameLength => FrameBytes.Length;

    /// <summary>Gets whether the parser validated the CRC against the dialect.</summary>
    public bool CrcVerified { get; init; }

    /// <summary>Gets whether the frame contains a MAVLink 2 signature, not whether it was authenticated.</summary>
    public bool Signed { get; init; }

    /// <summary>Gets signature verification evidence independently of signature presence.</summary>
    public string SignatureStatus { get; init; } = "Unverified";

    /// <summary>Gets wire version 1 or 2, or null for an unrecognized frame.</summary>
    public int? WireVersion { get; init; }

    /// <summary>Gets the dialect base-field length; MAVLink 2 may legitimately carry fewer bytes.</summary>
    public int? BasePayloadLength { get; init; }

    /// <summary>Gets the full dialect payload length including extension fields.</summary>
    public int? DecodedPayloadLength { get; init; }

    /// <summary>Gets decoded message identity from the same frame, when decoding succeeded.</summary>
    public uint? DecodedMessageId { get; init; }

    /// <summary>Copies exact diagnostic bytes from the connection's parser/decoder observation.</summary>
    /// <param name="vehicle">Owning vehicle.</param>
    /// <param name="observation">One frame and its associated decoded message.</param>
    /// <param name="definition">Known dialect metadata, or null.</param>
    /// <returns>An immutable diagnostic snapshot independent of read/parser buffer lifetime.</returns>
    public static VehicleRawDiagnostic Capture(VehicleId vehicle,
        MissionPlanner.MavLink.Services.MavLinkInspectionObservation observation,
        MissionPlanner.MavLink.Messages.MavLinkMessageDefinition? definition)
    {
        var frame = observation.Frame;
        var raw = System.Collections.Immutable.ImmutableArray.Create(frame.RawBytes.ToArray());
        var version = raw.Length > 0 ? raw[0] switch { 0xFD => (int?)2, 0xFE => 1, _ => null } : null;
        var signed = version == 2 && raw.Length > 2 && (raw[2] & 1) != 0;
        var direction = observation.Direction == MissionPlanner.MavLink.Services.MavLinkTrafficDirection.Inbound ? "RX" : "TX";
        var expansion = version == 2 && definition is not null && frame.Payload.Length < definition.MaximumPayloadLength
            ? $" · MAVLink 2 zero-trimmed payload; decoded length {definition.MaximumPayloadLength}"
            : string.Empty;
        return new(vehicle, frame.ReceivedAt, frame.SystemId, frame.ComponentId, frame.MessageId,
            definition?.Name ?? "Unknown",
            $"{direction} · CRC {(observation.CrcVerified ? "verified" : "unverified")} · wire payload {frame.Payload.Length} bytes · frame {raw.Length} bytes{expansion}",
            Convert.ToHexString(frame.Payload.Span))
        {
            Direction = observation.Direction.ToString(),
            FrameBytes = raw,
            CrcVerified = observation.CrcVerified,
            Signed = signed,
            SignatureStatus = observation.Signature.ToString(),
            WireVersion = version,
            BasePayloadLength = definition?.MinimumPayloadLength,
            DecodedPayloadLength = definition?.MaximumPayloadLength,
            DecodedMessageId = observation.Message?.MessageId
        };
    }
}
