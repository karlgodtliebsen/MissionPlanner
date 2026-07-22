namespace MissionPlanner.MavLink.Decoding;

/// <summary>
/// Identifies how an effective MAVLink decoder is implemented and owned.
/// </summary>
public enum MavLinkDecoderKind
{
    /// <summary>The model and decoder are generated directly from the dialect schema.</summary>
    Generated,

    /// <summary>An established hand-written wire contract replaces generated output.</summary>
    HandWrittenOverride,

    /// <summary>A custom decoder is owned by a dedicated protocol workflow.</summary>
    ProtocolWorkflow
}
