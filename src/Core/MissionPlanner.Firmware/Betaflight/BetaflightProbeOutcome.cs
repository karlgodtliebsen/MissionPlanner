namespace MissionPlanner.Firmware.Betaflight;

/// <summary>Outcome of an identity probe.</summary>
public enum BetaflightProbeOutcome
{
    /// <summary>Exact BTFL proof received.</summary>
    Success,
    /// <summary>No valid MSP reply.</summary>
    NotMsp,
    /// <summary>Another MSP firmware variant.</summary>
    MspButNotBetaflight,
    /// <summary>Port ownership could not be acquired.</summary>
    PortBusy,
    /// <summary>A probe deadline expired.</summary>
    Timeout,
    /// <summary>Device disconnected.</summary>
    Disconnected,
    /// <summary>Invalid mandatory identity payload.</summary>
    ProtocolError,
    /// <summary>Unsupported API major or host platform.</summary>
    UnsupportedApi,
    /// <summary>Caller cancelled discovery.</summary>
    Cancelled
}