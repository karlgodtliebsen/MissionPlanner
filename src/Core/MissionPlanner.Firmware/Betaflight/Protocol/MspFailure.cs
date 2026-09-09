namespace MissionPlanner.Firmware.Betaflight.Protocol;

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