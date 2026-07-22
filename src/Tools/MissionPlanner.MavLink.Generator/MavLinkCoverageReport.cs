namespace MissionPlanner.MavLink.Generator;

/// <summary>
/// Classifies the implementation level of a MAVLink message.
/// </summary>
public enum MavLinkCoverageClassification
{
    /// <summary>The message is known only to the protocol registry.</summary>
    RegistryOnly,
    /// <summary>The message has a typed wire representation.</summary>
    TypedWireMessage,
    /// <summary>The message updates domain telemetry.</summary>
    DomainTelemetry,
    /// <summary>The message participates in a protocol workflow.</summary>
    ProtocolWorkflow,
    /// <summary>The message is emitted but not consumed.</summary>
    OutboundOnly,
    /// <summary>The dialect marks the message deprecated.</summary>
    Deprecated,
    /// <summary>The application intentionally does not support the message.</summary>
    UnsupportedByDesign
}

/// <summary>
/// Describes implementation coverage for one dialect message.
/// </summary>
public sealed record MavLinkCoverageEntry(
    string Dialect,
    uint MessageId,
    string Name,
    byte CrcExtra,
    byte MinimumPayloadLength,
    byte MaximumPayloadLength,
    bool PresentInMessageIds,
    bool KnownToCrcProvider,
    bool TypedModelExists,
    bool TypedDecoderExists,
    bool DomainHandlerExists,
    bool DomainObservationExists,
    MavLinkCoverageClassification Classification);

/// <summary>
/// Contains a deterministic MAVLink implementation coverage report.
/// </summary>
public sealed record MavLinkCoverageReport(
    string SourceRevision,
    string RootDialect,
    IReadOnlyList<MavLinkCoverageEntry> Messages,
    IReadOnlyList<string> IncorrectConstants,
    IReadOnlyList<string> IncorrectCrcExtras,
    IReadOnlyList<string> UnknownDecoderMessageIds);
