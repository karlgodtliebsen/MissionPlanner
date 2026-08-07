namespace MissionPlanner.Core.FlightData.Preflight;

/// <summary>Describes the outcome of an individual readiness check.</summary>
public enum PreflightCheckStatus
{
    /// <summary>Available evidence meets the rule.</summary>
    Pass,

    /// <summary>Available evidence requires operator attention.</summary>
    Warning,

    /// <summary>Available evidence fails the rule.</summary>
    Fail,

    /// <summary>The latest evidence is too old.</summary>
    Stale,

    /// <summary>The required evidence is unavailable or unsupported.</summary>
    NotAvailable
}
