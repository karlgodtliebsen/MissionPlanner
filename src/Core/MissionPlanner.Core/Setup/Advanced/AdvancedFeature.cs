namespace MissionPlanner.Core.Setup.Advanced;

/// <summary>Stable identities for the thirteen Advanced Setup tools.</summary>
public enum AdvancedFeatureId
{
    /// <summary>Evaluates operator-defined telemetry warnings.</summary>
    Warnings = 1,
    /// <summary>Inspects received telemetry.</summary>
    Inspector,
    /// <summary>Displays nearby obstacles.</summary>
    Proximity,
    /// <summary>Manages MAVLink authentication.</summary>
    Signing,
    /// <summary>Mirrors telemetry to another endpoint.</summary>
    Mirror,
    /// <summary>Outputs navigation sentences.</summary>
    Nmea,
    /// <summary>Sends operator location updates.</summary>
    FollowMe,
    /// <summary>Exports parameter descriptions.</summary>
    ParameterMetadata,
    /// <summary>Relays moving-base positions.</summary>
    MovingBase,
    /// <summary>Removes identifying log information.</summary>
    AnonymousLogs,
    /// <summary>Analyzes frequency content.</summary>
    Fft,
    /// <summary>Analyzes frequency content over time.</summary>
    Spectrogram,
    /// <summary>Provides a consent-controlled support connection.</summary>
    SupportProxy
}

/// <summary>Requirements evaluated before a tool may launch.</summary>
[Flags]
public enum AdvancedRequirement
{
    /// <summary>No prerequisites.</summary>
    None = 0,
    /// <summary>A live transport is needed.</summary>
    Connection = 1,
    /// <summary>An active vehicle is needed.</summary>
    Vehicle = 2,
    /// <summary>A complete parameter set is needed.</summary>
    Parameters = 4,
    /// <summary>Input files must be selectable.</summary>
    FileOpen = 8,
    /// <summary>Output files must be saveable.</summary>
    FileSave = 16,
    /// <summary>An owned serial or network output is needed.</summary>
    OutputEndpoint = 32,
    /// <summary>Permission-controlled operator location is needed.</summary>
    Location = 64,
    /// <summary>Protected or explicitly session-only key storage is needed.</summary>
    Keys = 128
}

/// <summary>Presentation-neutral catalogue entry and stable navigation route.</summary>
public sealed record AdvancedFeature(
    AdvancedFeatureId Id,
    string Title,
    string Description,
    string Category,
    string Help,
    AdvancedRequirement Requirements)
{
    /// <summary>Gets the deterministic parity order.</summary>
    public int SortOrder => (int)Id;

    /// <summary>Gets the stable application route.</summary>
    public string Route => $"SetupAdvanced/{Id}";
}

/// <summary>Defines the complete legacy parity inventory independently of presentation.</summary>
public static class AdvancedFeatureCatalog
{
    /// <summary>Gets all tools in parity order.</summary>
    public static IReadOnlyList<AdvancedFeature> All { get; } = Array.AsReadOnly<AdvancedFeature>(
    [
        new(AdvancedFeatureId.Warnings, "Warning Manager", "Create and acknowledge telemetry warning rules.", "Telemetry", "Warnings are advisory; they do not change vehicle configuration.", AdvancedRequirement.None),
        new(AdvancedFeatureId.Inspector, "MAVLink Inspector", "Inspect message rates and decoded fields.", "Telemetry", "Inspection observes the existing connection without taking ownership.", AdvancedRequirement.Connection),
        new(AdvancedFeatureId.Proximity, "Proximity", "Inspect obstacle and distance measurements.", "Telemetry", "Missing or stale measurements do not indicate clear space.", AdvancedRequirement.Connection | AdvancedRequirement.Vehicle),
        new(AdvancedFeatureId.Signing, "MAVLink Signing", "Manage MAVLink 2 authentication keys.", "Security", "Changing authentication can prevent reconnection. Keep recovery access available.", AdvancedRequirement.Keys),
        new(AdvancedFeatureId.Mirror, "MAVLink Output / Mirror", "Forward telemetry to an explicitly selected endpoint.", "Output", "Forwarded telemetry may contain sensitive vehicle information.", AdvancedRequirement.Connection | AdvancedRequirement.OutputEndpoint),
        new(AdvancedFeatureId.Nmea, "NMEA Output", "Send navigation data as NMEA sentences.", "Output", "Only current, valid positions may be output.", AdvancedRequirement.Connection | AdvancedRequirement.Vehicle | AdvancedRequirement.OutputEndpoint),
        new(AdvancedFeatureId.FollowMe, "Follow Me", "Use the operator's location as a follow target.", "Operations", "This can move the vehicle. Review target, mode, and location accuracy before starting.", AdvancedRequirement.Connection | AdvancedRequirement.Vehicle | AdvancedRequirement.Location),
        new(AdvancedFeatureId.ParameterMetadata, "Parameter Metadata Generator", "Generate and validate parameter metadata files.", "Files", "Generated metadata is documentation; it does not write vehicle parameters.", AdvancedRequirement.FileOpen | AdvancedRequirement.FileSave),
        new(AdvancedFeatureId.MovingBase, "Moving Base", "Relay a selected moving base position.", "Operations", "Incorrect or stale base positions can affect vehicle navigation.", AdvancedRequirement.Connection | AdvancedRequirement.Vehicle),
        new(AdvancedFeatureId.AnonymousLogs, "Anonymous Log Export", "Create a separate privacy-reviewed log export.", "Files", "Always review the export before sharing. The original is preserved.", AdvancedRequirement.FileOpen | AdvancedRequirement.FileSave),
        new(AdvancedFeatureId.Fft, "FFT Analysis", "Analyze recorded signals in the frequency domain.", "Analysis", "Sampling rate and gaps affect the interpretation of peaks.", AdvancedRequirement.FileOpen),
        new(AdvancedFeatureId.Spectrogram, "Spectrogram", "Explore how recorded frequency content changes over time.", "Analysis", "Window size trades time resolution for frequency resolution.", AdvancedRequirement.FileOpen),
        new(AdvancedFeatureId.SupportProxy, "Serial Support Proxy", "Share an explicitly consented support connection.", "Security", "Review the endpoint and access mode. Stop the session to revoke access.", AdvancedRequirement.Connection | AdvancedRequirement.OutputEndpoint)
    ]);
}
