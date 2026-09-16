namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Vehicle logger evidence, independent of PC telemetry recording.</summary>
public sealed record VehicleOnboardLoggingStatus
{
    /// <summary>Gets the downloaded LOG_BACKEND_TYPE bitmask, or null when unknown.</summary>
    public int? BackendType { get; init; }

    /// <summary>Gets whether onboard logging is configured.</summary>
    public bool? Enabled => BackendType is null ? null : BackendType != 0;

    /// <summary>Gets the latest reported logger health; unknown is not assumed healthy.</summary>
    public bool? Healthy { get; init; }

    /// <summary>Gets the latest logger-related status text.</summary>
    public string? LatestMessage { get; init; }

    /// <summary>Gets retained storage failure evidence such as ENOSPC.</summary>
    public string? StorageDetail { get; init; }

    /// <summary>Gets whether a logger rejection currently affects arming.</summary>
    public bool AffectsArming { get; init; }

    /// <summary>Gets a configuration-aware display status.</summary>
    public string DisplayState => Enabled == false ? "Disabled" : Healthy switch
    {
        false => "Error",
        true => "Healthy",
        null => "Unknown"
    };

    /// <summary>Gets empty evidence for a new or disconnected session.</summary>
    public static VehicleOnboardLoggingStatus Empty { get; } = new();

    /// <summary>Combines retained telemetry with the current downloaded configuration.</summary>
    public VehicleOnboardLoggingStatus WithBackend(int? backendType)
    {
        return this with { BackendType = backendType, AffectsArming = backendType != 0 && AffectsArming };
    }
}
