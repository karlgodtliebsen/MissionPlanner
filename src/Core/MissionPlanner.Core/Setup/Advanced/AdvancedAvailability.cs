namespace MissionPlanner.Core.Setup.Advanced;

/// <summary>Explains why an Advanced tool can or cannot launch.</summary>
public enum AdvancedAvailabilityState
{
    /// <summary>All prerequisites are satisfied.</summary>
    Available,
    /// <summary>A live connection must be established.</summary>
    ConnectionRequired,
    /// <summary>An active vehicle must be selected.</summary>
    VehicleRequired,
    /// <summary>The operator must grant a permission.</summary>
    PermissionRequired,
    /// <summary>The platform cannot provide a prerequisite.</summary>
    UnsupportedPlatform,
    /// <summary>A recoverable prerequisite is not ready.</summary>
    TemporarilyUnavailable
}

/// <summary>Immutable capability evidence supplied by the platform adapter.</summary>
public sealed record AdvancedPlatformCapabilities(
    string Platform,
    bool IsBrowser = false,
    bool FileOpen = false,
    bool FileSave = false,
    bool SerialOutput = false,
    bool NetworkOutput = false,
    bool AuditedOutputBridge = false,
    bool Location = false,
    bool LocationPermissionGranted = false,
    bool SecureKeyStorage = false,
    bool SessionKeyStorage = false);

/// <summary>Supplies current platform evidence without starting hardware or requesting permission.</summary>
public interface IAdvancedPlatformCapabilities
{
    /// <summary>Gets the latest evidence.</summary>
    AdvancedPlatformCapabilities Current { get; }

    /// <summary>Notifies consumers when permission or bridge evidence changes.</summary>
    event Action<AdvancedPlatformCapabilities>? Changed;
}

/// <summary>Default capability provider for platforms without an Advanced adapter.</summary>
public class AdvancedPlatformCapabilitySource : IAdvancedPlatformCapabilities
{
    private AdvancedPlatformCapabilities current = new("Unconfigured platform");

    /// <inheritdoc />
    public AdvancedPlatformCapabilities Current => Volatile.Read(ref current);

    /// <inheritdoc />
    public event Action<AdvancedPlatformCapabilities>? Changed;

    /// <summary>Publishes changed evidence from a platform permission or bridge callback.</summary>
    public void Update(AdvancedPlatformCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        if (Interlocked.Exchange(ref current, capabilities) != capabilities)
        {
            Changed?.Invoke(capabilities);
        }
    }
}

/// <summary>Snapshot of prerequisites from the existing connection and parameter services.</summary>
public sealed record AdvancedSessionState(bool Connected, bool HasVehicle, bool ParametersLoaded);

/// <summary>Availability result with an actionable explanation.</summary>
public sealed record AdvancedAvailability(AdvancedAvailabilityState State, string Reason)
{
    /// <summary>Gets whether launching is permitted.</summary>
    public bool CanLaunch => State == AdvancedAvailabilityState.Available;
}

/// <summary>Evaluates platform prerequisites before connection prerequisites, without side effects.</summary>
public sealed class AdvancedAvailabilityService
{
    /// <summary>Evaluates a tool against current evidence. Registration is checked separately by navigation.</summary>
    public AdvancedAvailability Evaluate(AdvancedFeature feature, AdvancedPlatformCapabilities platform, AdvancedSessionState session)
    {
        var needs = feature.Requirements;
        if (needs.HasFlag(AdvancedRequirement.FileOpen) && !platform.FileOpen)
        {
            return Unsupported("This platform cannot open input files.");
        }
        if (needs.HasFlag(AdvancedRequirement.FileSave) && !platform.FileSave)
        {
            return Unsupported("This platform cannot save exported files.");
        }
        if (needs.HasFlag(AdvancedRequirement.OutputEndpoint)
            && (platform.IsBrowser ? !platform.AuditedOutputBridge : !platform.SerialOutput && !platform.NetworkOutput))
        {
            return Unsupported(platform.IsBrowser
                ? "An audited output bridge is required; the vehicle connection bridge does not provide this capability."
                : "No serial or network output adapter is available.");
        }
        if (needs.HasFlag(AdvancedRequirement.Keys) && !platform.SecureKeyStorage && !platform.SessionKeyStorage)
        {
            return Unsupported("Neither protected nor session-only key storage is available.");
        }
        if (needs.HasFlag(AdvancedRequirement.Location))
        {
            if (!platform.Location)
            {
                return Unsupported("Operator location is unavailable on this platform.");
            }
            if (!platform.LocationPermissionGranted)
            {
                return new(AdvancedAvailabilityState.PermissionRequired, "Grant operator location permission before starting Follow Me.");
            }
        }
        if (needs.HasFlag(AdvancedRequirement.Connection) && !session.Connected)
        {
            return new(AdvancedAvailabilityState.ConnectionRequired, "Connect a vehicle to use this tool.");
        }
        if (needs.HasFlag(AdvancedRequirement.Vehicle) && !session.HasVehicle)
        {
            return new(AdvancedAvailabilityState.VehicleRequired, "Select an active vehicle and wait for its heartbeat.");
        }
        if (needs.HasFlag(AdvancedRequirement.Parameters) && !session.ParametersLoaded)
        {
            return new(AdvancedAvailabilityState.TemporarilyUnavailable, "Wait for the complete vehicle parameter set to load.");
        }
        return new(AdvancedAvailabilityState.Available,
            needs.HasFlag(AdvancedRequirement.Keys) && !platform.SecureKeyStorage
                ? "Available with session-only keys; closing the application clears them."
                : "Available");
    }

    private static AdvancedAvailability Unsupported(string reason) => new(AdvancedAvailabilityState.UnsupportedPlatform, reason);
}
