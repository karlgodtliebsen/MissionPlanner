namespace MissionPlanner.Firmware.Betaflight.Protocol;

/// <summary>Supported native wire formats; jumbo and v2-over-v1 frames are not supported.</summary>
public enum MspProtocolVersion
{
    /// <summary>One-byte command and XOR checksum.</summary>
    V1,
    /// <summary>Native two-byte command with CRC-8 DVB-S2.</summary>
    V2
}

/// <summary>A checksum-validated response with an owned, bounded payload.</summary>
public sealed record MspFrame(MspProtocolVersion Version, ushort Command, byte[] Payload, bool IsError);

/// <summary>Typed protocol/transport outcome.</summary>
public enum MspFailure
{
    /// <summary>Request succeeded.</summary>
    None,
    /// <summary>No matching response before the deadline.</summary>
    Timeout,
    /// <summary>Caller cancelled the request.</summary>
    Cancelled,
    /// <summary>The exclusive serial endpoint could not be opened.</summary>
    PortUnavailableOrBusy,
    /// <summary>The endpoint closed or failed during I/O.</summary>
    Disconnected,
    /// <summary>A malformed or oversized frame was observed.</summary>
    MalformedFrame,
    /// <summary>A response checksum was invalid.</summary>
    ChecksumFailure,
    /// <summary>The endpoint explicitly rejected the command.</summary>
    MspErrorResponse,
    /// <summary>The requested protocol feature is unsupported.</summary>
    Unsupported
}

/// <summary>Result of one bounded MSP request; written evidence supports reboot/disconnect handling.</summary>
public sealed record MspResponse(MspFailure Failure, MspFrame? Frame = null, bool RequestWritten = false);
